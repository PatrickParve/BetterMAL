namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>Hourly tick that runs the weekly AnimeThemes catalogue sync when it is due
/// (spec `anime-themes`, add-anime-themes-and-guess-the-theme design.md D3). A
/// hosted service of its own, rather than a step of the airing tick, because a
/// refresh can take many minutes when AnimeThemes is slow and nothing else may
/// wait for it. It calls neither MyAnimeList nor AniList, so unlike the airing work
/// it doesn't wait for first-run setup. Runs immediately on start, so a first start
/// begins the first read at once. Each iteration gets a scope, and so a DbContext, of
/// its own, and a failure is logged and the loop carries on.</summary>
public class AnimeThemesCatalogueBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<AnimeThemesCatalogueBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIterationAsync(stoppingToken);
            }
            // Only our own shutdown ends the loop. An HttpClient timeout is a
            // TaskCanceledException too, and must not end it until the next restart.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // SyncIfDueAsync logs and records its own failures and never throws
                // them; this is the backstop for anything around it, such as opening
                // its scope or resolving it.
                logger.LogError(ex, "AnimeThemes catalogue sync step failed.");
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

    /// <summary>One loop iteration. Exposed internally so tests can drive a single
    /// one without the hourly delay (`InternalsVisibleTo`).</summary>
    internal async Task RunIterationAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var sync = scope.ServiceProvider.GetRequiredService<IAnimeThemesCatalogueSyncService>();
        await sync.SyncIfDueAsync(ct);
    }
}
