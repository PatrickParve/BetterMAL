namespace AnimeTracker.Api.Services.Sync;

/// <summary>Safety net for the debounce timer: periodically drains any entries
/// still marked pending_sync — e.g. a push that failed, or a debounce timer
/// lost to a process restart — so a failure is retried rather than silently
/// dropped (design.md's "Lost writes on crash/offline" risk).</summary>
public class PendingSyncRetryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PendingSyncRetryBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan RetryInterval = TimeSpan.FromMinutes(2);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var pushService = scope.ServiceProvider.GetRequiredService<IEntryPushService>();
                var pushed = await pushService.DrainPendingAsync(stoppingToken);
                if (pushed > 0)
                    logger.LogInformation("Pending-sync retry pass pushed {Count} entr{Suffix}.", pushed, pushed == 1 ? "y" : "ies");
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Pending-sync retry pass failed.");
            }

            try
            {
                await Task.Delay(RetryInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
