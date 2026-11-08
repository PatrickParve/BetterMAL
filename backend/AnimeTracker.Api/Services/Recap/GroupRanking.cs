using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The rules for ranking a group of anime (a season, a year, a genre, a
/// studio) that every such ranking shares: the Bayesian weighted score, the
/// score-by-score histogram its tie-break reads, the comparison that orders two
/// groups, and the posters that illustrate a group in my ranking's order.
/// Lifted out of <see cref="RecapRankingBuilder"/> so the taste stats rank genres
/// and studios on the very same arithmetic rather than a copy of it (taste-stats
/// design.md D1); the season and year rankings still call it, so their output is
/// unchanged.</summary>
internal static class GroupRanking
{
    /// <summary>How many posters illustrate a group.</summary>
    public const int PosterCount = 3;

    // A group's histogram of my scores, indexed 1-10 (index 0 unused) — the
    // score-by-score tie-break step shared by every ranking (list-recaps design.md
    // decision 6, step 3).
    public static int[] BuildHistogram(List<UserAnimeEntry> scored)
    {
        var histogram = new int[11];
        foreach (var entry in scored)
        {
            histogram[entry.MyScore!.Value]++;
        }
        return histogram;
    }

    // W = (v / (v + m)) * R + (m / (v + m)) * C
    public static double WeightedAverage(List<UserAnimeEntry> scored, double globalMean, int trustThreshold)
    {
        var v = scored.Count;
        var r = scored.Average(e => e.MyScore!.Value);
        return (double)v / (v + trustThreshold) * r + (double)trustThreshold / (v + trustThreshold) * globalMean;
    }

    // Ranks two groups in the order list-recaps design.md decision 6 sets out:
    // full-precision weighted score, scored count, a score-by-score histogram
    // comparison from 10 down to 1, then whatever last key the caller supplies
    // (a season or year ranks newest first, a genre or studio by name) — each
    // compared only once everything before it has tied. Negative means "a" ranks
    // ahead of "b". Weighted scores are compared with exact equality and no
    // epsilon: R and C are each an exact integer sum divided by a count, and
    // W applies the same arithmetic in the same order to both groups, so
    // equal inputs are bit-identical with no accumulation-order drift to
    // guard against. An epsilon would let a tie-break step overrule a real
    // difference in score, which step 1 exists to rule out. The last key is a
    // function so it is evaluated only when it is reached.
    public static int CompareGroups(
        double weightedA, int scoredCountA, int[] histogramA,
        double weightedB, int scoredCountB, int[] histogramB,
        Func<int> thenBy)
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

        return thenBy();
    }

    // design.md D1: ordering by rank alone already gives score-descending
    // order, because AnimeRankingKey compares score before anything else —
    // so the old OrderByDescending(MyScore) that opened this comparator is
    // gone, and grouping by score first (below) only splits that same order
    // into buckets. What the old ThenBy(Title) also did — settling which of
    // two anime I scored the same wins a poster slot — is now settled by
    // where I actually placed them, the app's one answer to that question
    // everywhere else it's asked. An anime the ranking does not cover (a
    // scored Plan-to-watch or not-yet-aired anime, which RankBandResolver
    // leaves outside the ranking) sorts after every ranked anime of its
    // score — RankOf returns null for it, and OrderBy's default nullable
    // comparer treats null as *less than* every rank, which is nulls-first,
    // the wrong way round; NullsLast substitutes int.MaxValue for a null
    // rank so it sorts after every real one — then by title, then by id,
    // the same nulls-last convention RecapPage.compareByRankThenTitle
    // applies on the client.
    public static List<RecapRankingScorePostersDto> PostersByScore(
        List<UserAnimeEntry> scored, AnimeRankingSnapshot rankingSnapshot) =>
        scored
            .GroupBy(e => e.MyScore!.Value)
            .OrderByDescending(g => g.Key)
            .Select(g => new RecapRankingScorePostersDto(g.Key, TopPosters(g, rankingSnapshot)))
            .ToList();

    /// <summary>A group's best <see cref="PosterCount"/> anime in my ranking's
    /// order, best-ranked first, scored or not: the order <see
    /// cref="PostersByScore"/> uses inside one score, applied to the whole group.
    /// An anime my ranking does not place follows every one it does, by title.</summary>
    public static List<RecapRankingPosterDto> TopPosters(
        IEnumerable<UserAnimeEntry> group, AnimeRankingSnapshot rankingSnapshot) =>
        group
            .OrderBy(e => RankKey(rankingSnapshot, e.AnimeId))
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.AnimeId)
            .Take(PosterCount)
            .Select(ToPoster)
            .ToList();

    public static RecapRankingPosterDto ToPoster(UserAnimeEntry entry) =>
        new(entry.AnimeId, entry.Anime.Title, entry.Anime.PictureUrl);

    /// <summary>An anime's place in my ranking as a sort key, ascending: its rank
    /// where the ranking places it, after every ranked anime where it does not.</summary>
    public static int RankKey(AnimeRankingSnapshot rankingSnapshot, int animeId) =>
        rankingSnapshot.RankOf(animeId) ?? int.MaxValue;
}
