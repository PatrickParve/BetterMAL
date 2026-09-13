using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Runs the settings page's manual "refresh all airing data" action
/// when signaled — re-fetches AniList airing data for every my-list anime
/// except finished shows already fetched at least once (see
/// IEpisodeScheduleRefreshService.GetFullRefreshTargetsAsync). Manual-only,
/// like SeriesBulkBuildBackgroundService: no auto-signal on startup, since the
/// automatic hourly tick already keeps data fresh — this is for "something
/// looks wrong, force a full re-fetch" and there's nothing to resume on a
/// restart.</summary>
public class AiringFullRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    IAiringFullRefreshTrigger trigger,
    AiringFullRefreshProgress progress,
    ILogger<AiringFullRefreshBackgroundService> logger) : BackgroundService
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
                var refreshService = scope.ServiceProvider.GetRequiredService<IEpisodeScheduleRefreshService>();
                var targets = await refreshService.GetFullRefreshTargetsAsync(stoppingToken);
                progress.SetTotal(targets.Count);
                await refreshService.RefreshManyAsync(targets, stoppingToken, synced => progress.ReportProgress(synced));
                progress.Complete();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual airing full refresh failed.");
                progress.Fail(JobFailure.Describe(ex, "AniList"));
            }
        }
    }
}
