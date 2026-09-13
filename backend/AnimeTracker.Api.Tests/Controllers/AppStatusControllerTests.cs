using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Controllers;

// AppStatusController (design.md D15): one cheap read of every job's state,
// the MyAnimeList connection state and the weekly check's last outcome — the
// in-memory tracker snapshots plus two single-row reads, and never MAL
// itself.
public class AppStatusControllerTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AppStatusController CreateController(
        AnimeTrackerDbContext db, IMalTokenStore tokenStore, ListImportProgress? listImportProgress = null,
        IUserAnimeEntryRepository? entryRepository = null, SyncNowProgress? syncNowProgress = null) =>
        new(db, tokenStore, entryRepository ?? new FakeUserAnimeEntryRepository(0, 0, null),
            listImportProgress ?? new ListImportProgress(), syncNowProgress ?? new SyncNowProgress(), new ReconcileProgress(),
            new HeldDecisionProgress(), new AiringFullRefreshProgress(), new SeriesBulkBuildProgress(),
            new FakeTransferImportProgressTracker());

    [Fact]
    public async Task TheResponseCarriesEveryJobKeyMalConnectionAndWeeklyCheck()
    {
        using var db = CreateDb();
        var tokenStore = new FakeMalTokenStore(new OAuthToken { AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });
        var controller = CreateController(db, tokenStore);

        var result = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic body = result.Value!;

        Assert.NotNull(body.malConnection);
        Assert.Null(body.weeklyCheck); // no ReconciliationRunLog row exists yet

        Assert.IsType<JobDto>(body.jobs.listImport);
        Assert.IsType<JobDto>(body.jobs.syncNow);
        Assert.IsType<JobDto>(body.jobs.reconcile);
        Assert.IsType<HeldDecisionJobDto>(body.jobs.heldDecision);
        Assert.IsType<JobDto>(body.jobs.airingRefresh);
        Assert.IsType<JobDto>(body.jobs.seriesBuild);
        Assert.IsType<JobDto>(body.jobs.fileImport);
    }

    [Fact]
    public async Task AQuietListImportReadsNotStarted()
    {
        using var db = CreateDb();
        var tokenStore = new FakeMalTokenStore(null);
        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: false); // running, but not yet revealed as having work
        var controller = CreateController(db, tokenStore, listImportProgress);

        var result = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic body = result.Value!;

        Assert.Equal("NotStarted", (string)body.jobs.listImport.Phase);
    }

    [Fact]
    public async Task ALostTokenRowReadsLostWithLostAt()
    {
        using var db = CreateDb();
        var lostAt = DateTimeOffset.UtcNow.AddHours(-2);
        var tokenStore = new FakeMalTokenStore(new OAuthToken
        {
            AccessToken = "a", RefreshToken = "r", ExpiresAt = DateTimeOffset.UtcNow.AddDays(1), ConnectionLostAt = lostAt,
        });
        var controller = CreateController(db, tokenStore);

        var result = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic body = result.Value!;

        Assert.Equal("Lost", (string)body.malConnection.state);
        Assert.Equal(lostAt, (DateTimeOffset)body.malConnection.lostAt);
    }

    [Fact]
    public async Task NoMalClientIsResolvedAndTheTokenStoreFakeThrowsIfAnythingElseIsCalled()
    {
        // AppStatusController never calls MyAnimeList (design.md D15) — it
        // has no IMalClient dependency at all, and its only call into the
        // token store is the plain read below. FakeMalTokenStore's other
        // members throw, so a regression that tried to save or mark the
        // connection lost from this read path would fail loudly.
        var ctorParams = typeof(AppStatusController).GetConstructors().Single().GetParameters();
        Assert.DoesNotContain(ctorParams, p => p.ParameterType == typeof(IMalClient));

        using var db = CreateDb();
        var tokenStore = new FakeMalTokenStore(null);
        var controller = CreateController(db, tokenStore);

        var result = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic body = result.Value!;
        Assert.Equal("NotConnected", (string)body.malConnection.state);
    }

    // design.md D1/D15: the sync block comes from the same GetSyncStatusAsync
    // GET api/sync/status used, and outcomeSeen rides every job DTO and the
    // weekly check.
    [Fact]
    public async Task TheResponseCarriesSyncAndOutcomeSeenOnEveryJobAndTheWeeklyCheck()
    {
        using var db = CreateDb();
        db.ReconciliationRunLogs.Add(new ReconciliationRunLog
        {
            LastRunAt = DateTimeOffset.UtcNow, LastRunFailed = true, LastRunError = "boom", LastRunOutcomeSeen = true,
        });
        await db.SaveChangesAsync();
        var tokenStore = new FakeMalTokenStore(null);
        var lastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var controller = CreateController(db, tokenStore, entryRepository: new FakeUserAnimeEntryRepository(3, 2, lastSyncedAt));

        var result = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic body = result.Value!;

        Assert.Equal(3, (int)body.sync.pendingCount);
        Assert.Equal(2, (int)body.sync.heldCount);
        Assert.Equal(lastSyncedAt, (DateTimeOffset?)body.sync.lastSyncedAt);
        Assert.False((bool)body.sync.diffPending);

        Assert.False(((JobDto)body.jobs.listImport).OutcomeSeen);
        Assert.False(((JobDto)body.jobs.syncNow).OutcomeSeen);
        Assert.False(((JobDto)body.jobs.reconcile).OutcomeSeen);
        Assert.False(((HeldDecisionJobDto)body.jobs.heldDecision).OutcomeSeen);
        Assert.False(((JobDto)body.jobs.airingRefresh).OutcomeSeen);
        Assert.False(((JobDto)body.jobs.seriesBuild).OutcomeSeen);
        Assert.False(((JobDto)body.jobs.fileImport).OutcomeSeen);

        Assert.True((bool)body.weeklyCheck.outcomeSeen);
    }

    [Fact]
    public async Task DiffPendingIsTrueOnlyWhileAPendingReconciliationDiffRowExists()
    {
        using var db = CreateDb();
        var controller = CreateController(db, new FakeMalTokenStore(null));

        var beforeResult = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic before = beforeResult.Value!;
        Assert.False((bool)before.sync.diffPending);

        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff { ComputedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var afterResult = Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None));
        dynamic after = afterResult.Value!;
        Assert.True((bool)after.sync.diffPending);
    }

    [Fact]
    public async Task MarkSeenMarksANamedJobsOutcome()
    {
        using var db = CreateDb();
        var syncNowProgress = new SyncNowProgress();
        syncNowProgress.TryBegin();
        syncNowProgress.Complete();
        var finishedAt = syncNowProgress.Snapshot.FinishedAt!.Value;
        var controller = CreateController(db, new FakeMalTokenStore(null), syncNowProgress: syncNowProgress);

        var result = await controller.MarkSeen(new AppStatusSeenRequest([new AppStatusSeenJob("syncNow", finishedAt)], null), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(syncNowProgress.Snapshot.OutcomeSeen);
    }

    [Fact]
    public async Task MarkSeenWithAStaleFinishedAtMarksNothingAndStillAnswers204()
    {
        using var db = CreateDb();
        var syncNowProgress = new SyncNowProgress();
        syncNowProgress.TryBegin();
        syncNowProgress.Complete();
        var controller = CreateController(db, new FakeMalTokenStore(null), syncNowProgress: syncNowProgress);

        var result = await controller.MarkSeen(
            new AppStatusSeenRequest([new AppStatusSeenJob("syncNow", DateTimeOffset.UtcNow.AddMinutes(-5))], null), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.False(syncNowProgress.Snapshot.OutcomeSeen);
    }

    [Fact]
    public async Task MarkSeenWithAnUnknownNameAnswers400()
    {
        using var db = CreateDb();
        var controller = CreateController(db, new FakeMalTokenStore(null));

        var result = await controller.MarkSeen(
            new AppStatusSeenRequest([new AppStatusSeenJob("notARealJob", DateTimeOffset.UtcNow)], null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task MarkSeenMarksTheWeeklyCheckOnlyOnAMatchingLastRunAt()
    {
        using var db = CreateDb();
        var lastRunAt = DateTimeOffset.UtcNow;
        db.ReconciliationRunLogs.Add(new ReconciliationRunLog { LastRunAt = lastRunAt, LastRunFailed = true, LastRunError = "boom" });
        await db.SaveChangesAsync();
        var controller = CreateController(db, new FakeMalTokenStore(null));

        await controller.MarkSeen(new AppStatusSeenRequest(null, lastRunAt.AddMinutes(-1)), CancellationToken.None); // stale
        Assert.False((await db.ReconciliationRunLogs.AsNoTracking().FirstAsync()).LastRunOutcomeSeen);

        await controller.MarkSeen(new AppStatusSeenRequest(null, lastRunAt), CancellationToken.None); // matches
        Assert.True((await db.ReconciliationRunLogs.AsNoTracking().FirstAsync()).LastRunOutcomeSeen);
    }

    private sealed class FakeMalTokenStore(OAuthToken? token) : IMalTokenStore
    {
        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(token);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeUserAnimeEntryRepository(int pendingCount, int heldCount, DateTimeOffset? lastSyncedAt) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            Task.FromResult((pendingCount, heldCount, lastSyncedAt));
    }

    private sealed class FakeTransferImportProgressTracker : ITransferImportProgressTracker
    {
        public TransferImportStatusSnapshot Snapshot { get; } = new(TransferImportPhase.NotStarted, 0, 0, null, null, null, null);
        public void MarkPending(string? deviceName, DateTimeOffset exportedAt) => throw new NotImplementedException();
        public void Start(int total) => throw new NotImplementedException();
        public void AddToTotal(int n) => throw new NotImplementedException();
        public void ReportProgress(int done) => throw new NotImplementedException();
        public void Complete(TransferImportReport report) => throw new NotImplementedException();
        public void Fail(string reason) => throw new NotImplementedException();
        public void MarkOutcomeSeen(DateTimeOffset finishedAt) => throw new NotImplementedException();
        public JobSnapshot ToJobSnapshot() => new(JobPhase.NotStarted, 0, null, null, null, null);
    }
}
