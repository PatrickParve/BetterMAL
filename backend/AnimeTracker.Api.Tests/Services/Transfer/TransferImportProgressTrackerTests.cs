using AnimeTracker.Api.Services.Transfer;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportProgressTracker (device-transfer design.md D1/D13, tasks.md 4.4).
public class TransferImportProgressTrackerTests
{
    [Fact]
    public void ANewTrackerStartsNotStarted()
    {
        var tracker = new TransferImportProgressTracker();

        Assert.Equal(TransferImportPhase.NotStarted, tracker.Snapshot.Phase);
        Assert.Null(tracker.Snapshot.Report);
    }

    [Fact]
    public void MarkPendingReportsRunningAndRecordsTheFile()
    {
        var tracker = new TransferImportProgressTracker();
        var exportedAt = DateTimeOffset.UtcNow;

        tracker.MarkPending("My Phone", exportedAt);

        var snapshot = tracker.Snapshot;
        Assert.Equal(TransferImportPhase.Running, snapshot.Phase);
        Assert.Equal("My Phone", snapshot.DeviceName);
        Assert.Equal(exportedAt, snapshot.ExportedAt);
        Assert.Equal(0, snapshot.Done);
        Assert.Equal(0, snapshot.Total);
    }

    [Fact]
    public void StartAndReportProgressAndAddToTotalUpdateCounts()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending(null, DateTimeOffset.UtcNow);

        tracker.Start(10);
        tracker.AddToTotal(3); // a picture refresh discovered mid-run
        tracker.ReportProgress(5);

        var snapshot = tracker.Snapshot;
        Assert.Equal(13, snapshot.Total);
        Assert.Equal(5, snapshot.Done);
    }

    [Fact]
    public void CompleteReportsTheGivenReportAndKeepsTheFileInfo()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        var report = new TransferImportReport([], [], [], []);

        tracker.Complete(report);

        var snapshot = tracker.Snapshot;
        Assert.Equal(TransferImportPhase.Complete, snapshot.Phase);
        Assert.Same(report, snapshot.Report);
        Assert.Equal("My Phone", snapshot.DeviceName);
    }

    [Fact]
    public void FailReportsTheReason()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        tracker.Fail("Nothing from the file was applied.");

        var snapshot = tracker.Snapshot;
        Assert.Equal(TransferImportPhase.Failed, snapshot.Phase);
        Assert.Equal("Nothing from the file was applied.", snapshot.Error);
    }

    [Fact]
    public void TheReportIsKeptUntilTheNextMarkPending()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        var report = new TransferImportReport([], [], [], []);
        tracker.Complete(report);

        // A later poll, well after the run ended, still reads the report.
        Assert.Same(report, tracker.Snapshot.Report);

        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        Assert.Null(tracker.Snapshot.Report);
        Assert.Equal(TransferImportPhase.Running, tracker.Snapshot.Phase);
    }

    // The shared job shape (design.md D4): a start/end time so the file
    // import can join "the oldest job", and an unknown total before the file
    // is read rather than a stalled-looking zero.
    [Fact]
    public void MarkPendingStampsAStart()
    {
        var tracker = new TransferImportProgressTracker();
        var before = DateTimeOffset.UtcNow;

        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        Assert.NotNull(tracker.Snapshot.StartedAt);
        Assert.True(tracker.Snapshot.StartedAt >= before);
    }

    [Fact]
    public void CompleteAndFailStampAnEnd()
    {
        var completed = new TransferImportProgressTracker();
        completed.MarkPending("My Phone", DateTimeOffset.UtcNow);
        completed.Complete(new TransferImportReport([], [], [], []));
        Assert.NotNull(completed.Snapshot.FinishedAt);

        var failed = new TransferImportProgressTracker();
        failed.MarkPending("My Phone", DateTimeOffset.UtcNow);
        failed.Fail("Nothing from the file was applied.");
        Assert.NotNull(failed.Snapshot.FinishedAt);
    }

    [Fact]
    public void ToJobSnapshotReportsAnUnknownTotalBeforeStartAndTheRealTotalAfter()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        Assert.Null(tracker.ToJobSnapshot().Total);

        tracker.Start(10);

        Assert.Equal(10, tracker.ToJobSnapshot().Total);
    }

    [Fact]
    public void MarkOutcomeSeenAppliesTheGuard()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        tracker.Complete(new TransferImportReport([], [], [], []));
        var finishedAt = tracker.Snapshot.FinishedAt!.Value;

        tracker.MarkOutcomeSeen(DateTimeOffset.UtcNow.AddMinutes(-5)); // stale
        Assert.False(tracker.Snapshot.OutcomeSeen);

        tracker.MarkOutcomeSeen(finishedAt);
        Assert.True(tracker.Snapshot.OutcomeSeen);
    }

    // Closing the outcome (simplify-settings-and-first-fetch-states D3, tasks 4.4).
    [Fact]
    public void DismissingACompleteRunClosesItAndCountsItAsSeen()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        tracker.Complete(new TransferImportReport([], [], [], []));
        Assert.False(tracker.Snapshot.Dismissed);

        tracker.Dismiss(tracker.Snapshot.FinishedAt!.Value);

        Assert.True(tracker.Snapshot.Dismissed);
        Assert.True(tracker.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void DismissingAFailedRunClosesItAndCountsItAsSeen()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        tracker.Fail("Nothing from the file was applied.");

        tracker.Dismiss(tracker.Snapshot.FinishedAt!.Value);

        Assert.True(tracker.Snapshot.Dismissed);
        Assert.True(tracker.Snapshot.OutcomeSeen);
        Assert.Equal(TransferImportPhase.Failed, tracker.Snapshot.Phase);
    }

    [Fact]
    public void DismissingARunningImportChangesNothing()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        tracker.Dismiss(DateTimeOffset.UtcNow);

        Assert.False(tracker.Snapshot.Dismissed);
        Assert.False(tracker.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void DismissingWithAStaleFinishedAtChangesNothing()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        tracker.Complete(new TransferImportReport([], [], [], []));

        tracker.Dismiss(DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.False(tracker.Snapshot.Dismissed);
        Assert.False(tracker.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void ANewRunAfterADismissalStartsClean()
    {
        var tracker = new TransferImportProgressTracker();
        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);
        tracker.Complete(new TransferImportReport([], [], [], []));
        tracker.Dismiss(tracker.Snapshot.FinishedAt!.Value);

        tracker.MarkPending("My Phone", DateTimeOffset.UtcNow);

        Assert.False(tracker.Snapshot.Dismissed);
        Assert.False(tracker.Snapshot.OutcomeSeen);
    }
}
