using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Recap;

public class RecapService(
    IUserAnimeEntryRepository entryRepository,
    IActivityLogRepository activityLogRepository,
    IBroadcastLocalTimeConverter localTimeConverter) : IRecapService
{
    public async Task<RecapDto> GetRecapAsync(RecapPeriod period, string filter, CancellationToken ct = default)
    {
        var wholeList = await entryRepository.GetAllAsync(ct);

        // A season recap has no toggle — it always selects on what aired
        // that season, whatever filter (if any) the caller sent.
        var effectiveFilter = period.Mode == RecapMode.Season ? RecapTimeFilter.Aired : filter;

        // design.md decision 3/5: the period's logged episode progress,
        // fetched once and reused for both selection and the stat block's
        // per-period episode figure — a half-open UTC instant range so a
        // row on the period's exact last local instant is still included.
        var (start, end) = period.DateRange;
        var fromUtc = localTimeConverter.LocalMidnightUtc(start);
        var toUtc = localTimeConverter.LocalMidnightUtc(end.AddDays(1));
        var logRows = await activityLogRepository.GetEpisodeProgressInRangeAsync(fromUtc, toUtc, ct);
        var watchLog = RecapWatchLog.BuildMap(logRows);
        var watchedAnimeIds = (IReadOnlySet<int>)watchLog.Keys.ToHashSet();

        var included = RecapEntrySelector.Select(wholeList, period, effectiveFilter, watchedAnimeIds);

        // Both filters' counts for *this* period (design.md decision 2), not
        // just the one actually selected on. The aired-attributed list is
        // also what RecapStatsBuilder counts CurrentlyWatching from
        // (decision 2), so it's kept rather than reduced straight to a count.
        var watchedCount = RecapEntrySelector.Select(wholeList, period, RecapTimeFilter.Watched, watchedAnimeIds).Count;
        var airedIncluded = RecapEntrySelector.Select(wholeList, period, RecapTimeFilter.Aired);
        var airedCount = airedIncluded.Count;

        var stats = RecapStatsBuilder.Build(included, wholeList, airedIncluded, watchLog, effectiveFilter);
        var items = included
            .Select(ToRow)
            .OrderBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var (seasonRanking, yearRanking, seasonTimeRanking, yearTimeRanking) =
            BuildRankings(included, period, effectiveFilter, wholeList);

        return new RecapDto(
            period.Mode, period.StartYear, period.EndYear, period.Season, effectiveFilter,
            watchedCount, airedCount, stats, items, seasonRanking, yearRanking,
            seasonTimeRanking, yearTimeRanking);
    }

    // design.md decision "Bayesian ranking of seasons and years" + tasks.md
    // 4.4: season ranking on multi-year/yearly under "aired" only; year
    // ranking on multi-year under "aired" only; neither on a season recap
    // (folded into effectiveFilter's own gate below) or under "watched". The
    // time rankings (design.md decision 8) share this same eligibility gate
    // but not the score rankings' global-mean requirement, since they don't
    // need a scored anime anywhere to be meaningful — so that guard is
    // scoped to the score rankings alone.
    private static (
        List<RecapSeasonRankingDto> Season, List<RecapYearRankingDto> Year,
        List<RecapTimeRankingDto> SeasonTime, List<RecapTimeRankingDto> YearTime) BuildRankings(
        List<UserAnimeEntry> included, RecapPeriod period, string effectiveFilter, List<UserAnimeEntry> wholeList)
    {
        var rankingEligible = effectiveFilter == RecapTimeFilter.Aired && period.Mode != RecapMode.Season;
        if (!rankingEligible)
            return ([], [], [], []);

        var globalMean = RecapRankingBuilder.ScoredMean(wholeList);
        var seasonRanking = globalMean is { } sm ? RecapRankingBuilder.BuildSeasonRanking(included, period, sm) : [];
        var yearRanking = period.Mode == RecapMode.MultiYear && globalMean is { } ym
            ? RecapRankingBuilder.BuildYearRanking(included, period, ym)
            : [];

        var seasonTimeRanking = RecapRankingBuilder.BuildSeasonTimeRanking(included, period);
        var yearTimeRanking = period.Mode == RecapMode.MultiYear
            ? RecapRankingBuilder.BuildYearTimeRanking(included, period)
            : [];

        return (seasonRanking, yearRanking, seasonTimeRanking, yearTimeRanking);
    }

    private static RecapRowDto ToRow(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.Anime.MediaType,
            e.MyScore, e.Anime.MalScore, e.Status.IsScoreRevealable());
}
