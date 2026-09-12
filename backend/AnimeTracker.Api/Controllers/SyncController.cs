using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Sync;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SyncController(
    IReconciliationService reconciliationService,
    IHeldChangeService heldChangeService,
    IUserAnimeEntryRepository entryRepository,
    IResyncTrigger resyncTrigger,
    ResyncProgress resyncProgress,
    BackgroundJobRunner runner,
    SyncNowProgress syncNowProgress,
    ReconcileProgress reconcileProgress,
    HeldDecisionProgress heldDecisionProgress) : ControllerBase
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
    /// without waiting out their debounce timers. Runs in the background,
    /// starting once (background-jobs "A job is started once"); the page
    /// reads its progress from the combined status read.</summary>
    [HttpPost("api/sync/now")]
    public IActionResult SyncNow()
    {
        runner.TryStart(syncNowProgress, async (sp, sink, ct) =>
        {
            var pushService = sp.GetRequiredService<IEntryPushService>();
            var result = await pushService.DrainPendingAsync(sink, ct);
            if (result.NotPushed > 0)
                syncNowProgress.Fail($"{result.NotPushed} of {result.Pushed + result.NotPushed} couldn't be sent; they'll be retried automatically.");
        });

        return Accepted(JobDto.From(syncNowProgress.Snapshot));
    }

    /// <summary>Manual full reconciliation — pulls the complete MAL list and
    /// computes a diff against local data, held for review (see the
    /// pending/accept/cancel endpoints below). Runs in the background,
    /// waiting for the weekly run if one is already in progress (design.md
    /// D6), and starting once (background-jobs "A job is started once").</summary>
    [HttpPost("api/sync/reconcile")]
    public IActionResult Reconcile()
    {
        runner.TryStart(reconcileProgress, async (sp, sink, ct) =>
        {
            var service = sp.GetRequiredService<IReconciliationService>();
            await service.RunAsync(sink, ct);
        });

        return Accepted(JobDto.From(reconcileProgress.Snapshot));
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
    /// MyAnimeList immediately. 404 if that anime has nothing held; 409 while
    /// accept-all/decline-all is deciding every held change (design.md
    /// D18).</summary>
    [HttpPost("api/sync/held/{animeId:int}/accept")]
    public async Task<IActionResult> AcceptHeld(int animeId, CancellationToken ct)
    {
        if (HeldDecisionRunning() is { } busy)
            return busy;

        var result = await heldChangeService.AcceptAsync(animeId, ct);
        return HeldDecisionResponse(result);
    }

    /// <summary>Declines one held change: discards it and adopts MyAnimeList's
    /// current value instead. 404 if that anime has nothing held; 409 while
    /// accept-all/decline-all is deciding every held change (design.md
    /// D18).</summary>
    [HttpPost("api/sync/held/{animeId:int}/decline")]
    public async Task<IActionResult> DeclineHeld(int animeId, CancellationToken ct)
    {
        if (HeldDecisionRunning() is { } busy)
            return busy;

        var result = await heldChangeService.DeclineAsync(animeId, ct);
        return HeldDecisionResponse(result);
    }

    /// <summary>Accepts every currently held change, one at a time; a
    /// per-item failure leaves that item held rather than aborting the rest.
    /// Runs in the background as one job shared with decline-all (design.md
    /// D18), starting once.</summary>
    [HttpPost("api/sync/held/accept")]
    public IActionResult AcceptAllHeld()
    {
        runner.TryStart(heldDecisionProgress, () => heldDecisionProgress.TryBegin(HeldDecisionAction.Accept), async (sp, sink, ct) =>
        {
            var service = sp.GetRequiredService<IHeldChangeService>();
            var result = await service.AcceptAllAsync(sink, ct);
            if (result.StillHeld > 0)
                heldDecisionProgress.Fail($"{result.StillHeld} of {result.Succeeded + result.StillHeld} couldn't be applied and are still held.");
        });

        return Accepted(HeldDecisionJobDto.From(heldDecisionProgress));
    }

    /// <summary>Declines every currently held change, one at a time; a
    /// per-item failure leaves that item held rather than aborting the rest.
    /// Runs in the background as one job shared with accept-all (design.md
    /// D18), starting once.</summary>
    [HttpPost("api/sync/held/decline")]
    public IActionResult DeclineAllHeld()
    {
        runner.TryStart(heldDecisionProgress, () => heldDecisionProgress.TryBegin(HeldDecisionAction.Decline), async (sp, sink, ct) =>
        {
            var service = sp.GetRequiredService<IHeldChangeService>();
            var result = await service.DeclineAllAsync(sink, ct);
            if (result.StillHeld > 0)
                heldDecisionProgress.Fail($"{result.StillHeld} of {result.Succeeded + result.StillHeld} couldn't be applied and are still held.");
        });

        return Accepted(HeldDecisionJobDto.From(heldDecisionProgress));
    }

    /// <summary>409 while accept-all/decline-all is running, refusing a
    /// single decision without acting (design.md D18); null otherwise.</summary>
    private IActionResult? HeldDecisionRunning() =>
        heldDecisionProgress.Snapshot.Phase == JobPhase.Running
            ? Conflict(new { error = "Held changes are being decided; try again when that finishes." })
            : null;

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
    /// Runs in the background (~1 req/s per anime) — starts once
    /// (background-jobs "A job is started once") and the answer already
    /// reports it as running either way, so the page reads its progress from
    /// the combined status read rather than polling this endpoint.</summary>
    [HttpPost("api/sync/resync-from-mal")]
    public IActionResult TriggerResyncFromMal()
    {
        if (resyncProgress.TryBegin())
            resyncTrigger.Signal();

        return Accepted(JobDto.From(resyncProgress.Snapshot));
    }
}
