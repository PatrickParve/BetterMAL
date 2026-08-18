using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Recap;

public class RecapService(IUserAnimeEntryRepository entryRepository) : IRecapService
{
    public async Task<RecapDto> GetRecapAsync(RecapPeriod period, string filter, CancellationToken ct = default)
    {
        var wholeList = await entryRepository.GetAllAsync(ct);

        // A season recap has no toggle — it always selects on what aired
        // that season, whatever filter (if any) the caller sent.
        var effectiveFilter = period.Mode == RecapMode.Season ? RecapTimeFilter.Aired : filter;

        var included = RecapEntrySelector.Select(wholeList, period, effectiveFilter);

        // Both filters' counts for *this* period (design.md decision 2), not
        // just the one actually selected on.
        var watchedCount = RecapEntrySelector.Select(wholeList, period, RecapTimeFilter.Watched).Count;
        var airedCount = RecapEntrySelector.Select(wholeList, period, RecapTimeFilter.Aired).Count;

        var stats = RecapStatsBuilder.Build(included, wholeList);
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

        var globalMean = ScoredMean(wholeList);
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

    // C in the Bayesian formula: my mean score across every scored entry in
    // my whole list, not the recap period (design.md decision 3) — null
    // only when I've scored nothing at all, in which case no group could
    // have v > 0 either, so the rankings are trivially empty.
    private static double? ScoredMean(List<UserAnimeEntry> wholeList)
    {
        var scores = wholeList.Where(e => e.MyScore is not null).Select(e => e.MyScore!.Value).ToList();
        return scores.Count > 0 ? scores.Average() : null;
    }

    private static RecapRowDto ToRow(UserAnimeEntry e) =>
        new(e.AnimeId, e.Anime.Title, e.Anime.EnglishTitle, e.Anime.PictureUrl, e.Anime.MediaType,
            e.MyScore, e.Anime.MalScore, e.Status == WatchStatus.Completed);
}
