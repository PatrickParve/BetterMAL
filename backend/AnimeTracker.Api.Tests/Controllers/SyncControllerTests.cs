using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Controllers;

// SyncController's job-starting endpoints (background-jobs "A job is started
// once"/design.md D2, D4, D18): re-sync starts once like the other
// trigger-based jobs; sync now, reconciliation and the held-decision pair
// start once through BackgroundJobRunner and answer 202 with the job's state
// either way; a single held decision is refused with 409 while the bulk pair
// is deciding.
public class SyncControllerTests
{
    private static SyncController CreateController(
        IReconciliationService? reconciliationService = null,
        IHeldChangeService? heldChangeService = null,
        IEntryPushService? pushService = null,
        IResyncTrigger? resyncTrigger = null,
        ResyncProgress? resyncProgress = null,
        BackgroundJobRunner? runner = null,
        SyncNowProgress? syncNowProgress = null,
        ReconcileProgress? reconcileProgress = null,
        HeldDecisionProgress? heldDecisionProgress = null)
    {
        reconciliationService ??= new UnusedReconciliationService();
        heldChangeService ??= new UnusedHeldChangeService();
        pushService ??= new UnusedEntryPushService();

        return new SyncController(
            reconciliationService,
            heldChangeService,
            new UnusedUserAnimeEntryRepository(),
            resyncTrigger ?? new ResyncTrigger(),
            resyncProgress ?? new ResyncProgress(),
            runner ?? new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(pushService, reconciliationService, heldChangeService))),
            syncNowProgress ?? new SyncNowProgress(),
            reconcileProgress ?? new ReconcileProgress(),
            heldDecisionProgress ?? new HeldDecisionProgress());
    }

    [Fact]
    public void TriggerResyncFromMalGivesRunningOnBothOfTwoPresses()
    {
        var progress = new ResyncProgress();
        var controller = CreateController(resyncProgress: progress);

        var first = Assert.IsType<AcceptedResult>(controller.TriggerResyncFromMal());
        Assert.Equal("Running", Assert.IsType<JobDto>(first.Value).Phase);

        var second = Assert.IsType<AcceptedResult>(controller.TriggerResyncFromMal());
        Assert.Equal("Running", Assert.IsType<JobDto>(second.Value).Phase);
    }

    [Fact]
    public async Task TriggerResyncFromMalSignalsExactlyOnceAcrossTwoPresses()
    {
        var trigger = new ResyncTrigger();
        var controller = CreateController(resyncTrigger: trigger);

        controller.TriggerResyncFromMal();
        controller.TriggerResyncFromMal();

        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            await trigger.WaitAsync(cts.Token);

        using var secondCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAsync<OperationCanceledException>(() => trigger.WaitAsync(secondCts.Token));
    }

    [Fact]
    public async Task SyncNowReturns202RunningAndASecondCallStartsNoSecondRun()
    {
        var gate = new TaskCompletionSource();
        var pushService = new GatedEntryPushService(gate.Task);
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(pushService, new UnusedReconciliationService(), new UnusedHeldChangeService())));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var controller = CreateController(pushService: pushService, runner: runner);

            var first = Assert.IsType<AcceptedResult>(controller.SyncNow());
            Assert.Equal("Running", Assert.IsType<JobDto>(first.Value).Phase);

            var second = Assert.IsType<AcceptedResult>(controller.SyncNow());
            Assert.Equal("Running", Assert.IsType<JobDto>(second.Value).Phase);

            Assert.Equal(1, pushService.CallCount);
            gate.SetResult();
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ReconcileReturns202RunningAndASecondCallStartsNoSecondRun()
    {
        var gate = new TaskCompletionSource();
        var reconciliationService = new GatedReconciliationService(gate.Task);
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(new UnusedEntryPushService(), reconciliationService, new UnusedHeldChangeService())));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var controller = CreateController(reconciliationService: reconciliationService, runner: runner);

            var first = Assert.IsType<AcceptedResult>(controller.Reconcile());
            Assert.Equal("Running", Assert.IsType<JobDto>(first.Value).Phase);

            var second = Assert.IsType<AcceptedResult>(controller.Reconcile());
            Assert.Equal("Running", Assert.IsType<JobDto>(second.Value).Phase);

            Assert.Equal(1, reconciliationService.CallCount);
            gate.SetResult();
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task AcceptAllHeldReturns202RunningAndASecondCallStartsNoSecondRun()
    {
        var gate = new TaskCompletionSource();
        var heldChangeService = new GatedHeldChangeService(gate.Task);
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(new UnusedEntryPushService(), new UnusedReconciliationService(), heldChangeService)));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var controller = CreateController(heldChangeService: heldChangeService, runner: runner);

            var first = Assert.IsType<AcceptedResult>(controller.AcceptAllHeld());
            Assert.Equal("Running", Assert.IsType<HeldDecisionJobDto>(first.Value).Phase);

            var second = Assert.IsType<AcceptedResult>(controller.AcceptAllHeld());
            Assert.Equal("Running", Assert.IsType<HeldDecisionJobDto>(second.Value).Phase);

            Assert.Equal(1, heldChangeService.AcceptAllCallCount);
            gate.SetResult();
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task DeclineAllHeldReturns202RunningAndASecondCallStartsNoSecondRun()
    {
        var gate = new TaskCompletionSource();
        var heldChangeService = new GatedHeldChangeService(gate.Task);
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(new UnusedEntryPushService(), new UnusedReconciliationService(), heldChangeService)));
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var controller = CreateController(heldChangeService: heldChangeService, runner: runner);

            var first = Assert.IsType<AcceptedResult>(controller.DeclineAllHeld());
            Assert.Equal("Running", Assert.IsType<HeldDecisionJobDto>(first.Value).Phase);

            var second = Assert.IsType<AcceptedResult>(controller.DeclineAllHeld());
            Assert.Equal("Running", Assert.IsType<HeldDecisionJobDto>(second.Value).Phase);

            Assert.Equal(1, heldChangeService.DeclineAllCallCount);
            gate.SetResult();
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ASingleHeldDecisionReturns409WhileHeldDecisionProgressRuns()
    {
        var heldDecisionProgress = new HeldDecisionProgress();
        heldDecisionProgress.TryBegin(HeldDecisionAction.Accept);
        var controller = CreateController(heldDecisionProgress: heldDecisionProgress);

        var acceptResult = await controller.AcceptHeld(1, CancellationToken.None);
        var acceptConflict = Assert.IsType<ConflictObjectResult>(acceptResult);
        dynamic acceptError = acceptConflict.Value!;
        Assert.Equal("Held changes are being decided; try again when that finishes.", (string)acceptError.error);

        var declineResult = await controller.DeclineHeld(2, CancellationToken.None);
        var declineConflict = Assert.IsType<ConflictObjectResult>(declineResult);
        dynamic declineError = declineConflict.Value!;
        Assert.Equal("Held changes are being decided; try again when that finishes.", (string)declineError.error);
    }

    private sealed class GatedEntryPushService(Task gate) : IEntryPushService
    {
        public int CallCount { get; private set; }

        public Task<bool> PushIfPendingAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> PushPendingDeletionAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();

        public async Task<DrainResult> DrainPendingAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
        {
            CallCount++;
            await gate;
            return new DrainResult(0, 0);
        }
    }

    private sealed class GatedReconciliationService(Task gate) : IReconciliationService
    {
        public int CallCount { get; private set; }

        public async Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
        {
            CallCount++;
            await gate;
            return new ReconciliationResult(0, 0, 0, 0, 0);
        }

        public Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> CancelPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class GatedHeldChangeService(Task gate) : IHeldChangeService
    {
        public int AcceptAllCallCount { get; private set; }
        public int DeclineAllCallCount { get; private set; }

        public Task<List<HeldChangeDto>> GetHeldAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeDecisionResult> AcceptAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeDecisionResult> DeclineAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();

        public async Task<HeldChangeBulkResult> AcceptAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
        {
            AcceptAllCallCount++;
            await gate;
            return new HeldChangeBulkResult(0, 0);
        }

        public async Task<HeldChangeBulkResult> DeclineAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
        {
            DeclineAllCallCount++;
            await gate;
            return new HeldChangeBulkResult(0, 0);
        }
    }

    private sealed class UnusedEntryPushService : IEntryPushService
    {
        public Task<bool> PushIfPendingAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> PushPendingDeletionAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DrainResult> DrainPendingAsync(IJobProgressSink? progress = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedReconciliationService : IReconciliationService
    {
        public Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> CancelPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedHeldChangeService : IHeldChangeService
    {
        public Task<List<HeldChangeDto>> GetHeldAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeDecisionResult> AcceptAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeDecisionResult> DeclineAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeBulkResult> AcceptAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<HeldChangeBulkResult> DeclineAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedUserAnimeEntryRepository : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeServiceProvider(
        IEntryPushService pushService, IReconciliationService reconciliationService, IHeldChangeService heldChangeService) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IEntryPushService)) return pushService;
            if (serviceType == typeof(IReconciliationService)) return reconciliationService;
            if (serviceType == typeof(IHeldChangeService)) return heldChangeService;
            return null;
        }
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
