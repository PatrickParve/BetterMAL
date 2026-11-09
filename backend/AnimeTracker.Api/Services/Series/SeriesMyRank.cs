using System.Globalization;

namespace AnimeTracker.Api.Services.Series;

/// <summary>My series ranking (add-my-rank-badges design.md D4; series-browser
/// "The My average order is my series ranking"): the Series page's My average
/// sort, applied to every series that page lists with none of its filters, and
/// each series' 1-based place in it. A C# copy of the client's
/// <c>compareMyScoreChain</c> + <c>sortSeries</c> in <c>frontend/src/utils/anime.ts</c>,
/// step for step — the two must be changed together, and
/// <c>SeriesMyRankTests</c> pins each step. The profile's Top series order is a
/// different chain over a different set and is not this.</summary>
public static class SeriesMyRank
{
    /// <summary>My main-line average, highest first → average position in my
    /// rankings, nearest the top first → main-line episodes aired, most first →
    /// display title. Each of the first two puts a series with no figure last
    /// (the client's <c>seriesNullsLast</c>), whichever way the step runs. The
    /// title is the English title when there is one, else the series title,
    /// lowercased, compared in the invariant culture — the client's
    /// <c>pickDisplayTitle(...).toLowerCase()</c> and <c>localeCompare</c>.
    /// The last step, series id, exists only so two series the client could not
    /// tell apart still hold two different ranks; it is never reached with real
    /// data, and the client's order there is whatever order the list arrived
    /// in.</summary>
    public static IComparer<SeriesListItemDto> MyAverageOrder { get; } = Comparer<SeriesListItemDto>.Create(Compare);

    /// <summary>The series' rank — its 1-based place in <see cref="MyAverageOrder"/>
    /// over the series that have a my-average — and how many series hold one.
    /// A series with no my-average holds no rank and is not counted, and neither
    /// is one missing from <paramref name="listed"/>: <c>Rank</c> is null for
    /// both. Ranks run 1..N with no gaps.</summary>
    public static (int? Rank, int RankedCount) Of(int seriesId, IEnumerable<SeriesListItemDto> listed)
    {
        var ranked = listed.Where(item => item.MineMain.Value is not null).Order(MyAverageOrder).ToList();
        var index = ranked.FindIndex(item => item.SeriesId == seriesId);
        return (index < 0 ? null : index + 1, ranked.Count);
    }

    private static int Compare(SeriesListItemDto a, SeriesListItemDto b)
    {
        var byAverage = NullsLast(a.MineMain.Value, b.MineMain.Value, descending: true);
        if (byAverage != 0) return byAverage;

        var byAverageRank = NullsLast(a.MainLineAverageRank, b.MainLineAverageRank, descending: false);
        if (byAverageRank != 0) return byAverageRank;

        var byAired = NullsLast<int>(a.MainLineAiredEpisodes, b.MainLineAiredEpisodes, descending: true);
        if (byAired != 0) return byAired;

        var byTitle = string.Compare(DisplayTitleKey(a), DisplayTitleKey(b), CultureInfo.InvariantCulture, CompareOptions.None);
        if (byTitle != 0) return byTitle;

        return a.SeriesId.CompareTo(b.SeriesId);
    }

    private static int NullsLast<T>(T? a, T? b, bool descending) where T : struct, IComparable<T>
    {
        if (a is null && b is null) return 0;
        if (a is null) return 1;
        if (b is null) return -1;
        return descending ? b.Value.CompareTo(a.Value) : a.Value.CompareTo(b.Value);
    }

    private static string DisplayTitleKey(SeriesListItemDto item) =>
        (string.IsNullOrWhiteSpace(item.EnglishTitle) ? item.Title : item.EnglishTitle).ToLowerInvariant();
}
