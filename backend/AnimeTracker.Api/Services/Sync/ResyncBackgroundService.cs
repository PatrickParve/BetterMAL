using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>Runs the corrective re-sync when signaled from the settings page
/// (SyncController) — manual-only, unlike InitialImportBackgroundService there
/// is no auto-signal on startup since this is a one-time corrective action,
/// not something every fresh install needs.</summary>
public class ResyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IResyncTrigger trigger,
    ResyncProgress progress,
    ILogger<ResyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var resyncService = scope.ServiceProvider.GetRequiredService<IResyncService>();
                await resyncService.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Corrective re-sync run failed.");
                progress.Fail(JobFailure.Describe(ex, "MyAnimeList"));
            }
        }
    }
}
