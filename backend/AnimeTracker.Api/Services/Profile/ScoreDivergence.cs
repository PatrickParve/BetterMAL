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

    public readonly record struct Context(double MyMean, double MalMean, double MySd, double MalSd)
    {
        // > 0 means MAL sits further above its mean than I sit above mine —
        // a "they liked it more than I did" direction; < 0 is the reverse.
        public double DivergenceOf(UserAnimeEntry entry) =>
            (entry.Anime.MalScore!.Value - MalMean) / MalSd - (entry.MyScore!.Value - MyMean) / MySd;
    }

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
