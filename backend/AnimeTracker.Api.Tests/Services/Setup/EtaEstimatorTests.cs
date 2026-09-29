using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 10.2 (design D17): a step's estimate comes from a ring of its last 30
// completion events. There is none until 5 items and 10 seconds of progress; the pace counts items,
// not events, so a page, a batch or a franchise build that finishes many at once is not read as one.
public class EtaEstimatorTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset At(double seconds) => Start.AddSeconds(seconds);

    [Fact]
    public void ThereIsNoEstimateBeforeAnythingHasFinished()
    {
        Assert.Null(new EtaEstimator().EtaSeconds(remaining: 100));
    }

    [Fact]
    public void OneEventAloneHasNoPaceToReadEvenWhenItFinishedManyItems()
    {
        var estimator = new EtaEstimator();
        estimator.Record(100, At(0));

        Assert.Null(estimator.EtaSeconds(remaining: 500));
    }

    [Fact]
    public void ThereIsNoEstimateUnderFiveCompletions()
    {
        var estimator = new EtaEstimator();
        foreach (var second in new[] { 0, 20, 40, 60 })
            estimator.Record(1, At(second)); // four completions over a minute

        Assert.Null(estimator.EtaSeconds(remaining: 10));
    }

    [Fact]
    public void ThereIsNoEstimateUnderTenSeconds()
    {
        var estimator = new EtaEstimator();
        foreach (var second in new[] { 0, 2, 4, 6, 8 })
            estimator.Record(1, At(second)); // five completions, but only eight seconds between the first and last

        Assert.Null(estimator.EtaSeconds(remaining: 10));

        estimator.Record(1, At(9.9));
        Assert.Null(estimator.EtaSeconds(remaining: 10)); // still just under ten
    }

    [Fact]
    public void FiveCompletionsOverTenSecondsGiveTheFirstEstimate()
    {
        var estimator = new EtaEstimator();
        foreach (var second in new[] { 0, 2.5, 5, 7.5, 10 })
            estimator.Record(1, At(second));

        // The first event is the starting line: four items in ten seconds, 0.4 a second.
        Assert.Equal(50, estimator.EtaSeconds(remaining: 20));
    }

    [Fact]
    public void TheOldestEventIsTheStartingLineNotPartOfThePace()
    {
        var estimator = new EtaEstimator();
        estimator.Record(100, At(0));  // finished before the clock started
        estimator.Record(30, At(60));  // 30 items in the minute since

        Assert.Equal(200, estimator.EtaSeconds(remaining: 100)); // 0.5 a second, not 130 in 60
    }

    [Fact]
    public void ABatchCountsItsItemsNotOneEvent()
    {
        var estimator = new EtaEstimator();
        estimator.Record(25, At(0));
        estimator.Record(25, At(30)); // an airing batch every half minute

        Assert.Equal(60, estimator.EtaSeconds(remaining: 50)); // 25 items in 30 s: 50 more take a minute
    }

    [Fact]
    public void OnlyTheLastThirtyEventsSetThePace()
    {
        var estimator = new EtaEstimator();
        for (var i = 0; i < 10; i++)
            estimator.Record(1, At(i * 100));            // a slow start: one every 100 s
        for (var i = 0; i < 30; i++)
            estimator.Record(1, At(1000 + i));            // then one a second

        // Only the thirty fast ones are in the ring: 29 items in 29 s.
        Assert.Equal(60, estimator.EtaSeconds(remaining: 60));
    }

    [Fact]
    public void NothingFinishedRecordsNothingAndDoesNotStretchTheSpan()
    {
        var estimator = new EtaEstimator();
        estimator.Record(3, At(0));
        estimator.Record(3, At(10));
        estimator.Record(0, At(100));
        estimator.Record(-4, At(200)); // a queue whose count grew

        Assert.Equal(100, estimator.EtaSeconds(remaining: 30)); // 3 items in 10 s
    }

    [Fact]
    public void ClearForgetsThePace()
    {
        var estimator = new EtaEstimator();
        estimator.Record(5, At(0));
        estimator.Record(5, At(20));
        Assert.NotNull(estimator.EtaSeconds(remaining: 10));

        estimator.Clear();

        Assert.Null(estimator.EtaSeconds(remaining: 10));
    }

    [Fact]
    public void NothingLeftMeansNoTimeLeft()
    {
        var estimator = new EtaEstimator();
        estimator.Record(5, At(0));
        estimator.Record(5, At(20));

        Assert.Equal(0, estimator.EtaSeconds(remaining: 0));
        Assert.Equal(0, estimator.EtaSeconds(remaining: -3));
    }

    [Fact]
    public void EventsAllAtOneInstantHaveNoPace()
    {
        var estimator = new EtaEstimator();
        estimator.Record(10, At(5));
        estimator.Record(10, At(5));

        Assert.Null(estimator.EtaSeconds(remaining: 10));
    }

    [Fact]
    public void AClockThatWentBackwardsGivesNoEstimateRatherThanANegativeOne()
    {
        var estimator = new EtaEstimator();
        estimator.Record(10, At(60));
        estimator.Record(10, At(0));

        Assert.Null(estimator.EtaSeconds(remaining: 10));
    }
}
