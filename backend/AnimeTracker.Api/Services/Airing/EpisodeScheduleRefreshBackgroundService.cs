namespace AnimeTracker.Api.Services.Airing;

/// <summary>Warms and periodically refreshes the per-episode schedule cache
/// from AniList. Runs shortly after startup (the cache is empty until then, so
/// the schedule falls back to the cadence estimate meanwhile) and every few
/// hours after, picking up newly announced episode dates and break changes.</summary>
public class EpisodeScheduleRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EpisodeScheduleRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(StartupDelay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var refresh = scope.ServiceProvider.GetRequiredService<IEpisodeScheduleRefreshService>();
                await refresh.RefreshAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "AniList schedule refresh pass failed.");
            }

            try
            {
                await Task.Delay(RefreshInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
