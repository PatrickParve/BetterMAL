using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Profile;

public class ProfileService(
    IUserAnimeEntryRepository entryRepository,
    IActivityLogRepository activityLogRepository,
    ITopAnimeSelectionRepository topAnimeSelectionRepository) : IProfileService
{
    private const int RecentActivityCount = 20;

    // The feed only keeps additions and genuine episode increases (collapsing
    // consecutive same-anime increments), so a much larger raw window is
    // pulled before filtering/collapsing down to RecentActivityCount.
    private const int RecentActivityFetchWindow = 200;

    private const int TopAnimeMinimumSize = 10;

    // No per-anime episode duration is cached (MAL's field isn't fetched
    // anywhere), so "Days" approximates using MAL's own fallback assumption
    // for unknown durations rather than tracking real runtimes.
    private const int AssumedMinutesPerEpisode = 24;

    public async Task<ProfileDto> GetProfileAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var recentActivityWindow = await activityLogRepository.GetRecentAsync(RecentActivityFetchWindow, ct);
        var orderedAnimeIds = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);

        var (theyLikedItIDidnt, iLikedItTheyDidnt) = BuildOpinionDivergence(entries);

        return new ProfileDto(
            BuildStats(entries),
            BuildActivityFeed(recentActivityWindow),
            BuildTopAnimeSection(entries, orderedAnimeIds, TopAnimeMediaTypeScope.All),
            BuildRewatchedSection(entries, TopAnimeMediaTypeScope.All),
            BuildScoreDistribution(entries),
            theyLikedItIDidnt,
            iLikedItTheyDidnt);
    }

    public async Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default)
    {
        var history = await activityLogRepository.GetAllAsync(ct);
        return history.Select(ToActivityFeedItem).ToList();
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

    private static ActivityFeedItemDto ToActivityFeedItem(ActivityLog log) =>
        new(log.Id, log.Timestamp, log.AnimeId, log.Anime.Title, log.Anime.EnglishTitle, log.Anime.PictureUrl, log.ChangeType, log.ChangeDetail);

    // window is most-recent-first. Keep only additions and genuine episode
    // increases, collapsing a run of consecutive same-anime increases (in this
    // filtered order) into just the newest one.
    private static List<ActivityFeedItemDto> BuildActivityFeed(List<ActivityLog> window)
    {
        var feed = new List<ActivityFeedItemDto>();

        foreach (var log in window)
        {
            if (log.ChangeType == ActivityChangeType.EpisodeIncremented)
            {
                if (!IsGenuineIncrease(log))
                    continue;

                if (feed.Count > 0 && feed[^1].ChangeType == ActivityChangeType.EpisodeIncremented && feed[^1].AnimeId == log.AnimeId)
                    continue;
            }
            else if (log.ChangeType != ActivityChangeType.Added)
            {
                continue;
            }

            feed.Add(ToActivityFeedItem(log));
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

    private static int? ParseNewEpisodesWatched(ActivityLog log)
    {
        const string prefix = "Episode ";
        return log.ChangeDetail is { } detail && detail.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(detail[prefix.Length..], out var newEpisodesWatched)
            ? newEpisodesWatched
            : null;
    }

    private static AnimeStatsDto BuildStats(List<UserAnimeEntry> entries)
    {
        var totalEpisodes = entries.Sum(e => e.EpisodesWatched);
        var scored = entries.Where(e => e.MyScore is not null).ToList();

        return new AnimeStatsDto(
            Days: Math.Round(totalEpisodes * AssumedMinutesPerEpisode / 1440.0, 1),
            MeanScore: scored.Count > 0 ? Math.Round(scored.Average(e => e.MyScore!.Value), 2) : null,
            Watching: entries.Count(e => e.Status == WatchStatus.Watching),
            Completed: entries.Count(e => e.Status == WatchStatus.Completed),
            OnHold: entries.Count(e => e.Status == WatchStatus.OnHold),
            Dropped: entries.Count(e => e.Status == WatchStatus.Dropped),
            PlanToWatch: entries.Count(e => e.Status == WatchStatus.PlanToWatch),
            TotalEntries: entries.Count,
            Rewatched: entries.Count(e => e.RewatchCount > 0),
            Episodes: totalEpisodes);
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
    // zero, most-rewatched first. Ties break by score (unscored last), then
    // title — the same tie-break vocabulary as TopAnimeOrdering, but over the
    // raw Title rather than display title so the order doesn't shift with
    // title-preference display logic.
    private static RewatchedSectionDto BuildRewatchedSection(List<UserAnimeEntry> entries, string mediaType)
    {
        var items = entries
            .Where(e => e.RewatchCount > 0 && TopAnimeMediaTypeScope.Matches(mediaType, e.Anime.MediaType))
            .OrderByDescending(e => e.RewatchCount)
            .ThenByDescending(e => e.MyScore ?? -1)
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
        var rated = entries.Where(e => e.MyScore is not null && e.Anime.MalScore is not null).ToList();

        var theyLikedItIDidnt = rated
            .Where(e => e.Anime.MalScore!.Value - e.MyScore!.Value >= 3)
            .OrderByDescending(e => e.Anime.MalScore!.Value - e.MyScore!.Value)
            .Select(ToDivergenceItem)
            .ToList();

        var iLikedItTheyDidnt = rated
            .Where(e => e.Anime.MalScore!.Value < 7 && e.MyScore!.Value - e.Anime.MalScore!.Value >= 2)
            .OrderByDescending(e => e.MyScore!.Value - e.Anime.MalScore!.Value)
            .Select(ToDivergenceItem)
            .ToList();

        return (theyLikedItIDidnt, iLikedItTheyDidnt);
    }

    private static OpinionDivergenceItemDto ToDivergenceItem(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value, e.Anime.MalScore!.Value, e.Status == WatchStatus.Completed);
}
