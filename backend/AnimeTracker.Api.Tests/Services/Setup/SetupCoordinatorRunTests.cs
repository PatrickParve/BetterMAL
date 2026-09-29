using System.Collections.Concurrent;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup tasks 8.1 and 8.3 to 8.8 together (design D1, D12 to D15): the whole
// coordinator, running its four steps and the Home check against scripted services and an
// in-memory database. Nothing here talks to MyAnimeList or AniList, and the first run itself is
// never done on this computer (12.6).
public class SetupCoordinatorRunTests
{
    private static async Task<bool> FinishedAsync(SetupRunKit kit) => await Task.FromResult(kit.Gate.IsFinished);

    private static void SeedList(SetupRunKit kit, int count, Func<int, string>? airing = null)
    {
        for (var id = 1; id <= count; id++)
            kit.Mal.Edges.Add(SetupRunKit.Edge(id, airing: airing?.Invoke(id) ?? "finished_airing"));
    }

    [Fact]
    public async Task AnEmptyListFinishesSetupAtOnce()
    {
        using var kit = new SetupRunKit();
        await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish");

        Assert.Empty(kit.Mal.DetailsCalls);
        Assert.Empty(kit.Series.BuildCalls);
        Assert.Empty(kit.Airing.Batches);
        await using var db = kit.NewDb();
        Assert.NotNull((await db.SetupStates.SingleAsync()).CompletedAt);
    }

    [Fact]
    public async Task ARunGoesFromTheListThroughAllFourStepsToHomeAndStoresWhatEachStepMakes()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 6, id => id <= 2 ? "currently_airing" : "finished_airing");
        kit.Series.Franchises[3] = [3, 4];
        kit.Series.Lone.Add(6);
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish");
        // The airing step has nothing left to drain either, since the list is small.
        await SetupRunKit.WaitUntilAsync(() => !coordinator.IsDraining, "the drain to end");

        await using var db = kit.NewDb();
        Assert.Equal(6, await db.UserAnimeEntries.CountAsync());
        Assert.Equal(6, await db.AnimeMetadata.CountAsync(a => a.LastSyncedAt != default)); // every anime fully fetched
        Assert.All(await db.AnimeMetadata.ToListAsync(), a => Assert.StartsWith("Full ", a.Title));
        Assert.Equal(6, await db.AnimeAiringSyncs.CountAsync(s => s.LastFetchedAt != null));
        Assert.Equal([1, 2, 3, 5, 6], kit.Series.BuildCalls.Order()); // 4 covered by 3's build; 6 is built once and found alone
        Assert.NotNull((await db.SetupStates.SingleAsync()).CompletedAt);

        var status = await kit.NewStatusService(db, coordinator).GetAsync();
        Assert.True(status.Finished);
        Assert.All(new[] { status.Steps.List, status.Steps.Details, status.Steps.Series, status.Steps.Airing },
            step => Assert.Equal(SetupStepPhase.Done, step.Phase));
        Assert.Equal(new SetupProgressDto(2, 2), status.AiringPriority);
    }

    [Fact]
    public async Task TheSeriesStepStartsOnlyAfterTheDetailsHaveFinished()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 5);
        var events = new ConcurrentQueue<string>();
        kit.Mal.BeforeDetails = (id, _) =>
        {
            events.Enqueue($"details {id}");
            return Task.CompletedTask;
        };
        kit.Series.BeforeBuild = (id, _) =>
        {
            events.Enqueue($"series {id}");
            return Task.CompletedTask;
        };
        await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish");

        var log = events.ToList();
        Assert.Equal(5, log.Count(e => e.StartsWith("details")));
        Assert.Equal(5, log.Count(e => e.StartsWith("series")));
        Assert.True(log.FindLastIndex(e => e.StartsWith("details")) < log.FindIndex(e => e.StartsWith("series")));
    }

    [Fact]
    public async Task TheAiringStepRunsAlongsideTheDetailsOnceTheListIsRead()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 3);
        var holdDetails = new TaskCompletionSource();
        kit.Mal.BeforeDetails = (_, ct) => holdDetails.Task.WaitAsync(ct);
        await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Count == 1, "the details step to start");
        await SetupRunKit.WaitUntilAsync(() => kit.Airing.AskedFor.Count == 3, "airing dates for the whole list");

        // Details is mid-fetch of its first anime; airing has already been through the list.
        Assert.Equal(SetupStepPhase.Done, kit.State.Snapshot().List.Phase);
        Assert.False(kit.Gate.IsFinished);
        holdDetails.SetResult();
        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish");
    }

    [Fact]
    public async Task ThreeMalFailuresPauseTheMalWorkerWhileAiringContinues_AndASuccessResumesIt()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 5);
        foreach (var id in new[] { 1, 2, 3 })
            kit.Mal.FailDetails(id, Failures.ServerError()); // three anime in a row, each once
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.MalHealth.IsDown, "MyAnimeList to appear down");
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Details.Phase == SetupStepPhase.Paused, "the queue to pause");
        await SetupRunKit.WaitUntilAsync(() => kit.Airing.AskedFor.Count == 5, "airing to finish the list");
        await SetupRunKit.Settle();

        Assert.Equal([1, 2, 3], kit.Mal.DetailsCalls); // a fourth anime was not tried while it is down
        Assert.False(kit.Gate.IsFinished);             // MyAnimeList down holds Home
        await using (var db = kit.NewDb())
        {
            var status = await kit.NewStatusService(db, coordinator).GetAsync();
            Assert.True(status.Services.Single(s => s.Name == "MyAnimeList").Down);
            Assert.NotNull(status.Services.Single(s => s.Name == "MyAnimeList").NextTryAt);
            Assert.False(status.Services.Single(s => s.Name == "AniList").Down);
            Assert.Equal(SetupStepPhase.Paused, status.Steps.Details.Phase);
        }

        // Retry now: the paused queue tries again at once, with the anime whose turn it was.
        coordinator.RetryNow();
        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish once MyAnimeList answers");

        Assert.Equal([1, 2, 3, 1, 2, 3, 4, 5], kit.Mal.DetailsCalls);
        Assert.False(kit.MalHealth.IsDown);
    }

    [Fact]
    public async Task RetryNowMakesAWaitingAnimeDueAtOnce()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 2);
        kit.Mal.FailDetails(1, Failures.Timeout());
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Details.WaitingRetry?.Count == 1, "anime 1 to wait for its retry");
        await SetupRunKit.Settle();
        Assert.False(kit.Gate.IsFinished);
        Assert.Equal([1, 2], kit.Mal.DetailsCalls); // waiting a minute, not retried yet

        coordinator.RetryNow();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish");
        Assert.Equal([1, 2, 1], kit.Mal.DetailsCalls);
    }

    [Fact]
    public async Task AniListFailingDoesNotHoldHomeButItsPriorityAnimeKeepRetryingInTheBackground()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 3, _ => "currently_airing");
        kit.Airing.FailWholeBatches = 100; // AniList answers nothing
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish without AniList");

        await using var db = kit.NewDb();
        Assert.Empty(await db.AnimeAiringSyncs.ToListAsync()); // no airing data at all, and Home is open
        Assert.True(coordinator.IsDraining);                    // the airing work carries on in the background

        // AniList comes back and Retry now reaches its queue: the drain finishes.
        kit.Airing.FailWholeBatches = 0;
        coordinator.RetryNow();
        await SetupRunKit.WaitUntilAsync(async () => await db.AnimeAiringSyncs.CountAsync() == 3, "the airing dates to arrive");
        await SetupRunKit.WaitUntilAsync(() => !coordinator.IsDraining, "the drain to end");
    }

    [Fact]
    public async Task TheLeftoverAiringWorkDrainsAfterHomeOpensAndIsDrainingSaysSo()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 30, id => id <= 3 ? "currently_airing" : "finished_airing");
        var holdLastBatch = new TaskCompletionSource();
        // The priority anime (1 to 3) are in the first batch of 25; the last five are what is left over.
        kit.Airing.BeforeBatch = (ids, ct) => ids.Count == 5 ? holdLastBatch.Task.WaitAsync(ct) : Task.CompletedTask;
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish with airing left over");

        Assert.True(coordinator.IsDraining);
        Assert.True(coordinator.GetSnapshot().AiringDraining);
        await using var db = kit.NewDb();
        Assert.Equal(25, await db.AnimeAiringSyncs.CountAsync());
        var status = await kit.NewStatusService(db, coordinator).GetAsync();
        Assert.True(status.Finished);
        Assert.True(status.AiringDraining);
        Assert.Equal(new SetupProgressDto(3, 3), status.AiringPriority);

        holdLastBatch.SetResult();
        await SetupRunKit.WaitUntilAsync(() => !coordinator.IsDraining, "the drain to end");
        Assert.Equal(30, await db.AnimeAiringSyncs.CountAsync());
    }

    [Fact]
    public async Task ANewCoordinatorOnTheSameDatabaseResumesWithoutRefetchingWhatWasSaved()
    {
        using var first = new SetupRunKit();
        SeedList(first, 4);
        // The first process gets through anime 1 and 2, and is stopped while it fetches the 3rd.
        first.Mal.BeforeDetails = (id, ct) => id == 3 ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask;
        var firstCoordinator = await first.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => first.Mal.DetailsCalls.Contains(3), "the 3rd anime to be in flight");
        await firstCoordinator.StopAsync(CancellationToken.None);
        Assert.False(first.Gate.IsFinished);

        // The app starts again on the same database, with no memory of the first run.
        using var second = new SetupRunKit(first.Store);
        SeedList(second, 4);
        await second.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => second.Gate.IsFinished, "the resumed setup to finish");

        Assert.Equal([3, 4], second.Mal.DetailsCalls);   // 1 and 2 were saved, and are not fetched again
        Assert.Equal(1, second.Mal.ListReads);            // the list is read again, once
        await using var db = second.NewDb();
        Assert.All(await db.AnimeMetadata.ToListAsync(), a => Assert.StartsWith("Full ", a.Title)); // and none was replaced by a basic row
        Assert.Equal(4, await db.UserAnimeEntries.CountAsync());
    }

    [Fact]
    public async Task ALostLoginKeepsDetailsAndSeriesGoingAndReconnectingFinishesTheList()
    {
        using var kit = new SetupRunKit();
        // An earlier run stored three entries; now MyAnimeList refuses the login.
        foreach (var id in new[] { 1, 2, 3 })
            await kit.SeedAnimeAsync(id);
        kit.Mal.ListFailure = new MalAuthorizationRequiredException();
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Count == 3, "the details step to carry on");
        await SetupRunKit.WaitUntilAsync(() => kit.Series.BuildCalls.Count == 3, "the series step to carry on");
        await SetupRunKit.WaitUntilAsync(() => kit.Airing.AskedFor.Count == 3, "the airing step to carry on");
        await SetupRunKit.Settle();

        Assert.True(kit.State.Snapshot().WaitingForReconnect);
        Assert.False(kit.Gate.IsFinished); // the list has not been read in this run
        await using (var db = kit.NewDb())
        {
            var status = await kit.NewStatusService(db, coordinator).GetAsync();
            Assert.True(status.WaitingForReconnect);
            Assert.Equal(SetupStepPhase.Paused, status.Steps.List.Phase);
            Assert.Equal(3, status.Steps.Details.Done);
        }

        // Reconnect: the callback stores the new login and signals, and the read runs.
        kit.Mal.ListFailure = null;
        kit.Mal.Edges.AddRange(Enumerable.Range(1, 3).Select(id => SetupRunKit.Edge(id)));
        kit.Trigger.Signal();

        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished, "setup to finish after Reconnect");
        Assert.False(kit.State.Snapshot().WaitingForReconnect);
        Assert.Equal(3, kit.Mal.DetailsCalls.Count); // nothing was fetched twice
    }

    [Fact]
    public async Task AFailureNothingHandledRestartsThePieceThatFailedInsteadOfEndingSetup()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 2);
        // The list read throws something the step doesn't recognise as a failure of MyAnimeList:
        // still just a failed read, retried, and never the host's problem.
        kit.Mal.ListFailure = new InvalidOperationException("the database went away");
        var coordinator = await kit.StartAsync();

        await SetupRunKit.WaitUntilAsync(() => kit.Mal.ListReads == 1, "the first read");
        await SetupRunKit.Settle();

        Assert.False(coordinator.ExecuteTask!.IsCompleted);
        Assert.Contains(kit.Log.Lines, l => l.Contains("reading the list failed"));
    }

    // --- stopping ---

    private static async Task AssertStopsCleanlyAsync(SetupCoordinator coordinator)
    {
        await coordinator.StopAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        Assert.Equal(TaskStatus.RanToCompletion, coordinator.ExecuteTask!.Status);
    }

    [Fact]
    public async Task StoppingWhileTheListWaitsForReconnectEndsCleanly()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1);
        kit.Mal.ListFailure = new MalAuthorizationRequiredException();
        var coordinator = await kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().WaitingForReconnect, "the list step to wait for Reconnect");
        await SetupRunKit.WaitUntilAsync(() => kit.Airing.AskedFor.Count == 1, "the other steps to settle");

        await AssertStopsCleanlyAsync(coordinator);
    }

    [Fact]
    public async Task StoppingWhileMalIsDownAndTheQueueIsPausedEndsCleanly()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 4);
        foreach (var id in new[] { 1, 2, 3 })
            kit.Mal.FailDetails(id, Failures.ServerError());
        var coordinator = await kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Details.Phase == SetupStepPhase.Paused, "the queue to pause");

        await AssertStopsCleanlyAsync(coordinator);
    }

    [Fact]
    public async Task StoppingWhileAnimeWaitForTheirRetryEndsCleanly()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 2);
        kit.Mal.FailDetails(1, Failures.Timeout());
        kit.Mal.FailDetails(2, Failures.Timeout());
        var coordinator = await kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Details.WaitingRetry?.Count == 2, "both anime to wait");

        await AssertStopsCleanlyAsync(coordinator);
    }

    [Fact]
    public async Task StoppingAfterSetupHasFinishedWhileTheDrainRunsEndsCleanly()
    {
        using var kit = new SetupRunKit();
        SeedList(kit, 30, id => id <= 3 ? "currently_airing" : "finished_airing");
        kit.Airing.BeforeBatch = (ids, ct) => ids.Count == 5 ? Task.Delay(Timeout.Infinite, ct) : Task.CompletedTask;
        var coordinator = await kit.StartAsync();
        await SetupRunKit.WaitUntilAsync(() => kit.Gate.IsFinished && coordinator.IsDraining, "the drain to start");

        await AssertStopsCleanlyAsync(coordinator);
    }
}
