namespace AnimeTracker.Api.Services.Relations;

/// <summary>Periodic tick that looks up AniList relations for anime holding
/// Unconfirmed edges that the airing-lookup path (<c>EpisodeScheduleRefreshService</c>)
/// wouldn't otherwise reach. One batched AniList request per tick, paced by
/// the same <c>AniListRequestPacer</c> every AniList call already goes
/// through — this never issues more than one request every 4.5s regardless
/// of tick frequency.</summary>
public class RelationAdjudicationBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<RelationAdjudicationBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    private const int BatchSize = 25; // matches the AniList Page.media batch size

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRelationAdjudicationService>();
                var lookedUp = await service.RunBatchAsync(BatchSize, stoppingToken);
                if (lookedUp > 0)
                    logger.LogInformation("Relation adjudication pass looked up {Count} anime on AniList.", lookedUp);
            }
            // Only our own shutdown ends the loop; an HttpClient timeout is a
            // TaskCanceledException too, and fails just this pass.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Relation adjudication pass failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
