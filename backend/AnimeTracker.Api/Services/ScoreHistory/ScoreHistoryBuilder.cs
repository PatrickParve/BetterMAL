using AnimeTracker.Api.Services.Series;

namespace AnimeTracker.Api.Services.ScoreHistory;

/// <summary>One stored score record, reduced to what the builder reads.</summary>
public readonly record struct ScoreRecordRow(DateTimeOffset DetectedAt, double Score);

/// <summary>One main-line entry as the series line sees it: who it is, when its
/// first episode aired, whether that has been confirmed, and its own points
/// (<see cref="ScoreHistoryBuilder.BuildPoints"/>).</summary>
public record ScoreHistoryEntry(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    DateOnly? AiredFrom,
    bool HasAired,
    IReadOnlyList<ScoreHistoryPointDto> Points);

/// <summary>Turns stored score records into the points an overlay draws, and a
/// series' main-line entries into its line (add-score-history-graph design.md
/// D6, D7). Pure — no database, no clock — so every case is a plain unit test,
/// and the has-changed answer a page read gives is the very one the overlay
/// then draws, rather than a separate <c>COUNT(DISTINCT)</c> that could
/// disagree with it when a score moves and returns within a day.</summary>
public static class ScoreHistoryBuilder
{
    /// <summary>The points of one anime: one per local calendar date, the last
    /// record of a date winning, and a point that repeats the one before it
    /// dropped. <paramref name="localDate"/> is the app's local day
    /// (<c>IBroadcastLocalTimeConverter.GetLocalDate</c>), so the dates match
    /// the rest of the app; a date-only value also means <c>dd.mm</c> could not
    /// tell two detections on one date apart anyway.</summary>
    public static List<ScoreHistoryPointDto> BuildPoints(
        IEnumerable<ScoreRecordRow> records, Func<DateTimeOffset, DateOnly> localDate)
    {
        var points = new List<ScoreHistoryPointDto>();
        var lastPerDate = records
            .OrderBy(r => r.DetectedAt)
            .GroupBy(r => localDate(r.DetectedAt))
            .Select(g => new ScoreHistoryPointDto(g.Key, g.Last().Score))
            .OrderBy(p => p.Date);

        foreach (var point in lastPerDate)
        {
            if (points.Count == 0 || points[^1].Score != point.Score)
                points.Add(point);
        }
        return points;
    }

    /// <summary>Whether the points hold at least two different scores. One
    /// record, or a score that moved and came back within a day, is no
    /// change.</summary>
    public static bool HasChanged(IReadOnlyList<ScoreHistoryPointDto> points) =>
        points.Select(p => p.Score).Distinct().Skip(1).Any();

    /// <summary>A series' line: the unweighted mean of its main-line entries'
    /// scores over time, with a marker for each entry that first aired while
    /// the line was recorded.
    ///
    /// <list type="bullet">
    /// <item><description><b>start</b> is the earliest first point of any
    /// entry. An entry with no points never counts.</description></item>
    /// <item><description>An entry <b>joins</b> on the earlier of its
    /// first-episode date and its first point, or on its first point when the
    /// date is unknown. One that had aired by the start counts from the
    /// start.</description></item>
    /// <item><description>On any date an entry's score is its latest point at
    /// or before that date or, before its first point, that first point. That
    /// carry-back is what keeps a late first record from reading as a change,
    /// and moves the line on a new season's first-episode date rather than the
    /// day its first score happened to be detected.</description></item>
    /// <item><description>The line has a point at the start, on each join after
    /// it, on every entry's later points and on each marked date, each the mean
    /// of the joined entries through <see cref="SeriesAverages.Mal"/> — the
    /// figure the series page's chip shows — rounded to two decimals, away from
    /// zero. A point repeating the one before it is dropped, unless its date is
    /// marked (a marker must sit on a real point).</description></item>
    /// </list></summary>
    public static ScoreHistoryDto BuildSeries(IReadOnlyList<ScoreHistoryEntry> mainLine)
    {
        var counted = mainLine
            .Where(e => e.Points.Count > 0)
            .Select(e => new CountedEntry(e))
            .ToList();
        if (counted.Count == 0)
            return new ScoreHistoryDto([], []);

        var start = counted.Min(c => c.FirstDate);

        // Every date the line can move on. The span's end is the last of these,
        // before markers add any: a marker has to fall inside the line, not
        // stretch it. An entry that had aired by the start counts from it.
        var events = new SortedSet<DateOnly> { start };
        foreach (var c in counted)
        {
            events.Add(c.JoinsOn < start ? start : c.JoinsOn);
            foreach (var point in c.Points)
                events.Add(point.Date);
        }
        var end = events.Max;

        var markers = mainLine
            .Where(e => e.HasAired && e.AiredFrom is { } aired && aired >= start && aired <= end)
            .Select(e => new ScoreHistoryMarkerDto(e.AnimeId, e.Title, e.EnglishTitle, e.PictureUrl, e.AiredFrom!.Value))
            .OrderBy(m => m.Date)
            .ToList();
        var markedDates = markers.Select(m => m.Date).ToHashSet();

        var points = new List<ScoreHistoryPointDto>();
        foreach (var date in events.Union(markedDates).OrderBy(d => d))
        {
            var joined = counted.Where(c => c.JoinsOn <= date).Select(c => (double?)c.ScoreOn(date));
            if (SeriesAverages.Mal(joined).Value is not { } mean)
                continue; // nobody has joined yet: unreachable from the start on, but a mean of nothing is not a score

            var score = Math.Round(mean, 2, MidpointRounding.AwayFromZero);
            if (points.Count == 0 || points[^1].Score != score || markedDates.Contains(date))
                points.Add(new ScoreHistoryPointDto(date, score));
        }
        return new ScoreHistoryDto(points, markers);
    }

    /// <summary>A main-line entry that has points, so counts toward the line:
    /// when it joins, and what it scored on any date.</summary>
    private sealed class CountedEntry(ScoreHistoryEntry entry)
    {
        public List<ScoreHistoryPointDto> Points { get; } = entry.Points.OrderBy(p => p.Date).ToList();

        public DateOnly FirstDate => Points[0].Date;

        /// <summary>The earlier of the first-episode date and the first point;
        /// the first point alone when the first-episode date is unknown.</summary>
        public DateOnly JoinsOn => entry.AiredFrom is { } aired && aired < FirstDate ? aired : FirstDate;

        /// <summary>The latest point at or before <paramref name="date"/> or,
        /// before the first point, that first point.</summary>
        public double ScoreOn(DateOnly date) => (Points.LastOrDefault(p => p.Date <= date) ?? Points[0]).Score;
    }
}
