using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Watching;

namespace AnimeTracker.Api.Services.Profile;

public class ProfileService(
    IUserAnimeEntryRepository entryRepository,
    IActivityLogRepository activityLogRepository,
    ITopAnimeSelectionRepository topAnimeSelectionRepository,
    SeriesRankingLookup seriesRankingLookup,
    ISeriesBuildTrigger seriesBuildTrigger,
    IEpisodeScheduleService episodeScheduleService) : IProfileService
{
    private const int RecentActivityCount = 20;

    // Bounded per read so a cold install's my-list (which can hold thousands
    // of anime with no series) doesn't turn opening the profile page into
    // tens of thousands of queued MAL fetches — coverage instead grows a
    // batch at a time across visits (design.md decision 6/task 4.2).
    private const int MaxSeriesBackfillPerRead = 20;

    // Filtering and per-anime/per-field-group collapsing are both
    // subtractive, so a much larger raw window is pulled before collapsing
    // down to RecentActivityCount — otherwise a long binge or editing
    // session on one anime could leave the feed short of its 20 items.
    private const int RecentActivityFetchWindow = 400;

    private const int TopAnimeMinimumSize = 10;

    // The app-wide fallback runtime for an anime with no cached average
    // episode duration (design.md decision 2) — not the sole basis for
    // "Days" any more, since most anime now carry a real cached duration
    // that WatchMath.EpisodeSeconds prefers. Internal (not private) so
    // WatchMath and SeriesService's runtime stats fall back to the same
    // assumption instead of a second literal.
    internal const int AssumedMinutesPerEpisode = 24;

    public async Task<ProfileDto> GetProfileAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var recentActivityWindow = await activityLogRepository.GetRecentAsync(RecentActivityFetchWindow, ct);
        var orderedAnimeIds = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);

        // profile-stats "All-list episode progress" (design.md decision 8):
        // resolved in one batched call, over just the shortfall — non-dropped
        // entries whose anime publishes no total episode count at all. A show
        // that hasn't started airing has nothing to fetch here (it's excluded
        // from the figure outright — BuildEpisodeProgress), so it's left out
        // of the shortfall too.
        var undeterminedTotalAnime = entries
            .Where(e => e.Status != WatchStatus.Dropped && e.Anime.TotalEpisodes is null && e.Anime.AiringStatus != "not_yet_aired")
            .Select(e => e.Anime)
            .DistinctBy(a => a.Id)
            .ToList();
        var airedCounts = await episodeScheduleService.EpisodesAiredAsOfAsync(undeterminedTotalAnime, DateTimeOffset.UtcNow, ct);

        var (theyLikedItIDidnt, iLikedItTheyDidnt) = BuildOpinionDivergence(entries);
        var (favouriteSeasons, favouriteYears) = BuildFavouriteSeasonsAndYears(entries);

        return new ProfileDto(
            BuildStats(entries),
            BuildEpisodeProgress(entries, airedCounts),
            BuildActivityFeed(recentActivityWindow),
            BuildTopAnimeSection(entries, orderedAnimeIds, TopAnimeMediaTypeScope.All),
            BuildRewatchedSection(entries, TopAnimeMediaTypeScope.All),
            BuildScoreDistribution(entries),
            theyLikedItIDidnt,
            iLikedItTheyDidnt,
            favouriteSeasons,
            favouriteYears);
    }

    public async Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default)
    {
        var history = await activityLogRepository.GetAllAsync(ct);
        return CollapseEpisodeRuns(history);
    }

    public async Task<TopAnimeSectionDto> GetTopAnimeSectionAsync(string mediaType, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var orderedAnimeIds = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        return BuildTopAnimeSection(entries, orderedAnimeIds, mediaType);
    }

    public async Task<RewatchedSectionDto> GetRewatchedSectionAsync(string mediaType, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        return BuildRewatchedSection(entries, mediaType);
    }

    public async Task<TopSeriesSectionDto> GetTopSeriesSectionAsync(CancellationToken ct = default)
    {
        var rankingIndex = await seriesRankingLookup.LoadAsync(ct);

        var items = rankingIndex.EligibleSeries()
            // Base ordering (design.md decision 3/task 3.3): my average
            // descending (nulls last), then scored main-line count
            // descending, then raw title — the client re-sorts/filters this
            // same array when the basis is switched (design.md decision 4).
            .OrderByDescending(s => s.MineMain.Value ?? double.NegativeInfinity)
            .ThenByDescending(s => s.MineMain.ScoredCount)
            .ThenBy(s => s.Title, StringComparer.OrdinalIgnoreCase)
            .Select(ToTopSeriesItem)
            .ToList();

        await ScheduleMissingSeriesBuildsAsync(rankingIndex, ct);

        return new TopSeriesSectionDto(items);
    }

    private static TopSeriesItemDto ToTopSeriesItem(SeriesRankingResult s) =>
        new(s.SeriesId, s.RootAnimeId, s.Title, s.EnglishTitle, s.PictureUrl, s.EntryCount, s.MainLineAiredCount,
            s.MalMain, s.MineMain, s.MalRevealed);

    // Fire-and-forget: enqueues a bounded batch of my-list anime with no
    // stored series onto the existing background build queue, reusing the
    // ranking projection's membership set rather than issuing a second query
    // (design.md decision 6/task 4.1). Enqueue itself never blocks or
    // throws, so nothing here can slow or fail the response (task 4.2); the
    // trigger's own dedupe means repeat reads advance to new ids instead of
    // re-queueing the same batch (task 4.3).
    private async Task ScheduleMissingSeriesBuildsAsync(SeriesRankingIndex rankingIndex, CancellationToken ct)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var missingIds = entries
            .Select(e => e.AnimeId)
            .Where(id => !rankingIndex.HasSeries(id))
            .Take(MaxSeriesBackfillPerRead);

        foreach (var animeId in missingIds)
            seriesBuildTrigger.Enqueue(animeId);
    }

    public async Task ApplyTopAnimeOrderAsync(List<TopAnimeTierOrderRequest> tiers, CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var existingOrder = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        var positionByAnimeId = TopAnimeOrdering.ToPositionMap(existingOrder);

        var membersByScore = entries
            .Where(e => e.MyScore is not null)
            .GroupBy(e => e.MyScore!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        var mismatchedIds = tiers
            .SelectMany(tier =>
            {
                var tierMemberIds = (membersByScore.GetValueOrDefault(tier.Score) ?? [])
                    .Select(e => e.AnimeId)
                    .ToHashSet();
                return tier.AnimeIds.Where(id => !tierMemberIds.Contains(id));
            })
            .ToList();
        if (mismatchedIds.Count > 0)
            throw new TopAnimeTierScoreMismatchException(mismatchedIds);

        var editedIds = new HashSet<int>();
        var editedSegments = new List<int>();

        foreach (var tier in tiers.OrderByDescending(t => t.Score))
        {
            var tierMembers = membersByScore.GetValueOrDefault(tier.Score) ?? [];
            var effectiveOrder = TopAnimeOrdering.OrderTierMembers(tierMembers, positionByAnimeId)
                .Select(e => e.AnimeId)
                .ToList();

            var visibleIds = tier.AnimeIds;
            var visibleIndexes = effectiveOrder
                .Select((animeId, index) => (animeId, index))
                .Where(x => visibleIds.Contains(x.animeId))
                .Select(x => x.index)
                .OrderBy(index => index)
                .ToList();

            var merged = effectiveOrder.ToList();
            for (var i = 0; i < visibleIndexes.Count; i++)
                merged[visibleIndexes[i]] = visibleIds[i];

            editedSegments.AddRange(merged);
            foreach (var animeId in merged) editedIds.Add(animeId);
        }

        var remaining = existingOrder.Where(id => !editedIds.Contains(id));
        var finalOrder = editedSegments.Concat(remaining).ToList();

        await topAnimeSelectionRepository.ReplaceOrderAsync(finalOrder, ct);
    }

    private static ActivityFeedItemDto ToActivityFeedItem(ActivityLog log, int? mergedScore = null, EpisodeRun? episodeRun = null) =>
        new(log.Id, log.Timestamp, log.AnimeId, log.Anime.Title, log.Anime.EnglishTitle, log.Anime.PictureUrl, log.ChangeType, log.ChangeDetail,
            ActivityFeedComposer.Summarize(log, mergedScore, episodeRun));

    // history is most-recent-first and, unlike BuildActivityFeed, unfiltered:
    // the full history is meant to show everything. A score merged into an
    // adjacent completion (see ActivityFeedComposer) is dropped as its own
    // row, with its value folded into the completion's row instead. A binge
    // of consecutive same-anime genuine episode-watched entries would
    // otherwise be one row per episode, so a run of two or more collapses
    // into a single "Episodes low-high" row — the completed run's episode
    // range stays its own row even when the completion right after it is
    // merged with a score. Any other change type, a different anime, a
    // non-genuine (decreasing) entry, or a gap in the log ends the run; runs
    // of one keep their original "Episode N" text.
    private static List<ActivityFeedItemDto> CollapseEpisodeRuns(List<ActivityLog> history)
    {
        var merges = ActivityFeedComposer.FindCompletionScoreMerges(history);
        var droppedScoreLogIds = merges.Values.Select(m => m.ScoreLogId).ToHashSet();

        var result = new List<ActivityFeedItemDto>();

        var index = 0;
        while (index < history.Count)
        {
            var log = history[index];

            if (droppedScoreLogIds.Contains(log.Id))
            {
                index++;
                continue;
            }

            if (log.ChangeType != ActivityChangeType.EpisodeIncremented || !IsGenuineIncrease(log))
            {
                var mergedScore = merges.TryGetValue(log.Id, out var merge) ? merge.Score : null;
                result.Add(ToActivityFeedItem(log, mergedScore));
                index++;
                continue;
            }

            var runEnd = index;
            while (runEnd + 1 < history.Count
                && history[runEnd + 1].ChangeType == ActivityChangeType.EpisodeIncremented
                && history[runEnd + 1].AnimeId == log.AnimeId
                && IsGenuineIncrease(history[runEnd + 1]))
            {
                runEnd++;
            }

            if (runEnd == index)
            {
                result.Add(ToActivityFeedItem(log));
            }
            else
            {
                var episodeNumbers = Enumerable.Range(index, runEnd - index + 1)
                    .Select(i => ParseNewEpisodesWatched(history[i]))
                    .Where(n => n is not null)
                    .Select(n => n!.Value)
                    .ToList();

                var episodeRun = episodeNumbers.Count > 0
                    ? new EpisodeRun(episodeNumbers.Min(), episodeNumbers.Max())
                    : (EpisodeRun?)null;

                result.Add(ToActivityFeedItem(log, episodeRun: episodeRun));
            }

            index = runEnd + 1;
        }

        return result;
    }

    // window is most-recent-first and contains every change type.
    // ActivityFeedComposer.FieldGroupOf both selects the types the feed
    // carries (Added, EpisodeIncremented, Completed, ScoreChanged,
    // RewatchCountChanged, Removed — status and date changes have no group
    // and are dropped) and drives the collapsing below. The completion+score
    // merge runs first so a completion carries its score and the standalone
    // score row never reaches the feed. After that, the feed keeps at most
    // one row per (AnimeId, FieldGroup): walking most-recent-first, the first
    // row seen for a pair is that anime's newest value for that field and
    // every older row for the same pair is dropped. Grouping Added and
    // Removed together means an add -> remove -> re-add cycle shows only the
    // latest membership state. A completion is the newest progress row for
    // its anime whenever one exists, so it wins over the increases behind it
    // without a separate adjacency check.
    private static List<ActivityFeedItemDto> BuildActivityFeed(List<ActivityLog> window)
    {
        var merges = ActivityFeedComposer.FindCompletionScoreMerges(window);
        var droppedScoreLogIds = merges.Values.Select(m => m.ScoreLogId).ToHashSet();

        var feed = new List<ActivityFeedItemDto>();
        var seenGroups = new HashSet<(int AnimeId, ActivityFieldGroup Group)>();

        foreach (var log in window)
        {
            if (droppedScoreLogIds.Contains(log.Id))
                continue;

            if (log.ChangeType == ActivityChangeType.EpisodeIncremented && !IsGenuineIncrease(log))
                continue;

            if (ActivityFeedComposer.FieldGroupOf(log.ChangeType) is not { } group)
                continue;

            if (!seenGroups.Add((log.AnimeId, group)))
                continue;

            var mergedScore = merges.TryGetValue(log.Id, out var merge) ? merge.Score : null;
            feed.Add(ToActivityFeedItem(log, mergedScore));
            if (feed.Count == RecentActivityCount)
                break;
        }

        return feed;
    }

    // Pre-migration rows have no PreviousEpisodesWatched; treated as a
    // best-effort increase since direction can't be reconstructed for them.
    private static bool IsGenuineIncrease(ActivityLog log) =>
        log.PreviousEpisodesWatched is not { } previous
        || (ParseNewEpisodesWatched(log) is { } newEpisodesWatched && newEpisodesWatched > previous);

    // internal (not private): reused by RecapWatchLog so the "Episode N"
    // format is parsed in exactly one place (tasks.md 5.2).
    internal static int? ParseNewEpisodesWatched(ActivityLog log)
    {
        const string prefix = "Episode ";
        return log.ChangeDetail is { } detail && detail.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(detail[prefix.Length..], out var newEpisodesWatched)
            ? newEpisodesWatched
            : null;
    }

    // profile-stats "Anime stats computed from local data" (design.md
    // decisions 1, 2, 4): Episodes is rewatch-inclusive and excludes movies
    // and music (neither is episodic); Movies picks up exactly what Episodes
    // dropped. Days is a different, wider population by design — every
    // entry, whatever its media type or status — since a film or a dropped
    // show still consumed the time I spent on it even though it contributes
    // no episodes.
    private static AnimeStatsDto BuildStats(List<UserAnimeEntry> entries)
    {
        var scored = entries.Where(e => e.MyScore is not null).ToList();
        var episodeEligible = entries.Where(e => !WatchMath.IsMovie(e.Anime) && !WatchMath.IsMusic(e.Anime));
        var totalSeconds = entries.Sum(e => (long)WatchMath.RewatchInclusiveEpisodes(e) * WatchMath.EpisodeSeconds(e.Anime));

        return new AnimeStatsDto(
            Days: Math.Round(totalSeconds / 86400.0, 1),
            MeanScore: scored.Count > 0 ? Math.Round(scored.Average(e => e.MyScore!.Value), 2) : null,
            Watching: entries.Count(e => e.Status == WatchStatus.Watching),
            Completed: entries.Count(e => e.Status == WatchStatus.Completed),
            OnHold: entries.Count(e => e.Status == WatchStatus.OnHold),
            Dropped: entries.Count(e => e.Status == WatchStatus.Dropped),
            PlanToWatch: entries.Count(e => e.Status == WatchStatus.PlanToWatch),
            TotalEntries: entries.Count,
            Rewatched: entries.Count(e => e.RewatchCount > 0),
            Episodes: episodeEligible.Sum(WatchMath.RewatchInclusiveEpisodes),
            Movies: entries.Count(e => WatchMath.IsMovie(e.Anime) && e.EpisodesWatched > 0));
    }

    // profile-stats "All-list episode progress" (design.md decision 7):
    // dropped entries are excluded outright; every other entry resolves its
    // total to its published TotalEpisodes, else the aired-so-far count when
    // known and non-zero. Failing both, a show that hasn't started airing at
    // all has nothing to progress against yet — that's a legitimate "not
    // applicable", not a data gap — so it's excluded outright too, the same
    // as a dropped entry. Anything else reaching this point (airing or
    // finished, with no published total and no stored aired rows) really is
    // missing data and is unresolved: it sits out of both sides of the
    // figure but is still returned in full (alphabetical by title) so the
    // frontend's unresolved-entries overlay can list exactly what the count
    // covers. Watched is clamped to the resolved total so a stored
    // over-count (or a rewatch, which doesn't multiply the contribution)
    // can never push the bar past 100%.
    private static EpisodeProgressDto BuildEpisodeProgress(List<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedCounts)
    {
        var episodesWatched = 0;
        var episodesTotal = 0;
        var unresolvedAnime = new List<UserAnimeEntry>();

        foreach (var entry in entries.Where(e => e.Status != WatchStatus.Dropped))
        {
            var total = entry.Anime.TotalEpisodes
                ?? (airedCounts.TryGetValue(entry.AnimeId, out var aired) && aired > 0 ? aired : null);

            if (total is { } resolvedTotal)
            {
                episodesTotal += resolvedTotal;
                episodesWatched += Math.Min(entry.EpisodesWatched, resolvedTotal);
                continue;
            }

            if (entry.Anime.AiringStatus == "not_yet_aired")
                continue;

            unresolvedAnime.Add(entry);
        }

        var unresolvedAnimeDto = unresolvedAnime
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new UnresolvedEpisodeEntryDto(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.EpisodesWatched))
            .ToList();

        return new EpisodeProgressDto(episodesWatched, episodesTotal, unresolvedAnime.Count, entries.Count, unresolvedAnimeDto);
    }

    // profile-stats "Favourite seasons and years" (design.md decision 6): a
    // synthetic whole-list period spanning every air year I have, fed into
    // the same RecapRankingBuilder a recap uses, with the same whole-list
    // mean — so a season's/year's weighted score here is byte-identical to
    // the score a recap covering it reports.
    private static (List<RecapSeasonRankingDto> Seasons, List<RecapYearRankingDto> Years) BuildFavouriteSeasonsAndYears(
        List<UserAnimeEntry> entries)
    {
        var airDated = entries.Where(e => e.Anime.AiredFrom is not null).ToList();
        if (airDated.Count == 0)
            return ([], []);

        var globalMean = RecapRankingBuilder.ScoredMean(entries);
        if (globalMean is not { } mean)
            return ([], []);

        var years = airDated.Select(e => e.Anime.AiredFrom!.Value.Year);
        var period = RecapPeriod.MultiYear(years.Min(), years.Max());

        return (
            RecapRankingBuilder.BuildSeasonRanking(airDated, period, mean),
            RecapRankingBuilder.BuildYearRanking(airDated, period, mean));
    }

    // All score-10 anime are shown uncapped; if that's fewer than 10, fill the
    // remainder with the next-highest score tiers, in descending order, each
    // ordered by the user's persisted preference (falling back to
    // alphabetical). The tier that doesn't fully fit is truncated — its full
    // membership is still returned (via Tiers) so the overlay can render a
    // cut line and let the user move members across it. Tiers that can never
    // reach the list (score dominance) are omitted entirely.
    private static TopAnimeSectionDto BuildTopAnimeSection(List<UserAnimeEntry> entries, List<int> orderedAnimeIds, string mediaType)
    {
        var positionByAnimeId = TopAnimeOrdering.ToPositionMap(orderedAnimeIds);

        var tierGroups = entries
            .Where(e => e.MyScore is not null && TopAnimeMediaTypeScope.Matches(mediaType, e.Anime.MediaType))
            .GroupBy(e => e.MyScore!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => (
                Score: g.Key,
                Members: TopAnimeOrdering.OrderTierMembers(g, positionByAnimeId).Select(ToTopAnimeEntry).ToList()));

        var items = new List<TopAnimeEntryDto>();
        var tiers = new List<TopAnimeTierDto>();
        var slotsRemaining = TopAnimeMinimumSize;

        foreach (var (score, members) in tierGroups)
        {
            int includedCount;
            if (score == 10)
            {
                includedCount = members.Count;
                slotsRemaining = Math.Max(0, slotsRemaining - includedCount);
            }
            else
            {
                if (slotsRemaining == 0) break;
                includedCount = Math.Min(members.Count, slotsRemaining);
                slotsRemaining -= includedCount;
            }

            items.AddRange(members.Take(includedCount));
            tiers.Add(new TopAnimeTierDto(score, members, includedCount));
        }

        return new TopAnimeSectionDto(items, tiers, mediaType);
    }

    private static TopAnimeEntryDto ToTopAnimeEntry(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value);

    // No tiers, no cut line, no cap: every entry with a rewatch count above
    // zero, most-rewatched first. Ties break by title alone — the same
    // tie-break vocabulary as TopAnimeOrdering, but over the raw Title rather
    // than display title so the order doesn't shift with title-preference
    // display logic.
    private static RewatchedSectionDto BuildRewatchedSection(List<UserAnimeEntry> entries, string mediaType)
    {
        var items = entries
            .Where(e => e.RewatchCount > 0 && TopAnimeMediaTypeScope.Matches(mediaType, e.Anime.MediaType))
            .OrderByDescending(e => e.RewatchCount)
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(ToRewatchedEntry)
            .ToList();

        return new RewatchedSectionDto(items, mediaType);
    }

    private static RewatchedEntryDto ToRewatchedEntry(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.RewatchCount, e.MyScore);

    private static ScoreDistributionDto BuildScoreDistribution(List<UserAnimeEntry> entries)
    {
        var scored = entries.Where(e => e.MyScore is not null).ToList();
        var counts = scored.GroupBy(e => e.MyScore!.Value).ToDictionary(g => g.Key, g => g.Count());

        var buckets = Enumerable.Range(1, 10)
            .Select(score => new ScoreDistributionBucketDto(score, counts.GetValueOrDefault(score)))
            .ToList();

        var meanScore = scored.Count > 0 ? Math.Round(scored.Average(e => e.MyScore!.Value), 2) : (double?)null;

        return new ScoreDistributionDto(buckets, meanScore);
    }

    private static (List<OpinionDivergenceItemDto> TheyLikedItIDidnt, List<OpinionDivergenceItemDto> ILikedItTheyDidnt)
        BuildOpinionDivergence(List<UserAnimeEntry> entries)
    {
        if (ScoreDivergence.TryCompute(entries) is not { } context)
            return ([], []);

        var rated = entries.Where(e => e.MyScore is not null && e.Anime.MalScore is not null).ToList();

        // divergence > 0 means MAL sits further above its mean than I sit
        // above mine — a "they liked it more than I did" direction; < 0 is
        // the reverse (design.md decision 3).
        var scored = rated
            .Select(e => (Entry: e, Divergence: context.DivergenceOf(e)))
            .ToList();

        var theyLikedItIDidnt = scored
            .Where(x => ScoreDivergence.IsTheyLikedItIDidnt(x.Entry, x.Divergence))
            .OrderByDescending(x => x.Divergence)
            .ThenBy(x => x.Entry.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(x => ToDivergenceItem(x.Entry))
            .ToList();

        var iLikedItTheyDidnt = scored
            .Where(x => ScoreDivergence.IsILikedItTheyDidnt(x.Entry, x.Divergence))
            .OrderByDescending(x => -x.Divergence)
            .ThenBy(x => x.Entry.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(x => ToDivergenceItem(x.Entry))
            .ToList();

        return (theyLikedItIDidnt, iLikedItTheyDidnt);
    }

    private static OpinionDivergenceItemDto ToDivergenceItem(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value, e.Anime.MalScore!.Value, e.Status.IsScoreRevealable());
}
