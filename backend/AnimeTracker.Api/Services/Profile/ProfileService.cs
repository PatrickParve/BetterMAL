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
        var selectedAnimeIds = await topAnimeSelectionRepository.GetSelectedAnimeIdsAsync(ct);

        var (theyLikedItIDidnt, iLikedItTheyDidnt) = BuildOpinionDivergence(entries);

        return new ProfileDto(
            BuildStats(entries),
            BuildActivityFeed(recentActivityWindow),
            BuildTopAnimeSection(entries, selectedAnimeIds),
            BuildScoreDistribution(entries),
            theyLikedItIDidnt,
            iLikedItTheyDidnt);
    }

    public async Task<List<ActivityFeedItemDto>> GetActivityHistoryAsync(CancellationToken ct = default)
    {
        var history = await activityLogRepository.GetAllAsync(ct);
        return history.Select(ToActivityFeedItem).ToList();
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
    // remainder with the next-highest score tiers. When a tier has more
    // members than remaining slots, that tier is the tie-break boundary: a
    // persisted manual selection takes precedence for those slots, falling
    // back to a deterministic (alphabetical) pick for anything it doesn't
    // cover, so the list always still reaches the minimum of 10 when possible.
    private static TopAnimeSectionDto BuildTopAnimeSection(List<UserAnimeEntry> entries, List<int> selectedAnimeIds)
    {
        var scored = entries.Where(e => e.MyScore is not null).ToList();

        var perfectScores = scored
            .Where(e => e.MyScore == 10)
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var items = perfectScores.Select(ToTopAnimeEntry).ToList();
        var slotsRemaining = Math.Max(0, TopAnimeMinimumSize - perfectScores.Count);
        var candidates = new List<TopAnimeEntryDto>();
        var tieBreakSlots = 0;

        var lowerTiers = scored
            .Where(e => e.MyScore < 10)
            .GroupBy(e => e.MyScore!.Value)
            .OrderByDescending(g => g.Key);

        foreach (var tierGroup in lowerTiers)
        {
            if (slotsRemaining == 0) break;

            var tier = tierGroup.OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase).ToList();

            if (tier.Count <= slotsRemaining)
            {
                items.AddRange(tier.Select(ToTopAnimeEntry));
                slotsRemaining -= tier.Count;
                continue;
            }

            tieBreakSlots = slotsRemaining;
            candidates = tier
                .Select(e => new TopAnimeEntryDto(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value))
                .ToList();

            var candidateIds = candidates.Select(c => c.AnimeId).ToHashSet();
            var fillIds = selectedAnimeIds.Where(candidateIds.Contains).Take(tieBreakSlots).ToList();
            if (fillIds.Count < tieBreakSlots)
            {
                var fillIdSet = fillIds.ToHashSet();
                fillIds.AddRange(tier.Select(e => e.AnimeId).Where(id => !fillIdSet.Contains(id)).Take(tieBreakSlots - fillIds.Count));
            }

            var fillSet = fillIds.ToHashSet();
            items.AddRange(candidates.Where(c => fillSet.Contains(c.AnimeId)));
            slotsRemaining = 0;
            break;
        }

        var relevantSelection = candidates.Count == 0
            ? []
            : selectedAnimeIds.Where(id => candidates.Any(c => c.AnimeId == id)).ToList();

        return new TopAnimeSectionDto(items, tieBreakSlots, candidates, relevantSelection);
    }

    private static TopAnimeEntryDto ToTopAnimeEntry(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value);

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
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.MyScore!.Value, e.Anime.MalScore!.Value);
}
