using System.Text;
using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Transfer;

// TransferImportService.AcceptAsync (device-transfer design.md D2/D12,
// tasks.md 5.1-5.3): the refusals, in order, each writing nothing and
// offering no file, and the accepted path that reaches the trigger.
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

    private static TransferImportService CreateService(
        AnimeTrackerDbContext db, ITransferImportTrigger trigger, ImportPhase listPhase = ImportPhase.Complete, int synced = 0, int total = 0) =>
        new(db, new FakeImportProgressTracker(new ImportStatusSnapshot(listPhase, synced, total)), trigger, new TransferImportProgressTracker());

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

    private sealed class FakeImportProgressTracker(ImportStatusSnapshot snapshot) : IImportProgressTracker
    {
        public ImportStatusSnapshot Snapshot => snapshot;
        public void Start(int total) => throw new NotImplementedException();
        public void ReportProgress(int synced) => throw new NotImplementedException();
        public void Complete() => throw new NotImplementedException();
    }

    // --- Order and refusals ---

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

    [Fact]
    public async Task AcceptAsync_RefusedWhileTheListIsStillBeingImported()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, ImportPhase.Running, synced: 142, total: 380);

        var ex = await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

        Assert.Contains("142", ex.Message);
        Assert.Contains("380", ex.Message);
        Assert.Null(trigger.Offered);
    }

    [Fact]
    public async Task AcceptAsync_RefusedWhenTheListHasNeverFinishedImportingSinceStart()
    {
        using var db = CreateDb();
        SeedDevice(db);
        await db.SaveChangesAsync();
        var trigger = new RecordingTrigger();
        var service = CreateService(db, trigger, ImportPhase.NotStarted);

        await Assert.ThrowsAsync<TransferImportBlockedException>(
            () => service.AcceptAsync(ValidFileBytes(Guid.NewGuid())));

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
}
