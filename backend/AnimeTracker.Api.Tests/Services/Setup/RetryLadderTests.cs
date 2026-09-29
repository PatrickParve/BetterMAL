using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.2 (design D13; spec "A failing anime is retried and keeps its
// place"): one anime's waits, and the rule that a waiting anime is passed over and taken
// back in its own place when its wait ends. Times are passed in, so nothing here sleeps.
public class RetryLadderTests
{
    private static readonly DateTimeOffset Start = new(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnAnimeThatHasNeverFailedIsDue()
    {
        var ladder = new RetryLadder();

        Assert.Equal(1, ladder.NextDue([1, 2, 3], Start));
        Assert.False(ladder.HasFailed(1));
        Assert.False(ladder.IsWaiting(1, Start));
    }

    [Fact]
    public void AFailedAnimeWaitsOneMinuteThenFiveThenFifteenThenSixtyThenSixtyAgain()
    {
        var ladder = new RetryLadder();
        var now = Start;

        var waits = new List<TimeSpan>();
        for (var failure = 0; failure < 6; failure++)
        {
            ladder.RecordFailure(7, now);
            var due = ladder.EarliestDue([7], now)!.Value;
            waits.Add(due - now);
            now = due; // its wait ends, it is tried again, and fails again
        }

        Assert.Equal(
            [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60), TimeSpan.FromMinutes(60)],
            waits);
    }

    [Fact]
    public void AWaitingAnimeIsPassedOverAndTheNextOneInTheOrderIsTaken()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(2, Start);

        Assert.Equal(1, ladder.NextDue([1, 2, 3], Start));
        Assert.Equal(3, ladder.NextDue([2, 3], Start));
        Assert.Null(ladder.NextDue([2], Start)); // nothing but the waiting one
    }

    [Fact]
    public void WhenItsWaitEndsItIsTakenBeforeEverythingAfterItInTheOrder()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(2, Start);

        // Still waiting after 59 s: passed over, the third is taken.
        Assert.Equal(1, ladder.NextDue([1, 2, 3], Start.AddSeconds(59)));
        // Due at 60 s, and 2 comes after 1 in the order but before 3: with 1 done, 2 is next.
        Assert.Equal(2, ladder.NextDue([2, 3, 4], Start.AddSeconds(60)));
        Assert.Equal([2, 3], ladder.Due([2, 3, 4], Start.AddSeconds(60), 2));
    }

    [Fact]
    public void DueReturnsTheFirstNotWaitingInOrderUpToTheMaximum()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(2, Start);
        ladder.RecordFailure(4, Start);

        Assert.Equal([1, 3, 5], ladder.Due([1, 2, 3, 4, 5, 6, 7], Start, 3));
        Assert.Empty(ladder.Due([2, 4], Start, 5));
    }

    [Fact]
    public void ASuccessClearsTheAnimeAndALaterFailureStartsTheLadderOver()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(5, Start);
        ladder.RecordFailure(5, Start.AddMinutes(1)); // second failure: a five-minute wait
        Assert.True(ladder.HasFailed(5));

        ladder.Clear(5);
        Assert.False(ladder.HasFailed(5));
        Assert.Equal(5, ladder.NextDue([5], Start));

        ladder.RecordFailure(5, Start);
        Assert.Equal(Start.AddMinutes(1), ladder.EarliestDue([5], Start)); // the one-minute wait again
    }

    [Fact]
    public void EarliestDueIsTheSoonestWaitAmongTheGivenAnimeOnly()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(1, Start);                  // due +1 min
        ladder.RecordFailure(2, Start);
        ladder.RecordFailure(2, Start);                  // due +5 min

        Assert.Equal(Start.AddMinutes(1), ladder.EarliestDue([1, 2], Start));
        Assert.Equal(Start.AddMinutes(5), ladder.EarliestDue([2, 3], Start)); // 3 never failed
        Assert.Null(ladder.EarliestDue([3], Start));
        Assert.Null(ladder.EarliestDue([1], Start.AddMinutes(1))); // already over
    }

    [Fact]
    public void WaitingCountsOnlyTheGivenAnimeStillWaitingAndNamesTheSoonest()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(1, Start);
        ladder.RecordFailure(2, Start);
        ladder.RecordFailure(2, Start);
        ladder.RecordFailure(9, Start); // not among the step's targets

        var waiting = ladder.Waiting([1, 2, 3], Start);

        Assert.Equal(new SetupRetryWait(2, Start.AddMinutes(1)), waiting);
        Assert.Null(ladder.Waiting([3], Start));
        Assert.Equal(1, ladder.Waiting([1, 2], Start.AddMinutes(1))!.Count); // 1's wait is over, 2's is not
    }

    [Fact]
    public void MakeAllDueNowEndsEveryWaitButKeepsTheFailuresSoALaterOneClimbsTheLadder()
    {
        var ladder = new RetryLadder();
        ladder.RecordFailure(1, Start);
        ladder.RecordFailure(2, Start);
        ladder.RecordFailure(2, Start);

        ladder.MakeAllDueNow(Start.AddSeconds(10));

        Assert.Equal(1, ladder.NextDue([1, 2], Start.AddSeconds(10)));
        Assert.Equal([1, 2], ladder.Due([1, 2], Start.AddSeconds(10), 5));
        Assert.True(ladder.HasFailed(1)); // still failed, until it succeeds
        Assert.Null(ladder.Waiting([1, 2], Start.AddSeconds(10)));

        // 2 failed twice before, so a third failure is a fifteen-minute wait.
        ladder.RecordFailure(2, Start.AddSeconds(10));
        Assert.Equal(Start.AddSeconds(10).AddMinutes(15), ladder.EarliestDue([2], Start.AddSeconds(10)));
    }

    [Fact]
    public void MakeAllDueNowOnAnEmptyLadderChangesNothing()
    {
        var ladder = new RetryLadder();
        ladder.MakeAllDueNow(Start);
        Assert.Equal(1, ladder.NextDue([1], Start));
    }
}
