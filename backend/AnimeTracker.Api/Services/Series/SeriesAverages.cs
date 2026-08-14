using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Shared "mean of scores over a group of members" computation
/// (design.md decision 1), used by both SeriesService (the series page's four
/// averages) and SeriesRankingLookup (Top series' main-line-only pair) so the
/// two surfaces can never disagree. Takes raw scores rather than
/// SeriesMembers so a lightweight projection (no navigation properties) can
/// use it too.</summary>
public static class SeriesAverages
{
    public static SeriesAverageDto Mal(IEnumerable<double?> malScores)
    {
        var all = malScores.ToList();
        var scored = all.Where(s => s is not null).Select(s => s!.Value).ToList();
        return new SeriesAverageDto(scored.Count > 0 ? scored.Average() : null, scored.Count, all.Count);
    }

    // MAL's score of 0 means "unscored", not a rating (design.md decision 8).
    public static SeriesAverageDto Mine(IEnumerable<int?> myScores)
    {
        var all = myScores.ToList();
        var scored = all.Where(s => s is > 0).Select(s => (double)s!.Value).ToList();
        return new SeriesAverageDto(scored.Count > 0 ? scored.Average() : null, scored.Count, all.Count);
    }

    // Every finished-airing member is Completed in my list, and at least one
    // such member exists — entries not yet aired or still airing don't count
    // against it, since they can't be completed yet (design.md decision 7).
    public static bool MainLineCompletedByMe(IEnumerable<(string? AiringStatus, WatchStatus? EntryStatus)> mainLine)
    {
        var finishedAiring = mainLine.Where(m => m.AiringStatus == "finished_airing").ToList();
        return finishedAiring.Count > 0 && finishedAiring.All(m => m.EntryStatus == WatchStatus.Completed);
    }
}
