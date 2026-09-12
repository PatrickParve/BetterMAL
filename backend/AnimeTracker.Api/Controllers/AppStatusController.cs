using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Controllers;

public record AppStatusSeenJob(string Name, DateTimeOffset FinishedAt);

public record AppStatusSeenRequest(IReadOnlyList<AppStatusSeenJob>? Jobs, DateTimeOffset? WeeklyCheckLastRunAt);

/// <summary>One cheap read of every job's state, the MyAnimeList connection
/// state, the weekly check's last outcome, and what's waiting for a decision
/// (design.md D1, D15): the in-memory tracker snapshots, plus a handful of
/// indexed, single-table reads of its own database. Never calls MyAnimeList
/// or AniList, so it's cheap enough for the Settings page and the navbar to
/// repeat every second while a job runs.</summary>
[ApiController]
public class AppStatusController(
    AnimeTrackerDbContext db,
    IMalTokenStore tokenStore,
    IUserAnimeEntryRepository entryRepository,
    ListImportProgress listImportProgress,
    SyncNowProgress syncNowProgress,
    ReconcileProgress reconcileProgress,
    HeldDecisionProgress heldDecisionProgress,
    ResyncProgress resyncProgress,
    AiringFullRefreshProgress airingRefreshProgress,
    SeriesBulkBuildProgress seriesBuildProgress,
    ITransferImportProgressTracker transferImportProgress) : ControllerBase
{
    [HttpGet("api/app-status")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var token = await tokenStore.GetAsync(ct);
        var malConnection = new
        {
            state = token is null ? "NotConnected" : token.ConnectionLostAt is not null ? "Lost" : "Connected",
            lostAt = token?.ConnectionLostAt,
        };

        var runLog = await db.ReconciliationRunLogs.AsNoTracking().FirstOrDefaultAsync(ct);
        var weeklyCheck = runLog is null
            ? null
            : new { lastRunAt = runLog.LastRunAt, failed = runLog.LastRunFailed, error = runLog.LastRunError, outcomeSeen = runLog.LastRunOutcomeSeen };

        // The same method GET api/sync/status used, so the two can't disagree
        // (design.md D1). heldCount is the stored count, before any item that
        // MyAnimeList already agrees with has been pruned by the Settings
        // page's own held-list read.
        var (pendingCount, heldCount, lastSyncedAt) = await entryRepository.GetSyncStatusAsync(ct);
        var diffPending = await db.PendingReconciliationDiffs.AnyAsync(ct);
        var sync = new { pendingCount, heldCount, lastSyncedAt, diffPending };

        return Ok(new
        {
            malConnection,
            weeklyCheck,
            sync,
            jobs = new
            {
                // The list import is reported as it's shown, so a quiet run
                // reads as not started (design.md D15). These eight names are
                // exactly what MarkSeen's switch below maps, so a job added
                // to one and not the other is visible in review.
                listImport = JobDto.From(listImportProgress.Snapshot),
                syncNow = JobDto.From(syncNowProgress.Snapshot),
                reconcile = JobDto.From(reconcileProgress.Snapshot),
                heldDecision = HeldDecisionJobDto.From(heldDecisionProgress),
                resync = JobDto.From(resyncProgress.Snapshot),
                airingRefresh = JobDto.From(airingRefreshProgress.Snapshot),
                seriesBuild = JobDto.From(seriesBuildProgress.Snapshot),
                fileImport = JobDto.From(transferImportProgress.ToJobSnapshot()),
            },
        });
    }

    /// <summary>Reports one or more job outcomes, and/or the weekly check's,
    /// as seen (design.md D5). Each is guarded by the exact end time the
    /// reporter saw: a time that no longer matches records nothing. An
    /// unknown job name refuses the whole report before anything is
    /// recorded, rather than applying the rest and failing partway.</summary>
    [HttpPost("api/app-status/seen")]
    public async Task<IActionResult> MarkSeen([FromBody] AppStatusSeenRequest? request, CancellationToken ct)
    {
        var toMark = new List<(Action<DateTimeOffset> MarkSeen, DateTimeOffset FinishedAt)>();
        foreach (var job in request?.Jobs ?? [])
        {
            // The same eight names as the read's `jobs` object above.
            Action<DateTimeOffset>? markSeen = job.Name switch
            {
                "listImport" => listImportProgress.MarkOutcomeSeen,
                "syncNow" => syncNowProgress.MarkOutcomeSeen,
                "reconcile" => reconcileProgress.MarkOutcomeSeen,
                "heldDecision" => heldDecisionProgress.MarkOutcomeSeen,
                "resync" => resyncProgress.MarkOutcomeSeen,
                "airingRefresh" => airingRefreshProgress.MarkOutcomeSeen,
                "seriesBuild" => seriesBuildProgress.MarkOutcomeSeen,
                "fileImport" => transferImportProgress.MarkOutcomeSeen,
                _ => null,
            };

            if (markSeen is null)
                return BadRequest(new { error = $"Unknown job '{job.Name}'." });

            toMark.Add((markSeen, job.FinishedAt));
        }

        foreach (var (markSeen, finishedAt) in toMark)
            markSeen(finishedAt);

        if (request?.WeeklyCheckLastRunAt is { } lastRunAt)
        {
            var log = await db.ReconciliationRunLogs.FirstOrDefaultAsync(ct);
            if (log is not null && log.LastRunAt == lastRunAt)
            {
                log.LastRunOutcomeSeen = true;
                await db.SaveChangesAsync(ct);
            }
        }

        return NoContent();
    }
}
