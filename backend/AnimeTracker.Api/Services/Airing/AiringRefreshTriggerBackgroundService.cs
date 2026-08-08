namespace AnimeTracker.Api.Services.Airing;

/// <summary>Drains IAiringRefreshTrigger's queue: an anime enqueued on add
/// (UserAnimeEntryEditService) gets its airing data fetched right away rather
/// than waiting for the next hourly tick — mirrors ImportTrigger/ResyncTrigger's
/// signal-plus-dedicated-loop pattern.</summary>
public class AiringRefreshTriggerBackgroundService(
    IServiceScopeFactory scopeFactory,
    IAiringRefreshTrigger trigger,
    ILogger<AiringRefreshTriggerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            int animeId;
            try
            {
                animeId = await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var refreshService = scope.ServiceProvider.GetRequiredService<IEpisodeScheduleRefreshService>();
                await refreshService.RefreshOneAsync(animeId, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Triggered airing refresh failed for anime {AnimeId}.", animeId);
            }
        }
    }
}
