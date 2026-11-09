using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Ranking;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Reads one series' rank in my series ranking and how many series
/// hold one (add-my-rank-badges design.md D4): the same index and the same
/// listed-series figures the Series page's list read sorts, run through
/// <see cref="SeriesMyRank"/>, so the rank and the page's My average order can
/// never disagree. Builds nothing and makes no MyAnimeList request — every
/// figure is computed from stored members, and read fresh on every call, so a
/// re-score moves the answer with no rebuild.</summary>
public class SeriesRankService(
    SeriesRankingLookup rankingLookup, IEpisodeScheduleService scheduleService, IAnimeRankingService rankingService)
{
    /// <param name="ranking">The anime ranking the caller already holds, so the
    /// series read does not load it twice. Loaded here when null.</param>
    public async Task<MyRankDto> GetAsync(int seriesId, AnimeRankingSnapshot? ranking = null, CancellationToken ct = default)
    {
        var index = await rankingLookup.LoadAsync(ct);
        var currentlyAiringIds = index.CurrentlyAiringMainLineAnimeIds();
        var airedEpisodesByAnimeId = currentlyAiringIds.Count > 0
            ? await scheduleService.EpisodesAiredAsOfAsync(currentlyAiringIds, DateTimeOffset.UtcNow, ct)
            : [];
        ranking ??= await rankingService.GetSnapshotAsync(ct);

        var (rank, rankedCount) = SeriesMyRank.Of(seriesId, index.ListedSeries(airedEpisodesByAnimeId, ranking));
        return new MyRankDto(rank, rankedCount);
    }
}
