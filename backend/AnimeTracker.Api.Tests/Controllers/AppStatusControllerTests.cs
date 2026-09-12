using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
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
        AnimeTrackerDbContext db, IMalTokenStore tokenStore, ListImportProgress? listImportProgress = null) =>
        new(db, tokenStore, listImportProgress ?? new ListImportProgress(), new SyncNowProgress(), new ReconcileProgress(),
            new HeldDecisionProgress(), new ResyncProgress(), new AiringFullRefreshProgress(), new SeriesBulkBuildProgress(),
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
        Assert.IsType<JobDto>(body.jobs.resync);
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

    private sealed class FakeMalTokenStore(OAuthToken? token) : IMalTokenStore
    {
        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(token);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) => throw new NotImplementedException();
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
        public JobSnapshot ToJobSnapshot() => new(JobPhase.NotStarted, 0, null, null, null, null);
    }
}
