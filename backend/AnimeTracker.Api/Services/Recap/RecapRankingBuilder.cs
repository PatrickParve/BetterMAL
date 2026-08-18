using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The Bayesian season and year rankings (design.md decision
/// "Bayesian ranking of seasons and years", tasks.md 4.1-4.3). Both builders
/// take the period's aired-in-period included set — every entry in it
/// already carries a non-null <c>AiredFrom</c> by construction (design.md
/// decision 5), so attribution here never has to guard against a missing
/// date.</summary>
public static class RecapRankingBuilder
{
    // The count at which a group is treated as fully trusted (design.md
    // decision "The Bayesian v counts scored anime, not watched anime").
    private const int SeasonTrustThreshold = 5;
    private const int YearTrustThreshold = 20;

    private const int PosterCount = 3;

    public static List<RecapSeasonRankingDto> BuildSeasonRanking(
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean)
    {
        var byPoint = airedIncluded.ToLookup(e => SeasonCalendar.GetSeasonFor(e.Anime.AiredFrom!.Value));

        var ranked = period.SeasonPoints
            .Select(point => (
                point.Year,
                point.Season,
                Scored: byPoint[point].Where(e => e.MyScore is not null).ToList()))
            .Where(c => c.Scored.Count > 0)
            .Select(c => (c.Year, c.Season, c.Scored, Weighted: WeightedAverage(c.Scored, globalMean, SeasonTrustThreshold)))
            .OrderByDescending(c => c.Weighted)
            .ThenByDescending(c => c.Scored.Count)
            .ThenBy(c => SeasonCalendar.GetSeasonPointIndex(c.Year, c.Season))
            .ToList();

        return ranked
            .Select((c, index) => new RecapSeasonRankingDto(
                c.Year, c.Season, c.Scored.Count, Math.Round(c.Weighted, 2),
                index == 0 ? TopPosters(c.Scored) : []))
            .ToList();
    }

    public static List<RecapYearRankingDto> BuildYearRanking(
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean)
    {
        var byYear = airedIncluded.ToLookup(e => e.Anime.AiredFrom!.Value.Year);

        var ranked = period.Years
            .Select(year => (Year: year, Scored: byYear[year].Where(e => e.MyScore is not null).ToList()))
            .Where(c => c.Scored.Count > 0)
            .Select(c => (c.Year, c.Scored, Weighted: WeightedAverage(c.Scored, globalMean, YearTrustThreshold)))
            .OrderByDescending(c => c.Weighted)
            .ThenByDescending(c => c.Scored.Count)
            .ThenBy(c => c.Year)
            .ToList();

        return ranked
            .Select((c, index) => new RecapYearRankingDto(
                c.Year, c.Scored.Count, Math.Round(c.Weighted, 2),
                index == 0 ? TopPosters(c.Scored) : []))
            .ToList();
    }

    // W = (v / (v + m)) * R + (m / (v + m)) * C
    private static double WeightedAverage(List<UserAnimeEntry> scored, double globalMean, int trustThreshold)
    {
        var v = scored.Count;
        var r = scored.Average(e => e.MyScore!.Value);
        return (double)v / (v + trustThreshold) * r + (double)trustThreshold / (v + trustThreshold) * globalMean;
    }

    private static List<RecapRankingPosterDto> TopPosters(List<UserAnimeEntry> scored) =>
        scored
            .OrderByDescending(e => e.MyScore!.Value)
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Take(PosterCount)
            .Select(e => new RecapRankingPosterDto(e.AnimeId, e.Anime.Title, e.Anime.PictureUrl))
            .ToList();
}
