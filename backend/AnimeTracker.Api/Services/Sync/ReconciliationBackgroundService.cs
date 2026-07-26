using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>Runs full reconciliation on a weekly schedule — the scheduled half
/// of the reconciliation safety net (manual trigger is SyncController).</summary>
public class ReconciliationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReconciliationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(7);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
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
                // matching the original always-wait-a-week cadence.
                await RecordRunAttemptAsync(scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>(), stoppingToken);

                var reconciliation = scope.ServiceProvider.GetRequiredService<IReconciliationService>();
                await reconciliation.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled reconciliation run failed.");
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
            db.ReconciliationRunLogs.Add(new ReconciliationRunLog { LastRunAt = now });
        else
            log.LastRunAt = now;

        await db.SaveChangesAsync(ct);
    }
}
