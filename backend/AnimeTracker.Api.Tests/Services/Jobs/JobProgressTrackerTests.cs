using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Tests.Services.Jobs;

// The shared job lifecycle every background job uses (design.md D1-D3):
// TryBegin is the start gate, a run always ends as Complete or Failed, and a
// finished run can be started again with a clean slate.
public class JobProgressTrackerTests
{
    [Fact]
    public void ASecondTryBeginWhileRunningReturnsFalseAndLeavesTheSnapshotAlone()
    {
        var tracker = new JobProgressTracker();
        Assert.True(tracker.TryBegin());
        tracker.SetTotal(10);
        tracker.ReportProgress(3);
        var before = tracker.Snapshot;

        Assert.False(tracker.TryBegin());

        Assert.Equal(before, tracker.Snapshot);
        Assert.Equal(JobPhase.Running, tracker.Snapshot.Phase);
    }

    [Fact]
    public void TryBeginAfterCompleteReturnsTrueAndResetsDoneTotalAndError()
    {
        var tracker = new JobProgressTracker();
        Assert.True(tracker.TryBegin());
        tracker.SetTotal(5);
        tracker.ReportProgress(5);
        tracker.Complete();

        Assert.True(tracker.TryBegin());

        var snapshot = tracker.Snapshot;
        Assert.Equal(JobPhase.Running, snapshot.Phase);
        Assert.Equal(0, snapshot.Done);
        Assert.Null(snapshot.Total);
        Assert.Null(snapshot.Error);
    }

    [Fact]
    public void TryBeginAfterFailedReturnsTrueAndResetsDoneTotalAndError()
    {
        var tracker = new JobProgressTracker();
        Assert.True(tracker.TryBegin());
        tracker.SetTotal(5);
        tracker.ReportProgress(2);
        tracker.Fail("MyAnimeList couldn't be reached.");

        Assert.True(tracker.TryBegin());

        var snapshot = tracker.Snapshot;
        Assert.Equal(JobPhase.Running, snapshot.Phase);
        Assert.Equal(0, snapshot.Done);
        Assert.Null(snapshot.Total);
        Assert.Null(snapshot.Error);
    }

    [Fact]
    public void FailKeepsDoneAndStampsFinishedAt()
    {
        var tracker = new JobProgressTracker();
        tracker.TryBegin();
        tracker.SetTotal(5);
        tracker.ReportProgress(3);

        tracker.Fail("Something went wrong — see the backend logs.");

        var snapshot = tracker.Snapshot;
        Assert.Equal(JobPhase.Failed, snapshot.Phase);
        Assert.Equal(3, snapshot.Done);
        Assert.Equal("Something went wrong — see the backend logs.", snapshot.Error);
        Assert.NotNull(snapshot.FinishedAt);
    }

    [Fact]
    public void TotalIsNullAfterTryBegin()
    {
        var tracker = new JobProgressTracker();

        Assert.True(tracker.TryBegin());

        Assert.Null(tracker.Snapshot.Total);
    }
}
