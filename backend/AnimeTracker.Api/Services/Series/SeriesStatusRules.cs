namespace AnimeTracker.Api.Services.Series;

/// <summary>The series status pill's four-step precedence (design.md D5 of
/// add-series-browser), extracted out of <see
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

        var anyFinished = allAiringStatuses.Any(s => s == "finished_airing");
        var anyUpcoming = allAiringStatuses.Any(s => s == "not_yet_aired");

        if (!anyFinished && anyUpcoming)
            return "Upcoming";

        return anyUpcoming ? "Ongoing" : "Finished";
    }
}
