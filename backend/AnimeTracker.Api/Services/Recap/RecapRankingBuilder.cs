using AnimeTracker.Api.Models;
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

    private const int PosterCount = 3;

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
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean)
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
                Weighted: WeightedAverage(c.Scored, globalMean, SeasonTrustThreshold),
                Histogram: BuildHistogram(c.Scored),
                Recency: SeasonCalendar.GetSeasonPointIndex(c.Year, c.Season)))
            .ToList();

        ranked.Sort((a, b) => CompareGroups(
            a.Weighted, a.Scored.Count, a.Histogram, a.Recency,
            b.Weighted, b.Scored.Count, b.Histogram, b.Recency));

        return ranked
            .Select(c => new RecapSeasonRankingDto(
                c.Year, c.Season, c.Scored.Count, Math.Round(c.Weighted, 2), TopPosters(c.Scored)))
            .ToList();
    }

    public static List<RecapYearRankingDto> BuildYearRanking(
        List<UserAnimeEntry> airedIncluded, RecapPeriod period, double globalMean)
    {
        var byYear = airedIncluded.ToLookup(e => e.Anime.AiredFrom!.Value.Year);

        var ranked = period.Years
            .Select(year => (Year: year, Scored: byYear[year].Where(e => e.MyScore is not null).ToList()))
            .Where(c => c.Scored.Count > 0)
            .Select(c => (
                c.Year, c.Scored,
                Weighted: WeightedAverage(c.Scored, globalMean, YearTrustThreshold),
                Histogram: BuildHistogram(c.Scored),
                Recency: c.Year))
            .ToList();

        ranked.Sort((a, b) => CompareGroups(
            a.Weighted, a.Scored.Count, a.Histogram, a.Recency,
            b.Weighted, b.Scored.Count, b.Histogram, b.Recency));

        return ranked
            .Select(c => new RecapYearRankingDto(
                c.Year, c.Scored.Count, Math.Round(c.Weighted, 2), TopPosters(c.Scored)))
            .ToList();
    }

    // A group's histogram of my scores, indexed 1-10 (index 0 unused) — the
    // score-by-score tie-break step shared by both rankings below (design.md
    // decision 6, step 3).
    private static int[] BuildHistogram(List<UserAnimeEntry> scored)
    {
        var histogram = new int[11];
        foreach (var entry in scored)
        {
            histogram[entry.MyScore!.Value]++;
        }
        return histogram;
    }

    // Ranks two groups (a season or a year) in the order design.md decision 6
    // sets out: full-precision weighted score, scored count, a score-by-score
    // histogram comparison from 10 down to 1, then recency — each compared
    // only once everything before it has tied. Negative means "a" ranks
    // ahead of "b". Weighted scores are compared with exact equality and no
    // epsilon: R and C are each an exact integer sum divided by a count, and
    // W applies the same arithmetic in the same order to both groups, so
    // equal inputs are bit-identical with no accumulation-order drift to
    // guard against. An epsilon would let a tie-break step overrule a real
    // difference in score, which step 1 exists to rule out.
    private static int CompareGroups(
        double weightedA, int scoredCountA, int[] histogramA, int recencyA,
        double weightedB, int scoredCountB, int[] histogramB, int recencyB)
    {
        if (weightedA != weightedB)
        {
            return weightedA > weightedB ? -1 : 1;
        }

        if (scoredCountA != scoredCountB)
        {
            return scoredCountA > scoredCountB ? -1 : 1;
        }

        for (var score = 10; score >= 1; score--)
        {
            if (histogramA[score] != histogramB[score])
            {
                return histogramA[score] > histogramB[score] ? -1 : 1;
            }
        }

        // Newest first (design.md decision 6, step 4) — reverses the
        // oldest-first fallback this replaced.
        return recencyA > recencyB ? -1 : recencyA < recencyB ? 1 : 0;
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
            .Take(PosterCount)
            .Select(e => new RecapRankingPosterDto(e.AnimeId, e.Anime.Title, e.Anime.PictureUrl))
            .ToList();
}
