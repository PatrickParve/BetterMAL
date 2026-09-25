namespace AnimeTracker.Api.Services.Series;

/// <summary>The year span a series shows — the header on the series page and
/// the card on the Series browser — computed once so the two can never
/// disagree (polish-series-header-and-completion-prompt design.md D1). Takes
/// each entry's air dates directly rather than <c>AnimeMetadata</c>, so
/// <see cref="SeriesService"/> and the navigation-free
/// <see cref="SeriesRankingIndex.ListedSeries"/> projection share it, like
/// <see cref="SeriesStatusRules"/> and <see cref="SeriesAverages"/>.</summary>
public static class SeriesYearSpan
{
    /// <summary>The span of the <b>main line</b>: every <c>IsMainLine</c>
    /// member, every alternative in a version slot included, so the span
    /// describes the franchise's main series and does not move with the picked
    /// route. Extras, related entries and held version neighbours are left out
    /// by the caller, whether they aired before the main series began or after
    /// it ended. There is deliberately no fallback to them (design.md D2): when
    /// no main-line entry has a known start date the result is
    /// <c>(null, null)</c>, and the header shows its no-year placeholder rather
    /// than the extras' years.
    ///
    /// The last year is the latest entry's *end* year (AiredTo), not the start
    /// year of whichever entry started airing most recently — a multi-cour or
    /// still-running entry's AiredFrom.Year understates how far the franchise
    /// actually runs. Falls back to AiredFrom when AiredTo isn't known yet
    /// (currently airing or not yet aired). An entry with no AiredFrom doesn't
    /// count at all.</summary>
    public static (int? First, int? Last) Of(IEnumerable<(DateOnly? AiredFrom, DateOnly? AiredTo)> mainLine)
    {
        var aired = mainLine.Where(a => a.AiredFrom is not null).ToList();
        if (aired.Count == 0) return (null, null);
        var firstYear = aired.Min(a => a.AiredFrom!.Value.Year);
        var lastYear = aired.Max(a => (a.AiredTo ?? a.AiredFrom!.Value).Year);
        return (firstYear, lastYear);
    }
}
