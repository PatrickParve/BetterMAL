using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// The settings page's manual "refresh all airing data" run (design.md D3): a
// refresh that throws leaves the tracker Failed with a plain AniList reason
// rather than only logging, which is what AiringFullRefreshProgressTracker
// used to do before it lost its own Failed phase.
public class AiringFullRefreshBackgroundServiceTests
{
    [Fact]
    public async Task ARefreshThatThrowsLeavesTheTrackerFailedWithTheAniListReason()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var refreshService = new ThrowingEpisodeScheduleRefreshService();
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger,
            progress,
            TestSetupGates.Finished(), NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin(); // mirrors AiringController.TriggerFullRefresh (design.md D2): the tracker is the start gate
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (progress.Snapshot.Phase != JobPhase.Failed && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Failed, progress.Snapshot.Phase); // fail loudly on a test timeout, not a hang
            Assert.Equal("AniList couldn't be reached.", progress.Snapshot.Error);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task APartiallyFailedRefreshLeavesTheTrackerFailedWithTheCountReason()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var targets = Enumerable.Range(1, 5).ToList();
        var refreshService = new FakeEpisodeScheduleRefreshService(targets, new RefreshBatchResult(Fetched: 3, NoData: 0, Failed: 2));
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger,
            progress,
            TestSetupGates.Finished(), NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin();
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (progress.Snapshot.Phase == JobPhase.Running && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Failed, progress.Snapshot.Phase);
            Assert.Equal("2 of 5 anime couldn't be refreshed from AniList.", progress.Snapshot.Error);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ANoDataOnlyResultEndsAsComplete()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var targets = Enumerable.Range(1, 5).ToList();
        var refreshService = new FakeEpisodeScheduleRefreshService(targets, new RefreshBatchResult(Fetched: 2, NoData: 3, Failed: 0));
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger,
            progress,
            TestSetupGates.Finished(), NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin();
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (progress.Snapshot.Phase == JobPhase.Running && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Complete, progress.Snapshot.Phase);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // episode-airing-data "Refreshing all airing dates skips only shows whose history
    // is complete": both buttons are one job. The trigger carries which one was
    // pressed, and a refresh started by hand looks unknown anime up again in both.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheModeThatWasSignalledSelectsTheTargets_AndBothLookUnknownAnimeUpAgain(bool force)
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var refreshService = new FakeEpisodeScheduleRefreshService(Enumerable.Range(1, 4).ToList(), new RefreshBatchResult(Fetched: 4, NoData: 0, Failed: 0));
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger,
            progress,
            TestSetupGates.Finished(), NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin();
            trigger.Signal(force);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (progress.Snapshot.Phase == JobPhase.Running && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Complete, progress.Snapshot.Phase);
            Assert.Equal([force], refreshService.ForceRequests);
            Assert.True(Assert.Single(refreshService.BatchOptions)!.RelookupAbsent);
            Assert.Equal(4, progress.Snapshot.Total);
            Assert.Equal(4, progress.Snapshot.Done); // one report per anime the batch finished
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AForcedSignalDoesNotLeakIntoTheNextPlainRun()
    {
        var trigger = new AiringFullRefreshTrigger();

        trigger.Signal(force: true);
        Assert.True(await trigger.WaitAsync(CancellationToken.None));

        trigger.Signal();
        Assert.False(await trigger.WaitAsync(CancellationToken.None));
    }

    [Fact]
    public async Task APlainSignalCoalescedIntoAForcedOneStaysForced()
    {
        var trigger = new AiringFullRefreshTrigger();

        trigger.Signal(force: true);
        trigger.Signal();

        Assert.True(await trigger.WaitAsync(CancellationToken.None));
    }

    // add-first-run-setup design D2: the API refuses the button until setup has
    // finished; a signal that arrived anyway does not start the job early.
    [Fact]
    public async Task NoRefreshRunsUntilSetupHasFinished_ThenTheSignalledRunStarts()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var refreshService = new FakeEpisodeScheduleRefreshService([1, 2], new RefreshBatchResult(2, 0, 0));
        var gate = TestSetupGates.Unfinished();
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger, progress, gate, NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin();
            trigger.Signal();
            await TestSetupGates.LetItRunAsync();
            Assert.Empty(refreshService.ForceRequests);
            Assert.Equal(JobPhase.Running, progress.Snapshot.Phase);

            await gate.MarkFinishedAsync();
            await TestSetupGates.WaitForAsync(() => progress.Snapshot.Phase == JobPhase.Complete);
            Assert.Equal([false], refreshService.ForceRequests);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class ThrowingEpisodeScheduleRefreshService : IEpisodeScheduleRefreshService
    {
        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) =>
            throw new HttpRequestException("AniList is unreachable");

        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleRefreshService(List<int> targets, RefreshBatchResult result) : IEpisodeScheduleRefreshService
    {
        public List<bool> ForceRequests { get; } = [];
        public List<RefreshBatchOptions?> BatchOptions { get; } = [];

        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default)
        {
            ForceRequests.Add(force);
            return Task.FromResult(targets);
        }

        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default)
        {
            BatchOptions.Add(options);
            foreach (var animeId in animeIds)
                options?.OnAnimeDone?.Invoke(animeId, AiringRefreshOutcome.Fetched);
            return Task.FromResult(result);
        }

        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeServiceProvider(IEpisodeScheduleRefreshService refreshService) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEpisodeScheduleRefreshService) ? refreshService : null;
    }

    private sealed class FakeServiceScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(provider);
    }

    private sealed class FakeServiceScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() { }
    }
}
