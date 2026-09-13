using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// design.md D7: the weekly run persists its outcome on ReconciliationRunLog
// so a mostly-idle machine still has something to show after most restarts —
// false plus no error on success, true plus the reason on failure.
public class ReconciliationBackgroundServiceTests
{
    // Each fake scope opens its own DbContext against this same in-memory
    // database name (mirroring real per-scope resolution), so polling from
    // the test never shares a DbContext instance with the service's own
    // concurrent work.
    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static async Task<ReconciliationRunLog?> WaitForOutcomeAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReconciliationRunLog? log = null;
        while (!cts.IsCancellationRequested)
        {
            using var db = new AnimeTrackerDbContext(options);
            log = await db.ReconciliationRunLogs.AsNoTracking().FirstOrDefaultAsync();
            if (log?.LastRunFailed is not null)
                break;
            await Task.Delay(10, CancellationToken.None);
        }
        return log;
    }

    [Fact]
    public async Task RecordsLastRunFailedFalseOnSuccess()
    {
        var options = CreateOptions();
        var service = new ReconciliationBackgroundService(
            new FakeServiceScopeFactory(options, new StubReconciliationService(ex: null)),
            NullLogger<ReconciliationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var log = await WaitForOutcomeAsync(options);

            Assert.NotNull(log);
            Assert.False(log!.LastRunFailed);
            Assert.Null(log.LastRunError);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task RecordsLastRunFailedTrueAndTheReasonOnFailure()
    {
        var options = CreateOptions();
        var service = new ReconciliationBackgroundService(
            new FakeServiceScopeFactory(options, new StubReconciliationService(ex: new HttpRequestException("boom"))),
            NullLogger<ReconciliationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var log = await WaitForOutcomeAsync(options);

            Assert.NotNull(log);
            Assert.True(log!.LastRunFailed);
            Assert.Equal("MyAnimeList couldn't be reached.", log.LastRunError);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task ANewAttemptResetsLastRunOutcomeSeenAlongWithTheOutcomeFields()
    {
        var options = CreateOptions();
        var seededLastRunAt = DateTimeOffset.UtcNow.AddDays(-8); // overdue, so the service runs immediately
        using (var seedDb = new AnimeTrackerDbContext(options))
        {
            seedDb.ReconciliationRunLogs.Add(new ReconciliationRunLog
            {
                LastRunAt = seededLastRunAt,
                LastRunFailed = true,
                LastRunError = "previous failure",
                LastRunOutcomeSeen = true,
            });
            await seedDb.SaveChangesAsync();
        }

        var service = new ReconciliationBackgroundService(
            new FakeServiceScopeFactory(options, new StubReconciliationService(ex: null)),
            NullLogger<ReconciliationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            // WaitForOutcomeAsync would match the seeded row's already-non-null
            // LastRunFailed before the service resets anything, so wait for the
            // new attempt's own LastRunAt instead.
            var log = await WaitForNewOutcomeAsync(options, seededLastRunAt);

            Assert.NotNull(log);
            Assert.False(log!.LastRunOutcomeSeen);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private static async Task<ReconciliationRunLog?> WaitForNewOutcomeAsync(DbContextOptions<AnimeTrackerDbContext> options, DateTimeOffset previousLastRunAt)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        ReconciliationRunLog? log = null;
        while (!cts.IsCancellationRequested)
        {
            using var db = new AnimeTrackerDbContext(options);
            log = await db.ReconciliationRunLogs.AsNoTracking().FirstOrDefaultAsync();
            if (log is not null && log.LastRunAt != previousLastRunAt && log.LastRunFailed is not null)
                break;
            await Task.Delay(10, CancellationToken.None);
        }
        return log;
    }

    [Fact]
    public async Task ARunThatSkippedUnrecognizedStatusesRecordsFailedWithTheirReason()
    {
        var options = CreateOptions();
        var service = new ReconciliationBackgroundService(
            new FakeServiceScopeFactory(options, new StubReconciliationService(ex: null, skippedUnrecognized: 2)),
            NullLogger<ReconciliationBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            var log = await WaitForOutcomeAsync(options);

            Assert.NotNull(log);
            Assert.True(log!.LastRunFailed);
            Assert.Equal(JobFailure.UnrecognizedStatuses(2), log.LastRunError);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class StubReconciliationService(Exception? ex, int skippedUnrecognized = 0) : IReconciliationService
    {
        public Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default) =>
            ex is null ? Task.FromResult(new ReconciliationResult(0, 0, 0, 0, 0, 0, skippedUnrecognized)) : throw ex;

        public Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<bool> CancelPendingDiffAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeServiceScopeFactory(DbContextOptions<AnimeTrackerDbContext> options, IReconciliationService reconciliation) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(options, reconciliation);
    }

    private sealed class FakeServiceScope : IServiceScope
    {
        private readonly AnimeTrackerDbContext _db;
        public IServiceProvider ServiceProvider { get; }

        public FakeServiceScope(DbContextOptions<AnimeTrackerDbContext> options, IReconciliationService reconciliation)
        {
            _db = new AnimeTrackerDbContext(options);
            ServiceProvider = new FakeServiceProvider(_db, reconciliation);
        }

        public void Dispose() => _db.Dispose();
    }

    private sealed class FakeServiceProvider(AnimeTrackerDbContext db, IReconciliationService reconciliation) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(AnimeTrackerDbContext)) return db;
            if (serviceType == typeof(IReconciliationService)) return reconciliation;
            return null;
        }
    }
}
