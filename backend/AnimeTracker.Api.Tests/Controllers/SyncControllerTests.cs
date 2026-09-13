using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Controllers;

// SyncController's job-starting endpoints (background-jobs "A job is started
// once"/design.md D2, D4, D18): sync now, reconciliation and the
// held-decision pair start once through BackgroundJobRunner and answer 202
// with the job's state either way; a single held decision is refused with
// 409 while the bulk pair is deciding.
public class SyncControllerTests
{
    private static SyncController CreateController(
        IReconciliationService? reconciliationService = null,
        IHeldChangeService? heldChangeService = null,
        IEntryPushService? pushService = null,
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
            runner ?? new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(pushService, reconciliationService, heldChangeService))),
            syncNowProgress ?? new SyncNowProgress(),
            reconcileProgress ?? new ReconcileProgress(),
            heldDecisionProgress ?? new HeldDecisionProgress());
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
    public async Task ReconcileEndsFailedWhenTheRunSkippedUnrecognizedStatuses()
    {
        var reconciliationService = new StubReconciliationService(skippedUnrecognized: 1);
        var reconcileProgress = new ReconcileProgress();
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(new UnusedEntryPushService(), reconciliationService, new UnusedHeldChangeService())));
        await runner.StartAsync(CancellationToken.None);

        var controller = CreateController(reconciliationService: reconciliationService, runner: runner, reconcileProgress: reconcileProgress);
        controller.Reconcile();
        await runner.StopAsync(CancellationToken.None);

        Assert.Equal(JobPhase.Failed, reconcileProgress.Snapshot.Phase);
        Assert.Equal(JobFailure.UnrecognizedStatuses(1), reconcileProgress.Snapshot.Error);
    }

    [Fact]
    public async Task ReconcileEndsCompleteWhenNothingWasSkipped()
    {
        var reconciliationService = new StubReconciliationService(skippedUnrecognized: 0);
        var reconcileProgress = new ReconcileProgress();
        var runner = new BackgroundJobRunner(new FakeServiceScopeFactory(new FakeServiceProvider(new UnusedEntryPushService(), reconciliationService, new UnusedHeldChangeService())));
        await runner.StartAsync(CancellationToken.None);

        var controller = CreateController(reconciliationService: reconciliationService, runner: runner, reconcileProgress: reconcileProgress);
        controller.Reconcile();
        await runner.StopAsync(CancellationToken.None);

        Assert.Equal(JobPhase.Complete, reconcileProgress.Snapshot.Phase);
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
            return new ReconciliationResult(0, 0, 0, 0, 0, 0, 0);
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

    private sealed class StubReconciliationService(int skippedUnrecognized) : IReconciliationService
    {
        public Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default) =>
            Task.FromResult(new ReconciliationResult(0, 0, 0, 0, 0, 0, skippedUnrecognized));

        public Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> CancelPendingDiffAsync(CancellationToken ct = default) => throw new NotImplementedException();
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
