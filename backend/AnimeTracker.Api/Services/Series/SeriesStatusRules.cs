namespace AnimeTracker.Api.Services.Series;

/// <summary>The series status pill's three-step precedence (design.md D5 of
/// trim-dead-controls-and-widen-updates), extracted out of <see
/// cref="SeriesService.ComputeStatus"/> so a lightweight projection with no
/// navigation properties — <see cref="SeriesRankingIndex.ListedSeries"/> —
/// can compute the same pill without loading entities. Takes airing-status
/// strings directly rather than <c>AnimeMetadata</c>.</summary>
public static class SeriesStatusRules
{
    public static string Compute(IEnumerable<string?> mainLineAiringStatuses, IEnumerable<string?> allAiringStatuses)
    {
        if (mainLineAiringStatuses.Any(s => s == "currently_airing"))
            return "Airing";

        if (allAiringStatuses.Any(s => s == "currently_airing"))
            return "Ongoing";

        var anyUpcoming = allAiringStatuses.Any(s => s == "not_yet_aired");

        return anyUpcoming ? "Ongoing" : "Finished";
    }
}
