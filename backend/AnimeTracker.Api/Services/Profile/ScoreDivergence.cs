using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Profile;

/// <summary>Normalises my 1-10 scores and MAL's much narrower community
/// averages onto a common scale — standard deviations from each scale's own
/// mean — so the two can be compared and subtracted. Shared by
/// ProfileService's opinion-divergence lists and the recap's hot takes
/// (add-list-recaps design.md decision 4), which apply the same
/// normalisation over different entry subsets but must never disagree about
/// a given anime's divergence value: <see cref="TryCompute"/> is always run
/// over the WHOLE list's rated pairs, never a subset, since a handful of
/// entries can't support their own standard deviation.</summary>
public static class ScoreDivergence
{
    // A mean and standard deviation computed over fewer pairs than this
    // don't describe a spread worth normalizing against — the rule needs a
    // population, not a handful of points.
    public const int MinimumRatedPairs = 10;

    // Opinion divergence gate (design.md decision 1/3 of polish-recap-page,
    // originally add-list-recaps design.md decision 3): both scales are
    // compressed to a common unit — standard deviations from their own mean —
    // before being compared, so the threshold below is symmetric even though
    // MAL's community averages (SD ~0.75 measured) occupy a far narrower band
    // than personal 1-10 scores (SD ~1.44).
    public const double OpinionDivergenceThresholdSd = 1.0;

    // Label-gate boundaries, applied in raw score terms so they match what
    // each list's heading actually claims. 5 and 8 are MAL's own score
    // labels: 5 is "Average" and everything worse sits below it; 8 is "Very
    // Good" and everything better sits above it. That leaves 6 ("Fine") and 7
    // ("Good") as a neutral band between them, deliberately in neither list.
    public const int OpinionDivergenceMyDislikeCeiling = 5;
    public const int OpinionDivergenceMyLikeFloor = 8;

    // Same boundary in both directions: it splits MAL's community scale
    // between its "Good" and "Very Good" labels, so a 7.5+ average reads as a
    // community like and a 7.5-or-below average is not a community dislike.
    public const double OpinionDivergenceMalLikeFloorAndDislikeCeiling = 7.5;

    public readonly record struct Context(double MyMean, double MalMean, double MySd, double MalSd)
    {
        // > 0 means MAL sits further above its mean than I sit above mine —
        // a "they liked it more than I did" direction; < 0 is the reverse.
        public double DivergenceOf(UserAnimeEntry entry) =>
            (entry.Anime.MalScore!.Value - MalMean) / MalSd - (entry.MyScore!.Value - MyMean) / MySd;
    }

    // "They liked it, I didn't": divergence runs at least one SD in MAL's
    // direction, my score sits at or below the dislike ceiling, and MAL's
    // score sits at or above the community-like floor.
    public static bool IsTheyLikedItIDidnt(UserAnimeEntry entry, double divergence) =>
        divergence >= OpinionDivergenceThresholdSd
        && entry.MyScore!.Value <= OpinionDivergenceMyDislikeCeiling
        && entry.Anime.MalScore!.Value >= OpinionDivergenceMalLikeFloorAndDislikeCeiling;

    // "I liked it, they didn't": the mirror image, divergence running at
    // least one SD in my direction.
    public static bool IsILikedItTheyDidnt(UserAnimeEntry entry, double divergence) =>
        -divergence >= OpinionDivergenceThresholdSd
        && entry.MyScore!.Value >= OpinionDivergenceMyLikeFloor
        && entry.Anime.MalScore!.Value <= OpinionDivergenceMalLikeFloorAndDislikeCeiling;

    // The union of both directions — a "hot take" is either kind of
    // disagreement, whichever way it runs.
    public static bool IsOpinionDivergent(UserAnimeEntry entry, double divergence) =>
        IsTheyLikedItIDidnt(entry, divergence) || IsILikedItTheyDidnt(entry, divergence);

    /// <summary>The whole list's rated-pair mean/SD context, or null when
    /// there are fewer than <see cref="MinimumRatedPairs"/> pairs, or either
    /// scale's standard deviation is zero (nothing to normalise against, and
    /// dividing by it would throw).</summary>
    public static Context? TryCompute(List<UserAnimeEntry> wholeList)
    {
        var rated = wholeList.Where(e => e.MyScore is not null && e.Anime.MalScore is not null).ToList();
        if (rated.Count < MinimumRatedPairs)
            return null;

        var myMean = rated.Average(e => e.MyScore!.Value);
        var malMean = rated.Average(e => e.Anime.MalScore!.Value);
        var mySd = PopulationStandardDeviation(rated.Select(e => (double)e.MyScore!.Value), myMean);
        var malSd = PopulationStandardDeviation(rated.Select(e => e.Anime.MalScore!.Value), malMean);
        if (mySd == 0 || malSd == 0)
            return null;

        return new Context(myMean, malMean, mySd, malSd);
    }

    // Population SD, not sample SD: the rated pairs are the whole population
    // being ranked, not a sample standing in for a larger one.
    private static double PopulationStandardDeviation(IEnumerable<double> values, double mean)
    {
        var list = values as IReadOnlyCollection<double> ?? values.ToList();
        var variance = list.Sum(v => (v - mean) * (v - mean)) / list.Count;
        return Math.Sqrt(variance);
    }
}
