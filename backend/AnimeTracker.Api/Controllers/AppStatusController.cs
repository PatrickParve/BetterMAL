using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Controllers;

/// <summary>One cheap read of every job's state, the MyAnimeList connection
/// state, and the weekly check's last outcome (design.md D15): the in-memory
/// tracker snapshots, plus two single-row primary-key reads. Never calls
/// MyAnimeList or AniList, so it's cheap enough for the Settings page to
/// repeat every second while a job runs.</summary>
[ApiController]
public class AppStatusController(
    AnimeTrackerDbContext db,
    IMalTokenStore tokenStore,
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
            : new { lastRunAt = runLog.LastRunAt, failed = runLog.LastRunFailed, error = runLog.LastRunError };

        return Ok(new
        {
            malConnection,
            weeklyCheck,
            jobs = new
            {
                // The list import is reported as it's shown, so a quiet run
                // reads as not started (design.md D15).
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
}
