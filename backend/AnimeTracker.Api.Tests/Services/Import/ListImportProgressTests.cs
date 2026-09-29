using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Tests.Services.Import;

// ListImportProgress.MarkOutcomeSeen (design.md D3, tasks.md 4.2): the
// override acknowledges whichever snapshot Snapshot is currently showing, the
// live run while shown or the frozen _shown snapshot while quiet, mirroring
// Snapshot's own choice.
public class ListImportProgressTests
{
    [Fact]
    public void SetupReadingTheListOpensTheGateWithoutBeginningOrShowingARun()
    {
        var progress = new ListImportProgress();
        Assert.False(progress.Gate.WentThroughSinceStart);

        progress.MarkListReadBySetup();

        Assert.True(progress.Gate.WentThroughSinceStart);
        Assert.False(progress.Gate.Running);
        Assert.Null(progress.Gate.LastReadFailure);
        Assert.Equal(JobPhase.NotStarted, progress.Snapshot.Phase); // Settings' report stays as quiet as it was
    }

    [Fact]
    public void SetupReadingTheListClearsAnEarlierFailureAndItsPlannedRetry()
    {
        var progress = new ListImportProgress();
        progress.BeginRun(visibleFromStart: false);
        progress.FailBeforeRead("MyAnimeList couldn't be reached.");
        progress.SetRetryAt(DateTimeOffset.UtcNow.AddMinutes(5));

        progress.MarkListReadBySetup();

        Assert.Null(progress.Gate.LastReadFailure);
        Assert.Null(progress.Gate.RetryAt);
        Assert.True(progress.Gate.WentThroughSinceStart);
    }

    [Fact]
    public void AShownFailureIsAcknowledgedWhileQuietThroughShownsEndTime()
    {
        var progress = new ListImportProgress();
        progress.BeginRun(visibleFromStart: true);
        progress.Fail("MyAnimeList couldn't be reached.");
        var finishedAt = progress.Snapshot.FinishedAt!.Value;

        progress.BeginRun(visibleFromStart: false); // a quiet retry begins; the failure stays frozen in _shown

        progress.MarkOutcomeSeen(finishedAt);

        Assert.True(progress.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void ARunShownFromTheStartIsAcknowledgedThroughTheLiveSnapshot()
    {
        var progress = new ListImportProgress();
        progress.BeginRun(visibleFromStart: true);
        progress.Complete();
        var finishedAt = progress.Snapshot.FinishedAt!.Value;

        progress.MarkOutcomeSeen(finishedAt);

        Assert.True(progress.Snapshot.OutcomeSeen);
    }

    [Fact]
    public void AnUnmatchedTimeWhileQuietChangesNothing()
    {
        var progress = new ListImportProgress();
        progress.BeginRun(visibleFromStart: true);
        progress.Fail("MyAnimeList couldn't be reached.");
        progress.BeginRun(visibleFromStart: false);

        progress.MarkOutcomeSeen(DateTimeOffset.UtcNow.AddMinutes(-5));

        Assert.False(progress.Snapshot.OutcomeSeen);
    }
}
