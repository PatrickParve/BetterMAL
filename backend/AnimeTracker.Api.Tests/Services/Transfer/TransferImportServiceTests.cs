using System.Text;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportService.AcceptAsync (device-transfer design.md D2/D12,
// report-jobs-and-lost-mal-connection design.md D10, tasks.md 5.1-5.3, 6.4):
// the refusals, in order, each writing nothing and offering no file, and the
// accepted path that reaches the trigger once the list import's gate has
// opened — even a run that ended with fetch failures still opens it.
public class TransferImportServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Guid SeedDevice(AnimeTrackerDbContext db)
    {
        var id = Guid.NewGuid();
        db.DeviceIdentities.Add(new DeviceIdentity { DeviceId = id });
        return id;
    }

    private static byte[] ValidFileBytes(Guid deviceId, string deviceName = "Other Device") =>
        Encoding.UTF8.GetBytes($$"""
        {
          "formatVersion": 1,
          "device": {"id": "{{deviceId}}", "name": "{{deviceName}}"},
          "exportedAt": "2026-01-01T00:00:00Z",
          "ranking": {"animeIds": [], "modifiedAt": null},
          "animePictures": [],
          "series": [],
          "activity": []
        }
        """);

    // A gate that has already opened and is currently quiet — the default
    // "nothing in the way" state for tests that aren't about the gate itself.
    private static ListImportProgress GoneThroughGate()
    {
        var progress = new ListImportProgress();
        progress.BeginRun(visibleFromStart: false);
        progress.EndWithoutWork();
        return progress;
    }

    private static OAuthToken HealthyToken() => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        UpdatedAt = DateTimeOffset.UtcNow,
        ConnectionLostAt = null,
    };

    private static TransferImportService CreateService(
        AnimeTrackerDbContext db, ITransferImportTrigger trigger,
        ListImportProgress? listImportProgress = null, OAuthToken? token = null) =>
        new(db, listImportProgress ?? GoneThroughGate(), new FakeMalTokenStore(token ?? HealthyToken()), trigger, new TransferImportProgressTracker());

    private sealed class RecordingTrigger : ITransferImportTrigger
    {
        public TransferFile? Offered { get; private set; }
        public bool ShouldAccept { get; set; } = true;

        public bool TryOffer(TransferFile file)
        {
            if (!ShouldAccept) return false;
            Offered = file;
            return true;
        }

        public Task<TransferFile> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
        public void Release() => throw new NotImplementedException();
    }

    private sealed class FakeMalTokenStore(OAuthToken? token) : IMalTokenStore
    {
        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(token);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    // --- Refusals that come before the gate ---

    [Fact]
    public async Task AcceptAsync_ADamagedFileIsRefusedBeforeAnythingElseIsChecked()
    {
        using var db = CreateDb();
        // No device seeded at all — if the reader's refusal didn't come
        // first, the device-id lookup below would throw for a different reason.
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger);

        await Assert.ThrowsAsync<TransferFileRefusedException>(
            () => service.AcceptAsync(Encoding.UTF8.GetBytes("not json")));

        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_AFileFromThisDeviceIsRefused()
    {
        using var db = CreateDb();
        var deviceId = SeedDevice(db);
        await db.SaveChangesAsync();
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger);

        var ex = await Assert.ThrowsAsync<TransferFileRefusedException>(
            () => service.AcceptAsync(ValidFileBytes(deviceId)));

        Assert.Contains("this device", ex.Message);
        Assert.Null(trigger.Offered);
    }

    // --- The list import's gate (design.md D10) ---

    [Fact]
    public async Task AcceptAsync_RefusedWhileTheListIsStillBeingImportedAndShown()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: true);
        listImportProgress.SetTotal(380);
        listImportProgress.ReportProgress(142);

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Contains("142", ex.Message);
        Assert.Contains("380", ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhileTheListIsRunningButQuiet()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        // Running, but hasn't found work yet, so the page shows nothing —
        // the gate still refuses, with a different message than a shown run.
        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: false);

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal("The list is being checked against MyAnimeList.", ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhileTheConnectionToMalIsLost()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        // No run has gone through yet, and the stored login is lost.
        var listImportProgress = new ListImportProgress();
        var lostToken = HealthyToken();
        lostToken.ConnectionLostAt = DateTimeOffset.UtcNow;

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress, lostToken);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal(
            "The list can't be checked while the connection to MyAnimeList is lost. Re-authorize in Settings.",
            ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhenNoTokenIsStoredEitherAndNoRunHasGoneThrough()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        // Construct directly rather than through CreateService: its `token
        // ?? HealthyToken()` default can't represent "explicitly no token".
        var listImportProgress = new ListImportProgress();
        var trigger = new RecordingTrigger();
        var service = new TransferImportService(
            db, listImportProgress, new FakeMalTokenStore(null), trigger, new TransferImportProgressTracker());

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal(
            "The list can't be checked while the connection to MyAnimeList is lost. Re-authorize in Settings.",
            ex.Message);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhenTheListHasNeverFinishedImportingSinceStart()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        var listImportProgress = new ListImportProgress(); // fresh: no run has ever gone through
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal("The list hasn't finished importing since the app started.", ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWithTheRetryTimeWhenTheLastReadFailedAndARetryIsPlanned()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: false);
        listImportProgress.FailBeforeRead("MyAnimeList couldn't be reached.");
        var retryAt = DateTimeOffset.UtcNow.AddMinutes(5);
        listImportProgress.SetRetryAt(retryAt);

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal($"The list couldn't be read from MyAnimeList. It will try again at {retryAt:u}.", ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedSayingItWillTryAgainOnNextStartWhenNoRetryIsPlanned()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: false);
        listImportProgress.FailBeforeRead("MyAnimeList couldn't be reached."); // no SetRetryAt: no more retries planned

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Equal(
            "The list couldn't be read from MyAnimeList. It will try again when the app next starts.",
            ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhileAnotherImportIsAlreadyRunning()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();
        var trigger = new RecordingTrigger { ShouldAccept = false };
        var service = CreateService(db, trigger);

        await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));
    }

    // --- Accepted ---

    [Fact]
    public async Task AcceptAsync_AnAcceptedFileReachesTheTriggerAndTheStatusReadsRunning()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger);

        var status = await service.AcceptAsync(ValidFileBytes(Guid.NewGuid(), "Other Device"));

        Assert.NotNull(trigger.Offered);
        Assert.Equal(TransferImportPhase.Running, status.Phase);
        Assert.Equal("Other Device", status.DeviceName);
    }

    // Found on the first real run of add-first-run-setup: the import service skips its start-up run
    // in the process that waited for setup, so nothing opened this gate and the file import refused
    // with "The list hasn't finished importing since the app started" until the next restart.
    [Fact]
    public async Task AcceptAsync_AcceptsOnceSetupHasReadTheListWithNoImportRunEver()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        var listImportProgress = new ListImportProgress(); // no run has begun in this process
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);
        await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        listImportProgress.MarkListReadBySetup();
        var status = await service.AcceptAsync(ValidFileBytes(Guid.NewGuid(), "Other Device"));

        Assert.NotNull(trigger.Offered);
        Assert.Equal(TransferImportPhase.Running, status.Phase);
    }

    [Fact]
    public async Task AcceptAsync_AGateThatWentThroughWithFetchFailuresStillAccepts()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();

        // The list import ran, read the whole list, but couldn't fetch every
        // anime — the gate still counts it as gone through (design.md D8
        // point 6 / D10): those anime surface as failures in the file
        // import's own report instead of blocking it.
        var listImportProgress = new ListImportProgress();
        listImportProgress.BeginRun(visibleFromStart: false);
        listImportProgress.Reveal(2);
        listImportProgress.Fail("1 of 2 anime couldn't be fetched.");

        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, listImportProgress);

        var status = await service.AcceptAsync(ValidFileBytes(Guid.NewGuid(), "Other Device"));

        Assert.NotNull(trigger.Offered);
        Assert.Equal(TransferImportPhase.Running, status.Phase);
    }
}
