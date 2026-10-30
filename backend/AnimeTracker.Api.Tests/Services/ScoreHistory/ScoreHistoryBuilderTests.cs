using AnimeTracker.Api.Services.ScoreHistory;

namespace AnimeTracker.Api.Tests.Services.ScoreHistory;

// add-score-history-graph design D6/D7: ScoreHistoryBuilder is pure, so each
// case below is a plain unit test of records in, points and markers out.
public class ScoreHistoryBuilderTests
{
    private static readonly DateOnly D0 = new(2026, 10, 30);

    // The app's local day, here simply UTC's, so a record's instant names its date.
    private static DateOnly Utc(DateTimeOffset instant) => DateOnly.FromDateTime(instant.UtcDateTime);

    private static ScoreRecordRow At(DateOnly date, double score, int hour = 12) =>
        new(new DateTimeOffset(date.ToDateTime(new TimeOnly(hour, 0)), TimeSpan.Zero), score);

    private static List<ScoreHistoryPointDto> Points(params ScoreRecordRow[] records) =>
        ScoreHistoryBuilder.BuildPoints(records, Utc);

    private static ScoreHistoryPointDto P(DateOnly date, double score) => new(date, score);

    // ---- an anime's points ----

    [Fact]
    public void TwoRecordsOnOneDateMakeOnePointAtTheLastOfThem()
    {
        var points = Points(At(D0, 8.01, hour: 9), At(D0, 8.03, hour: 18));

        Assert.Equal([P(D0, 8.03)], points);
    }

    [Fact]
    public void TheLastRecordOfADateWinsWhateverOrderTheyArriveIn()
    {
        var points = Points(At(D0, 8.03, hour: 18), At(D0, 8.01, hour: 9));

        Assert.Equal([P(D0, 8.03)], points);
    }

    [Fact]
    public void ARepeatOfThePointBeforeItIsDropped()
    {
        var points = Points(
            At(D0, 8.01),
            At(D0.AddDays(1), 8.02, hour: 9), At(D0.AddDays(1), 8.01, hour: 18),
            At(D0.AddDays(2), 8.03));

        Assert.Equal([P(D0, 8.01), P(D0.AddDays(2), 8.03)], points);
    }

    [Fact]
    public void AMoveThatReturnsWithinADayIsNoChange()
    {
        var points = Points(At(D0, 8.01), At(D0.AddDays(1), 8.02, hour: 9), At(D0.AddDays(1), 8.01, hour: 18));

        Assert.Equal([P(D0, 8.01)], points);
        Assert.False(ScoreHistoryBuilder.HasChanged(points));
    }

    [Fact]
    public void OneRecordIsOnePointAndNoChange()
    {
        var points = Points(At(D0, 8.31));

        Assert.Single(points);
        Assert.False(ScoreHistoryBuilder.HasChanged(points));
    }

    [Fact]
    public void NoRecordsAreNoPointsAndNoChange()
    {
        var points = Points();

        Assert.Empty(points);
        Assert.False(ScoreHistoryBuilder.HasChanged(points));
    }

    [Fact]
    public void TwoDifferentScoresAreAChange()
    {
        Assert.True(ScoreHistoryBuilder.HasChanged(Points(At(D0, 8.31), At(D0.AddDays(3), 8.33))));
    }

    [Fact]
    public void AScoreThatLeavesAndComesBackAcrossDatesIsAChange()
    {
        var points = Points(At(D0, 8.0), At(D0.AddDays(1), 8.1), At(D0.AddDays(2), 8.0));

        Assert.Equal(3, points.Count); // the return is not a repeat of the point before it
        Assert.True(ScoreHistoryBuilder.HasChanged(points));
    }

    [Fact]
    public void ADateIsTheLocalOneNotTheInstantsOwn()
    {
        // 22:30 UTC on the 30th is already the 31st three hours east.
        var record = new ScoreRecordRow(new DateTimeOffset(2026, 10, 30, 22, 30, 0, TimeSpan.Zero), 8.2);

        var points = ScoreHistoryBuilder.BuildPoints([record], instant => DateOnly.FromDateTime(instant.UtcDateTime.AddHours(3)));

        Assert.Equal([P(new DateOnly(2026, 10, 31), 8.2)], points);
    }

    // ---- a series' line ----

    private static ScoreHistoryEntry Entry(
        int id, DateOnly? airedFrom, bool hasAired, params ScoreHistoryPointDto[] points) =>
        new(id, $"Anime {id}", id % 2 == 0 ? $"English {id}" : null, $"https://img/{id}.jpg", airedFrom, hasAired, points);

    [Fact]
    public void ANewSeasonMovesTheLineOnItsFirstEpisodeDateNotOnItsFirstRecord()
    {
        // The spec's worked example: S1 8.10 and S2 7.90 are recorded on 30.10, so the line reads 8.00.
        // S3's first episode airs on 4 January and its first score, 7.40, is detected on 7 January.
        var jan4 = new DateOnly(2027, 1, 4);
        var jan7 = new DateOnly(2027, 1, 7);
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.10)),
            Entry(2, new DateOnly(2022, 4, 1), true, P(D0, 7.90)),
            Entry(3, jan4, true, P(jan7, 7.40)),
        ]);

        Assert.Equal([P(D0, 8.00), P(jan4, 7.80)], line.Points); // 7 January repeats 7.80 and is dropped
        var marker = Assert.Single(line.Markers);
        Assert.Equal((3, jan4), (marker.AnimeId, marker.Date));
    }

    [Fact]
    public void ALateFirstRecordIsNoChange()
    {
        // Two entries aired years ago; the second's first record comes a week after the first's.
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0)),
            Entry(2, new DateOnly(2021, 4, 1), true, P(D0.AddDays(7), 7.0)),
        ]);

        Assert.Equal([P(D0, 7.5)], line.Points);
        Assert.False(ScoreHistoryBuilder.HasChanged(line.Points));
    }

    [Fact]
    public void AFreshInstallsStaggeredFirstRecordsMakeOnePoint()
    {
        // Each main-line entry's first score is recorded at a different time during setup,
        // some of it past midnight, and no score has moved since.
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2019, 1, 1), true, P(D0, 8.4)),
            Entry(2, new DateOnly(2020, 1, 1), true, P(D0, 7.9)),
            Entry(3, new DateOnly(2021, 1, 1), true, P(D0.AddDays(1), 8.1)),
            Entry(4, new DateOnly(2022, 1, 1), true, P(D0.AddDays(1), 8.6)),
        ]);

        Assert.Single(line.Points);
        Assert.False(ScoreHistoryBuilder.HasChanged(line.Points));
        Assert.Empty(line.Markers);
    }

    [Fact]
    public void AnEntryWithNoFirstEpisodeDateJoinsOnItsFirstPoint()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0)),
            Entry(2, airedFrom: null, hasAired: true, P(D0.AddDays(5), 6.0)),
        ]);

        Assert.Equal([P(D0, 8.0), P(D0.AddDays(5), 7.0)], line.Points);
    }

    [Fact]
    public void AFirstRecordEarlierThanTheFirstEpisodeDateJoinsOnTheRecord()
    {
        // MyAnimeList's start date for the entry sits later than the day its first score was recorded.
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0)),
            Entry(2, D0.AddDays(10), true, P(D0.AddDays(5), 6.0)),
        ]);

        Assert.Equal([P(D0, 8.0), P(D0.AddDays(5), 7.0)], line.Points);
    }

    [Fact]
    public void AnEntryWithNoPointsCountsNowhere()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0), P(D0.AddDays(3), 8.2)),
            Entry(2, new DateOnly(2021, 4, 1), hasAired: false), // announced with a score on MyAnimeList, but nothing recorded
        ]);

        Assert.Equal([P(D0, 8.0), P(D0.AddDays(3), 8.2)], line.Points);
    }

    [Fact]
    public void AMainLineWithNoPointsAtAllHasNoLine()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Entry(1, new DateOnly(2020, 4, 1), true), Entry(2, null, false)]);

        Assert.Empty(line.Points);
        Assert.Empty(line.Markers);
        Assert.False(ScoreHistoryBuilder.HasChanged(line.Points));
    }

    [Fact]
    public void ATieRoundsAwayFromZeroAndRepeatsAreDroppedAfterRounding()
    {
        // 8.25 and 8.00 average exactly 8.125, which a banker's round would print as 8.12.
        // The raw means then differ (8.125, 8.13) but both read 8.13, so the second is a repeat.
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.25)),
            Entry(2, new DateOnly(2021, 4, 1), true, P(D0, 8.00), P(D0.AddDays(1), 8.01)),
        ]);

        Assert.Equal([P(D0, 8.13)], line.Points);
    }

    [Fact]
    public void TheFigureIsTheSeriesPagesOwnMeanRoundedToTwoDecimals()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.31)),
            Entry(2, new DateOnly(2021, 4, 1), true, P(D0, 7.62)),
            Entry(3, new DateOnly(2022, 4, 1), true, P(D0, 7.99)),
        ]);

        Assert.Equal([P(D0, 7.97)], line.Points); // (8.31 + 7.62 + 7.99) / 3 = 7.9733...
    }

    [Fact]
    public void AnEntryKeepsItsLatestScoreUntilItMovesAgain()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0), P(D0.AddDays(10), 9.0)),
            Entry(2, new DateOnly(2021, 4, 1), true, P(D0.AddDays(4), 6.0)),
        ]);

        // 30.10: 8.0 and 2's carried-back 6.0 = 7.0; its own point on day 4 repeats it; day 10: 9.0 and 6.0.
        Assert.Equal([P(D0, 7.0), P(D0.AddDays(10), 7.5)], line.Points);
    }

    // ---- markers ----

    private static readonly DateOnly MarkedDay = D0.AddDays(20);

    private static ScoreHistoryEntry Anchor() =>
        Entry(1, new DateOnly(2020, 4, 1), true, P(D0, 8.0), P(D0.AddDays(40), 8.2));

    [Fact]
    public void AnEntryThatAiredInsideTheSpanIsMarkedOnAPointOfTheLine()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, MarkedDay, hasAired: true, P(MarkedDay.AddDays(2), 7.0))]);

        var marker = Assert.Single(line.Markers);
        Assert.Equal((2, "Anime 2", "English 2", "https://img/2.jpg", MarkedDay), (marker.AnimeId, marker.Title, marker.EnglishTitle, marker.PictureUrl, marker.Date));
        Assert.Contains(line.Points, p => p.Date == MarkedDay);
    }

    [Fact]
    public void AnEntryThatAiredBeforeTheLineStartsIsNotMarked()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, D0.AddDays(-1), hasAired: true, P(D0.AddDays(5), 7.0))]);

        Assert.Empty(line.Markers);
    }

    [Fact]
    public void AnEntryThatAiredOnTheFirstDateOfTheLineIsMarked()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, D0, hasAired: true, P(D0, 7.0))]);

        Assert.Equal(D0, Assert.Single(line.Markers).Date);
    }

    [Fact]
    public void AnEntryThatHasNotAiredIsNotMarkedWhateverDateItIsAnnouncedFor()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, MarkedDay, hasAired: false)]);

        Assert.Empty(line.Markers);
        Assert.DoesNotContain(line.Points, p => p.Date == MarkedDay);
    }

    [Fact]
    public void AnEntryThatAiresAfterTheLastPointIsNotMarked()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, D0.AddDays(60), hasAired: true)]);

        Assert.Empty(line.Markers);
        Assert.Equal(D0.AddDays(40), line.Points[^1].Date); // a marker never stretches the line
    }

    [Fact]
    public void AnAiredEntryWithNoScoreYetIsMarkedAndKeepsItsRepeatedPoint()
    {
        var line = ScoreHistoryBuilder.BuildSeries([Anchor(), Entry(2, MarkedDay, hasAired: true)]);

        Assert.Equal(MarkedDay, Assert.Single(line.Markers).Date);
        Assert.Equal([P(D0, 8.0), P(MarkedDay, 8.0), P(D0.AddDays(40), 8.2)], line.Points);
    }

    [Fact]
    public void TwoEntriesAiredOnOneDateAreBothMarkedOnOnePoint()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Anchor(),
            Entry(2, MarkedDay, hasAired: true),
            Entry(3, MarkedDay, hasAired: true),
        ]);

        Assert.Equal([2, 3], line.Markers.Select(m => m.AnimeId));
        Assert.Single(line.Points, p => p.Date == MarkedDay);
    }

    [Fact]
    public void MarkersComeOldestFirst()
    {
        var line = ScoreHistoryBuilder.BuildSeries([
            Anchor(),
            Entry(3, MarkedDay.AddDays(7), hasAired: true),
            Entry(2, MarkedDay, hasAired: true),
        ]);

        Assert.Equal([2, 3], line.Markers.Select(m => m.AnimeId));
    }
}
