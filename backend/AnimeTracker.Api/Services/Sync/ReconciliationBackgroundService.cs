using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>Runs full reconciliation on a weekly schedule — the scheduled half
/// of the reconciliation safety net (manual trigger is SyncController).</summary>
public class ReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    SetupGate setupGate,
    ILogger<ReconciliationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // With no run log it is due at once, which on a fresh install would read
        // the list a second time beside setup's own read; it starts when setup
        // has finished (add-first-run-setup design D2).
        await setupGate.WhenFinished.WaitAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var delay = await GetDelayUntilDueAsync(stoppingToken);
                if (delay > TimeSpan.Zero)
                    await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                // Recorded at attempt time, not on success, so a persistently
                // failing run still waits a full interval before retrying —
                // matching the original always-wait-a-week cadence. Resets
                // LastRunFailed/LastRunError to null: this attempt hasn't
                // recorded an ending yet.
                await RecordRunAttemptAsync(scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>(), stoppingToken);

                var reconciliation = scope.ServiceProvider.GetRequiredService<IReconciliationService>();
                // No sink — the weekly run is never reported as "Run full
                // reconciliation" (design.md D6).
                var result = await reconciliation.RunAsync(ct: stoppingToken);

                var failed = result.SkippedUnrecognized > 0;
                await RecordRunOutcomeAsync(
                    failed,
                    failed ? JobFailure.UnrecognizedStatuses(result.SkippedUnrecognized) : null,
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled reconciliation run failed.");
                // A fresh scope: the run's own DbContext may be in a failed state.
                await RecordRunOutcomeAsync(failed: true, JobFailure.Describe(ex, "MyAnimeList"), stoppingToken);
            }
        }
    }

    // Persisted so a container restart mid-interval doesn't reset the weekly
    // clock: without this, a machine that restarts more often than weekly
    // could go indefinitely without ever running reconciliation.
    private async Task<TimeSpan> GetDelayUntilDueAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var lastRunAt = await db.ReconciliationRunLogs.AsNoTracking()
            .Select(l => (DateTimeOffset?)l.LastRunAt)
            .FirstOrDefaultAsync(ct);

        if (lastRunAt is null)
            return TimeSpan.Zero;

        var dueAt = lastRunAt.Value + Interval;
        var now = DateTimeOffset.UtcNow;
        return dueAt > now ? dueAt - now : TimeSpan.Zero;
    }

    private static async Task RecordRunAttemptAsync(AnimeTrackerDbContext db, CancellationToken ct)
    {
        var log = await db.ReconciliationRunLogs.FirstOrDefaultAsync(ct);
        var now = DateTimeOffset.UtcNow;
        if (log is null)
        {
            db.ReconciliationRunLogs.Add(new ReconciliationRunLog { LastRunAt = now });
        }
        else
        {
            log.LastRunAt = now;
            log.LastRunFailed = null;
            log.LastRunError = null;
            log.LastRunOutcomeSeen = false;
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task RecordRunOutcomeAsync(bool failed, string? error, CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var log = await db.ReconciliationRunLogs.FirstOrDefaultAsync(ct);
        if (log is null)
            return; // RecordRunAttemptAsync always creates the row before a run starts

        log.LastRunFailed = failed;
        log.LastRunError = error;
        await db.SaveChangesAsync(ct);
    }
}
