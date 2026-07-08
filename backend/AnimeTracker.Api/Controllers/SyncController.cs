using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SyncController(IEntryPushService pushService, IReconciliationService reconciliationService) : ControllerBase
{
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
}
