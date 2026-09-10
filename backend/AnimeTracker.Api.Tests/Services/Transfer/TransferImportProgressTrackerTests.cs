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
}
