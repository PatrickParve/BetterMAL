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
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
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
}
