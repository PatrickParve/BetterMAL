using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Drains ISeriesBuildTrigger's queue: an anime whose search match
/// had no stored series gets its franchise built now, so a later search finds
/// it — mirrors AiringRefreshTriggerBackgroundService's
/// signal-plus-dedicated-loop pattern.</summary>
public class SeriesBuildTriggerBackgroundService(
    IServiceScopeFactory scopeFactory,
    ISeriesBuildTrigger trigger,
    SetupGate setupGate,
    ILogger<SeriesBuildTriggerBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Setup builds every list anime's series itself, without a budget, so a
        // queued build would only repeat or race it; whatever is queued waits
        // here and finds its series up to date (add-first-run-setup design D2).
        await setupGate.WhenFinished.WaitAsync(stoppingToken);

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
                var seriesService = scope.ServiceProvider.GetRequiredService<ISeriesService>();
                await seriesService.GetSeriesAsync(animeId, stoppingToken);
            }
            // Only our own shutdown ends the loop; an HttpClient timeout is a
            // TaskCanceledException too, and fails just this build.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (SeriesNotFoundException)
            {
                // The anime's story relations resolve to nothing else — not a
                // failure, just nothing to store, exactly as a visit-triggered
                // build behaves for a lone anime.
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Background series build failed for anime {AnimeId}.", animeId);
            }
        }
    }
}
