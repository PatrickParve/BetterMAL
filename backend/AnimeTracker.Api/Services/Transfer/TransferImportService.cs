using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Transfer;

public class TransferImportService(
    AnimeTrackerDbContext db,
    ListImportProgress listImportProgress,
    IMalTokenStore tokenStore,
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

        // Step 3 (D2 5, D10): the list import's gate.
        var gate = listImportProgress.Gate;
        if (gate.Running)
        {
            var snapshot = listImportProgress.Snapshot;
            if (snapshot.Phase == JobPhase.Running)
            {
                var message = snapshot.Total is { } total
                    ? $"The list is still being imported ({snapshot.Done} / {total})."
                    : "The list is still being imported.";
                throw new TransferImportBlockedException(message);
            }

            throw new TransferImportBlockedException("The list is being checked against MyAnimeList.");
        }

        if (!gate.WentThroughSinceStart)
        {
            var token = await tokenStore.GetAsync(ct);
            if (token is null || token.ConnectionLostAt is not null)
            {
                throw new TransferImportBlockedException(
                    "The list can't be checked while the connection to MyAnimeList is lost. Re-authorize in Settings.");
            }

            throw gate.LastReadFailure is null
                ? new TransferImportBlockedException("The list hasn't finished importing since the app started.")
                : new TransferImportBlockedException(gate.RetryAt is { } retryAt
                    ? $"The list couldn't be read from MyAnimeList. It will try again at {retryAt:u}."
                    : "The list couldn't be read from MyAnimeList. It will try again when the app next starts.");
        }

        // Step 4 (D2 6): another import already pending or running.
        if (!trigger.TryOffer(file))
            throw new TransferImportBlockedException("An import is already running.");

        progress.MarkPending(file.Device.Name, file.ExportedAt);
        return progress.Snapshot;
    }
}
