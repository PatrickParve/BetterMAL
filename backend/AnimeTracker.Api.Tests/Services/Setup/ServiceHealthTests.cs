using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// first-run-setup spec, "A service that is down pauses its own queue" and
// "Throttling is shown with its resume time"; design D12. Time is a settable
// instant, so the 1/5/15/60 ladder is checked exactly and without waiting.
public class ServiceHealthTests
{
    private sealed class ManualClock
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public DateTimeOffset Read() => Now;
    }

    private static (ServiceHealth Health, ManualClock Clock) NewHealth()
    {
        var clock = new ManualClock();
        return (new ServiceHealth("Test service", clock.Read), clock);
    }

    private static void Fail(ServiceHealth health, int times)
    {
        for (var i = 0; i < times; i++)
            health.RecordTemporaryFailure();
    }

    [Fact]
    public void AServiceIsNotDownUntilTheThirdFailureInARow()
    {
        var (health, _) = NewHealth();

        Fail(health, 2);
        Assert.False(health.IsDown);
        Assert.Null(health.NextTryAt);

        health.RecordTemporaryFailure();
        Assert.True(health.IsDown);
        Assert.Equal(3, health.ConsecutiveFailures);
    }

    [Fact]
    public void ASuccessBetweenFailuresStartsTheCountOver()
    {
        var (health, _) = NewHealth();

        Fail(health, 2);
        health.RecordSuccess();
        Fail(health, 2);

        Assert.False(health.IsDown);
        Assert.Equal(2, health.ConsecutiveFailures);
    }

    [Fact]
    public void ASuccessBringsADownServiceBackAndResetsItsLadder()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);
        health.Advance();
        health.Advance();

        health.RecordSuccess();

        Assert.False(health.IsDown);
        Assert.Null(health.NextTryAt);

        // Down again later: the ladder starts from its first step, not where it was left.
        Fail(health, 3);
        Assert.Equal(clock.Now + TimeSpan.FromMinutes(1), health.NextTryAt);
    }

    [Fact]
    public void TheLadderIsOneFiveFifteenSixtyThenEverySixty()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);

        var waits = new List<TimeSpan> { health.NextTryAt!.Value - clock.Now };
        for (var i = 0; i < 5; i++)
        {
            health.Advance();
            waits.Add(health.NextTryAt!.Value - clock.Now);
        }

        Assert.Equal(new[] { 1, 5, 15, 60, 60, 60 }.Select(m => TimeSpan.FromMinutes(m)), waits);
    }

    [Fact]
    public void AdvanceCountsFromTheMomentItIsCalled()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);

        clock.Now += TimeSpan.FromMinutes(1);
        health.Advance();

        Assert.Equal(clock.Now + TimeSpan.FromMinutes(5), health.NextTryAt);
    }

    [Fact]
    public void FurtherFailuresWhileDownDoNotMoveTheNextTry()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);
        var due = health.NextTryAt;

        clock.Now += TimeSpan.FromSeconds(30);
        Fail(health, 4);

        Assert.Equal(due, health.NextTryAt);
        Assert.Equal(7, health.ConsecutiveFailures);
    }

    [Fact]
    public void AdvanceOnAServiceThatIsNotDownDoesNothing()
    {
        var (health, _) = NewHealth();
        Fail(health, 2);

        health.Advance();

        Assert.False(health.IsDown);
        Assert.Null(health.NextTryAt);
    }

    [Fact]
    public void MakeDueNowMakesTheNextTryNowButKeepsItsPlaceOnTheLadder()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);
        health.Advance(); // waiting on the 5-minute step
        clock.Now += TimeSpan.FromMinutes(2);

        health.MakeDueNow();
        Assert.Equal(clock.Now, health.NextTryAt);

        // The try it allowed fails: the ladder moves on from where it was (15), not from the start.
        health.Advance();
        Assert.Equal(clock.Now + TimeSpan.FromMinutes(15), health.NextTryAt);
    }

    [Fact]
    public void MakeDueNowLeavesTheThrottleAlone()
    {
        var (health, clock) = NewHealth();
        Fail(health, 3);
        var throttledUntil = clock.Now + TimeSpan.FromSeconds(30);
        health.SetThrottledUntil(throttledUntil);

        health.MakeDueNow();

        Assert.Equal(throttledUntil, health.ThrottledUntil);
    }

    [Fact]
    public void MakeDueNowOnAServiceThatIsNotDownDoesNothing()
    {
        var (health, _) = NewHealth();
        Fail(health, 1);

        health.MakeDueNow();

        Assert.Null(health.NextTryAt);
    }

    [Fact]
    public void AThrottleIsReportedUntilItIsClearedOrRunsOut()
    {
        var (health, clock) = NewHealth();
        Assert.Null(health.ThrottledUntil);

        var until = clock.Now + TimeSpan.FromSeconds(8);
        health.SetThrottledUntil(until);
        Assert.Equal(until, health.ThrottledUntil);

        clock.Now += TimeSpan.FromSeconds(7);
        Assert.Equal(until, health.ThrottledUntil);

        // Over: never "resuming in 0 s".
        clock.Now += TimeSpan.FromSeconds(1);
        Assert.Null(health.ThrottledUntil);

        health.SetThrottledUntil(clock.Now + TimeSpan.FromSeconds(30));
        health.ClearThrottle();
        Assert.Null(health.ThrottledUntil);
    }

    [Fact]
    public void ASuccessDoesNotClearAThrottleOnItsOwn()
    {
        // Whoever made the request knows whether the answer was a throttle, so
        // clearing it is theirs to say (MalAuthPacingHandler, AniListClient).
        var (health, clock) = NewHealth();
        health.SetThrottledUntil(clock.Now + TimeSpan.FromSeconds(30));

        health.RecordSuccess();

        Assert.NotNull(health.ThrottledUntil);
    }

    [Fact]
    public void TheTwoServicesAreNamedAsTheScreenShowsThem()
    {
        Assert.Equal("MyAnimeList", new MalServiceHealth().Name);
        Assert.Equal("AniList", new AniListServiceHealth().Name);
    }

    [Fact]
    public async Task ConcurrentFailuresAreAllCounted()
    {
        var health = new ServiceHealth("Test service");

        await Task.WhenAll(Enumerable.Range(0, 32).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++)
                health.RecordTemporaryFailure();
        })));

        Assert.Equal(3200, health.ConsecutiveFailures);
        Assert.True(health.IsDown);
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 1)]
    [InlineData(1, 5)]
    [InlineData(2, 15)]
    [InlineData(3, 60)]
    [InlineData(4, 60)]
    [InlineData(500, 60)]
    public void TheRetryScheduleGivesTheWaitAfterEachFailure(int failures, int minutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(minutes), RetrySchedule.DelayAfter(failures));
    }
}
