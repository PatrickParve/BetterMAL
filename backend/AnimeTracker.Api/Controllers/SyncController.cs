using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SyncController(
    IEntryPushService pushService,
    IReconciliationService reconciliationService,
    IUserAnimeEntryRepository entryRepository,
    IResyncTrigger resyncTrigger,
    IResyncProgressTracker resyncProgress) : ControllerBase
{
    /// <summary>Sync status for the settings page: how many entries are
    /// currently pending/retrying, and when the most recent push succeeded.</summary>
    [HttpGet("api/sync/status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var (pendingCount, lastSyncedAt) = await entryRepository.GetSyncStatusAsync(ct);
        return Ok(new { pendingCount, lastSyncedAt });
    }

    /// <summary>Manual "sync now" — flushes only pending entries immediately,
    /// without waiting out their debounce timers.</summary>
    [HttpPost("api/sync/now")]
    public async Task<IActionResult> SyncNow(CancellationToken ct)
    {
        var pushed = await pushService.DrainPendingAsync(ct);
        return Ok(new { pushed });
    }

    /// <summary>Manual full reconciliation — pulls the complete MAL list and
    /// computes a diff against local data, held for review (see the
    /// pending/accept/cancel endpoints below).</summary>
    [HttpPost("api/sync/reconcile")]
    public async Task<IActionResult> Reconcile(CancellationToken ct)
    {
        var result = await reconciliationService.RunAsync(ct);
        return Ok(result);
    }

    /// <summary>The most recent reconciliation run's diff still awaiting
    /// review, or 204 if none is pending.</summary>
    [HttpGet("api/sync/reconcile/pending")]
    public async Task<IActionResult> GetPendingDiff(CancellationToken ct)
    {
        var diff = await reconciliationService.GetPendingDiffAsync(ct);
        return diff is null ? NoContent() : Ok(diff);
    }

    /// <summary>Applies every difference in the pending reconciliation diff to
    /// local data and clears it.</summary>
    [HttpPost("api/sync/reconcile/accept")]
    public async Task<IActionResult> AcceptPendingDiff(CancellationToken ct)
    {
        var applied = await reconciliationService.AcceptPendingDiffAsync(ct);
        return applied ? NoContent() : NotFound();
    }

    /// <summary>Discards the pending reconciliation diff without applying
    /// anything; the next reconciliation run computes a fresh one.</summary>
    [HttpPost("api/sync/reconcile/cancel")]
    public async Task<IActionResult> CancelPendingDiff(CancellationToken ct)
    {
        var cancelled = await reconciliationService.CancelPendingDiffAsync(ct);
        return cancelled ? NoContent() : NotFound();
    }

    /// <summary>Kicks off the one-time corrective full re-sync (settings page):
    /// re-fetches full detail for every anime in the MAL list and upserts
    /// AnimeMetadata + UserAnimeEntry, correcting rows imported before
    /// list_status/nsfw/English-title/duration/source were fetched correctly.
    /// Runs in the background (~1 req/s per anime) — poll the status endpoint
    /// below rather than waiting on this call.</summary>
    [HttpPost("api/sync/resync-from-mal")]
    public IActionResult TriggerResyncFromMal()
    {
        resyncTrigger.Signal();
        return Accepted(resyncProgress.Snapshot);
    }

    /// <summary>Progress of the corrective full re-sync, for the settings
    /// page's progress indicator.</summary>
    [HttpGet("api/sync/resync-from-mal/status")]
    public IActionResult GetResyncFromMalStatus()
    {
        var snapshot = resyncProgress.Snapshot;
        return Ok(new
        {
            phase = snapshot.Phase.ToString(),
            synced = snapshot.Synced,
            total = snapshot.Total,
        });
    }
}
