using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SyncController(
    IEntryPushService pushService,
    IReconciliationService reconciliationService,
    IHeldChangeService heldChangeService,
    IUserAnimeEntryRepository entryRepository,
    IResyncTrigger resyncTrigger,
    IResyncProgressTracker resyncProgress) : ControllerBase
{
    /// <summary>Sync status for the settings page: how many entries are
    /// currently pending/retrying, how many are held for review, and when the
    /// most recent push succeeded.</summary>
    [HttpGet("api/sync/status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var (pendingCount, heldCount, lastSyncedAt) = await entryRepository.GetSyncStatusAsync(ct);
        return Ok(new { pendingCount, heldCount, lastSyncedAt });
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

    /// <summary>Every change held for review since a previous process
    /// start — entries and queued removals together, each tagged with its
    /// kind — or an empty list. Empty rather than 204, since the settings
    /// page also uses the count. An item MyAnimeList already agrees with is
    /// cleared as a side effect of this read and omitted (design.md D8a).</summary>
    [HttpGet("api/sync/held")]
    public async Task<IActionResult> GetHeld(CancellationToken ct)
    {
        var held = await heldChangeService.GetHeldAsync(ct);
        return Ok(held);
    }

    /// <summary>Accepts one held change: releases its hold and pushes it to
    /// MyAnimeList immediately. 404 if that anime has nothing held.</summary>
    [HttpPost("api/sync/held/{animeId:int}/accept")]
    public async Task<IActionResult> AcceptHeld(int animeId, CancellationToken ct)
    {
        var result = await heldChangeService.AcceptAsync(animeId, ct);
        return HeldDecisionResponse(result);
    }

    /// <summary>Declines one held change: discards it and adopts MyAnimeList's
    /// current value instead. 404 if that anime has nothing held.</summary>
    [HttpPost("api/sync/held/{animeId:int}/decline")]
    public async Task<IActionResult> DeclineHeld(int animeId, CancellationToken ct)
    {
        var result = await heldChangeService.DeclineAsync(animeId, ct);
        return HeldDecisionResponse(result);
    }

    /// <summary>Accepts every currently held change, one at a time; a
    /// per-item failure leaves that item held rather than aborting the rest.</summary>
    [HttpPost("api/sync/held/accept")]
    public async Task<IActionResult> AcceptAllHeld(CancellationToken ct)
    {
        var result = await heldChangeService.AcceptAllAsync(ct);
        return Ok(result);
    }

    /// <summary>Declines every currently held change, one at a time; a
    /// per-item failure leaves that item held rather than aborting the rest.</summary>
    [HttpPost("api/sync/held/decline")]
    public async Task<IActionResult> DeclineAllHeld(CancellationToken ct)
    {
        var result = await heldChangeService.DeclineAllAsync(ct);
        return Ok(result);
    }

    private IActionResult HeldDecisionResponse(HeldChangeDecisionResult result) => result.Outcome switch
    {
        HeldChangeDecisionOutcome.NotHeld => NotFound(),
        HeldChangeDecisionOutcome.Failed => Ok(new { applied = false, error = result.Error }),
        _ => Ok(new { applied = true }),
    };

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
