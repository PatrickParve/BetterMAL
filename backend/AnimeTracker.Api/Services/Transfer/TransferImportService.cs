using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Import;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Transfer;

public class TransferImportService(
    AnimeTrackerDbContext db,
    IImportProgressTracker listImportProgress,
    ITransferImportTrigger trigger,
    ITransferImportProgressTracker progress) : ITransferImportService
{
    public async Task<TransferImportStatusSnapshot> AcceptAsync(byte[] bytes, CancellationToken ct = default)
    {
        // Step 1 (D2 1-3): the reader's own file-shape refusals.
        var file = TransferFileReader.Read(bytes);

        // Step 2 (D2 4): exported from this very device.
        var deviceId = await db.DeviceIdentities.AsNoTracking().Select(d => d.DeviceId).SingleAsync(ct);
        if (file.Device.Id == deviceId)
            throw new TransferFileRefusedException("This file was exported from this device; import it on the other one.");

        // Step 3 (D2 5, D12): the initial list import hasn't finished since the app started.
        var listSnapshot = listImportProgress.Snapshot;
        if (listSnapshot.Phase != ImportPhase.Complete)
        {
            throw listSnapshot.Phase == ImportPhase.Running
                ? new TransferImportBlockedException($"The list is still being imported ({listSnapshot.Synced} / {listSnapshot.Total}).")
                : new TransferImportBlockedException("The list hasn't finished importing since the app started.");
        }

        // Step 4 (D2 6): another import already pending or running.
        if (!trigger.TryOffer(file))
            throw new TransferImportBlockedException("An import is already running.");

        progress.MarkPending(file.Device.Name, file.ExportedAt);
        return progress.Snapshot;
    }
}
