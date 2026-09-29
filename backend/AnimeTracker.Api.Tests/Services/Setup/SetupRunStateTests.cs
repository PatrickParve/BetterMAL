using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup design D1: what setup keeps in memory, and hands the status
// read as a snapshot that can't change under it.
public class SetupRunStateTests
{
    [Fact]
    public void ANewStateIsIdle()
    {
        var snapshot = new SetupRunState().Snapshot();

        Assert.False(snapshot.ListReadThisRun);
        Assert.False(snapshot.WaitingForReconnect);
        Assert.False(snapshot.AiringDraining);
        Assert.All(Enum.GetValues<SetupStep>(), step => Assert.Equal(SetupStepPhase.Waiting, snapshot.Step(step).Phase));
        Assert.Empty(snapshot.NoSeriesAnimeIds);
        Assert.Empty(snapshot.UnrecognizedStatuses);
    }

    [Fact]
    public void EachStepKeepsItsOwnPhaseRetryWaitAndEta()
    {
        var state = new SetupRunState();
        var due = new DateTimeOffset(2026, 9, 29, 12, 5, 0, TimeSpan.Zero);

        state.SetPhase(SetupStep.Details, SetupStepPhase.Paused);
        state.SetWaitingRetry(SetupStep.Details, new SetupRetryWait(3, due));
        // Twenty airing anime in the half minute before the last look, fifty still to do.
        state.RecordProgress(SetupStep.Airing, 20, 70, due);
        state.RecordProgress(SetupStep.Airing, 20, 50, due.AddSeconds(30));
        state.SetPhase(SetupStep.Airing, SetupStepPhase.Running);

        var snapshot = state.Snapshot();
        Assert.Equal(SetupStepPhase.Paused, snapshot.Details.Phase);
        Assert.Equal(new SetupRetryWait(3, due), snapshot.Details.WaitingRetry);
        Assert.Null(snapshot.Details.EtaSeconds);
        Assert.Equal(SetupStepPhase.Running, snapshot.Airing.Phase);
        Assert.Equal(75, snapshot.Airing.EtaSeconds); // 20 items in 30 s: 50 more take 75 s
        Assert.Equal(SetupStepPhase.Waiting, snapshot.List.Phase);
        Assert.Equal(SetupStepPhase.Waiting, snapshot.Series.Phase);
    }

    [Fact]
    public void EachStepKeepsItsOwnPace()
    {
        var state = new SetupRunState();
        var at = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

        state.RecordProgress(SetupStep.Details, 5, 100, at);
        state.RecordProgress(SetupStep.Details, 5, 95, at.AddSeconds(10));
        state.RecordProgress(SetupStep.Series, 1, 100, at);

        var snapshot = state.Snapshot();
        Assert.Equal(190, snapshot.Details.EtaSeconds); // 5 items in 10 s
        Assert.Null(snapshot.Series.EtaSeconds);        // one event: no pace of its own yet
    }

    [Fact]
    public void ProgressWithNoKnownTotalKeepsThePaceButLeavesTheEstimateOut()
    {
        var state = new SetupRunState();
        var at = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);

        // The list read before it knows how long the list is.
        state.RecordProgress(SetupStep.List, 100, null, at);
        state.RecordProgress(SetupStep.List, 100, null, at.AddSeconds(20));
        Assert.Null(state.Snapshot().List.EtaSeconds);

        // The total turns up: the pace read so far gives the estimate at once.
        state.RecordProgress(SetupStep.List, 0, 300, at.AddSeconds(20));
        Assert.Equal(60, state.Snapshot().List.EtaSeconds);
    }

    [Fact]
    public void ResettingAStepForgetsItsPaceAndItsEstimate()
    {
        var state = new SetupRunState();
        var at = new DateTimeOffset(2026, 9, 29, 12, 0, 0, TimeSpan.Zero);
        state.RecordProgress(SetupStep.Airing, 10, 50, at);
        state.RecordProgress(SetupStep.Airing, 10, 40, at.AddSeconds(20));
        Assert.NotNull(state.Snapshot().Airing.EtaSeconds);

        state.ResetEta(SetupStep.Airing);

        Assert.Null(state.Snapshot().Airing.EtaSeconds);
        // The old pace is gone, not just hidden: one fresh event is not enough.
        state.RecordProgress(SetupStep.Airing, 10, 30, at.AddSeconds(3600));
        Assert.Null(state.Snapshot().Airing.EtaSeconds);
    }

    [Fact]
    public void TheListReadCountsAndFlagsAreKept()
    {
        var state = new SetupRunState();

        state.SetListProgress(300, 624);
        state.MarkListRead();
        state.SetWaitingForReconnect(true);
        state.SetDraining(true);

        var snapshot = state.Snapshot();
        Assert.Equal((300, 624), (snapshot.ListRead, snapshot.ListTotal));
        Assert.True(snapshot.ListReadThisRun);
        Assert.True(snapshot.WaitingForReconnect);
        Assert.True(snapshot.AiringDraining);
        Assert.True(state.IsDraining);
    }

    [Fact]
    public void ASnapshotDoesNotChangeWhenTheStateDoes()
    {
        var state = new SetupRunState();
        state.AddNoSeries(1);
        state.SetUnrecognized([new UnrecognizedStatusSkip(7, "Old Show", "dropped_forever")]);
        var before = state.Snapshot();

        state.AddNoSeries(2);
        state.SetUnrecognized([]);
        state.SetPhase(SetupStep.Series, SetupStepPhase.Running);

        Assert.Equal([1], before.NoSeriesAnimeIds);
        Assert.Single(before.UnrecognizedStatuses);
        Assert.Equal(SetupStepPhase.Waiting, before.Series.Phase);
        Assert.Equal([1, 2], state.Snapshot().NoSeriesAnimeIds.Order());
    }

    [Fact]
    public void TheUnrecognizedListIsReplacedByTheLatestRead()
    {
        var state = new SetupRunState();
        state.SetUnrecognized([new UnrecognizedStatusSkip(1, "A", "x"), new UnrecognizedStatusSkip(2, "B", "y")]);

        state.SetUnrecognized([new UnrecognizedStatusSkip(3, "C", "z")]);

        Assert.Equal([3], state.Snapshot().UnrecognizedStatuses.Select(s => s.AnimeId));
    }
}
