using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Watching;

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

    // C in the Bayesian formula: the caller's mean score across every scored
    // entry in its whole list, not any particular period — null only when
    // nothing at all has been scored, in which case no group could have v > 0
    // either, so the rankings are trivially empty. Shared by RecapService and
    // ProfileService (design.md decision 6) so both rank on the identical C.
    public static double? ScoredMean(List<UserAnimeEntry> wholeList)
    {
        var scores = wholeList.Where(e => e.MyScore is not null).Select(e => e.MyScore!.Value).ToList();
        return scores.Count > 0 ? scores.Average() : null;
    }

    public static List<RecapSeasonRankingDto> BuildSeasonRanking(
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean, AnimeRankingSnapshot rankingSnapshot)
    {
        var byPoint = airedIncluded.ToLookup(e => SeasonCalendar.GetSeasonFor(e.Anime.AiredFrom!.Value));

        var ranked = period.SeasonPoints
            .Select(point => (
                point.Year,
                point.Season,
                Scored: byPoint[point].Where(e => e.MyScore is not null).ToList()))
            .Where(c => c.Scored.Count > 0)
            .Select(c => (
                c.Year, c.Season, c.Scored,
                Weighted: GroupRanking.WeightedAverage(c.Scored, globalMean, SeasonTrustThreshold),
                Histogram: GroupRanking.BuildHistogram(c.Scored),
                Recency: SeasonCalendar.GetSeasonPointIndex(c.Year, c.Season)))
            .ToList();

        ranked.Sort((a, b) => GroupRanking.CompareGroups(
            a.Weighted, a.Scored.Count, a.Histogram,
            b.Weighted, b.Scored.Count, b.Histogram,
            () => NewestFirst(a.Recency, b.Recency)));

        return ranked
            .Select(c => new RecapSeasonRankingDto(
                c.Year, c.Season, c.Scored.Count, Math.Round(c.Weighted, 2),
                GroupRanking.PostersByScore(c.Scored, rankingSnapshot), c.Histogram[1..]))
            .ToList();
    }

    public static List<RecapYearRankingDto> BuildYearRanking(
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean, AnimeRankingSnapshot rankingSnapshot)
    {
        var byYear = airedIncluded.ToLookup(e => e.Anime.AiredFrom!.Value.Year);

        var ranked = period.Years
            .Select(year => (Year: year, Scored: byYear[year].Where(e => e.MyScore is not null).ToList()))
            .Where(c => c.Scored.Count > 0)
            .Select(c => (
                c.Year, c.Scored,
                Weighted: GroupRanking.WeightedAverage(c.Scored, globalMean, YearTrustThreshold),
                Histogram: GroupRanking.BuildHistogram(c.Scored),
                Recency: c.Year))
            .ToList();

        ranked.Sort((a, b) => GroupRanking.CompareGroups(
            a.Weighted, a.Scored.Count, a.Histogram,
            b.Weighted, b.Scored.Count, b.Histogram,
            () => NewestFirst(a.Recency, b.Recency)));

        return ranked
            .Select(c => new RecapYearRankingDto(
                c.Year, c.Scored.Count, Math.Round(c.Weighted, 2),
                GroupRanking.PostersByScore(c.Scored, rankingSnapshot), c.Histogram[1..]))
            .ToList();
    }

    // The last key of the season and year rankings (list-recaps design.md decision
    // 6, step 4): newest first — reverses the oldest-first fallback this
    // replaced. A genre or studio ranking ends on its name instead.
    private static int NewestFirst(int recencyA, int recencyB) =>
        recencyA > recencyB ? -1 : recencyA < recencyB ? 1 : 0;

    // D7/D8: unlike the score rankings, no scored-anime requirement — a group
    // is ranked whenever it has any time watched at all, and ties break by
    // episodes watched then chronologically for a stable order. The score
    // rankings' extended tie-break above (histogram, then newest-first)
    // deliberately does not apply here: this ranks on seconds watched, where
    // an exact tie already requires an exact tie on episodes watched too, so
    // a fifth rule would buy nothing (design.md decision 6).
    //
    // No row carries posters (polish-recap-page follow-up): once this ranking
    // sits beside the season score ranking rather than stacked full-width
    // below it, even the leader's three posters made the row cramped next to
    // its narrower column-mate. The year-level BuildYearTimeRanking below is
    // unaffected and keeps its leader-only posters.
    public static List<RecapTimeRankingDto> BuildSeasonTimeRanking(List<UserAnimeEntry> airedIncluded, RecapPeriod period)
    {
        var byPoint = airedIncluded.ToLookup(e => SeasonCalendar.GetSeasonFor(e.Anime.AiredFrom!.Value));

        var ranked = period.SeasonPoints
            .Select(point => (point.Year, point.Season, Group: byPoint[point].ToList()))
            .Select(c => (c.Year, c.Season, c.Group, Time: GroupTimeSeconds(c.Group), Episodes: c.Group.Sum(e => e.EpisodesWatched)))
            .Where(c => c.Time > 0)
            .OrderByDescending(c => c.Time)
            .ThenByDescending(c => c.Episodes)
            .ThenBy(c => SeasonCalendar.GetSeasonPointIndex(c.Year, c.Season))
            .ToList();

        return ranked
            .Select(c => new RecapTimeRankingDto(c.Year, c.Season, c.Time, c.Episodes, []))
            .ToList();
    }

    public static List<RecapTimeRankingDto> BuildYearTimeRanking(List<UserAnimeEntry> airedIncluded, RecapPeriod period)
    {
        var byYear = airedIncluded.ToLookup(e => e.Anime.AiredFrom!.Value.Year);

        var ranked = period.Years
            .Select(year => (Year: year, Group: byYear[year].ToList()))
            .Select(c => (c.Year, c.Group, Time: GroupTimeSeconds(c.Group), Episodes: c.Group.Sum(e => e.EpisodesWatched)))
            .Where(c => c.Time > 0)
            .OrderByDescending(c => c.Time)
            .ThenByDescending(c => c.Episodes)
            .ThenBy(c => c.Year)
            .ToList();

        return ranked
            .Select((c, index) => new RecapTimeRankingDto(
                c.Year, null, c.Time, c.Episodes,
                index == 0 ? TopPostersByTime(c.Group) : []))
            .ToList();
    }

    private static long GroupTimeSeconds(List<UserAnimeEntry> group) =>
        group.Sum(e => (long)e.EpisodesWatched * WatchMath.EpisodeSeconds(e.Anime));

    private static List<RecapRankingPosterDto> TopPostersByTime(List<UserAnimeEntry> group) =>
        group
            .Where(e => e.EpisodesWatched > 0)
            .OrderByDescending(e => e.EpisodesWatched)
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Take(GroupRanking.PosterCount)
            .Select(e => new RecapRankingPosterDto(e.AnimeId, e.Anime.Title, e.Anime.PictureUrl))
            .ToList();
}
