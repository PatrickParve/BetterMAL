using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Runs the settings page's manual "build all series from my list"
/// action when signaled — builds a series for every my-list anime that
/// belongs to no stored series yet, so the Top series ranking can be
/// completed on demand rather than only filling in through the on-read
/// backfill (design.md decision 6). Manual-only, mirroring
/// AiringFullRefreshBackgroundService: no auto-signal on startup, and nothing
/// to resume on a restart.</summary>
public class SeriesBulkBuildBackgroundService(
    IServiceScopeFactory scopeFactory,
    ISeriesBulkBuildTrigger trigger,
    ISeriesBulkBuildProgressTracker progress,
    ILogger<SeriesBulkBuildBackgroundService> logger) : BackgroundService
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
                await RunAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual series bulk build failed.");
            }
        }
    }

    private async Task RunAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
        var entryRepository = scope.ServiceProvider.GetRequiredService<IUserAnimeEntryRepository>();
        var seriesService = scope.ServiceProvider.GetRequiredService<ISeriesService>();

        var targets = await GetTargetsAsync(db, entryRepository, ct);
        progress.Start(targets.Count);

        var processed = 0;
        foreach (var animeId in targets)
        {
            ct.ThrowIfCancellationRequested();

            // Re-check membership before each target: building one anime's
            // franchise can store several of its other members too, so a
            // later target in this same run may already be covered — count
            // it as processed without building it again, so progress reflects
            // real remaining work (design.md decision 6).
            var alreadyCovered = await db.SeriesMembers.AsNoTracking().AnyAsync(m => m.AnimeId == animeId, ct);
            if (!alreadyCovered)
            {
                try
                {
                    await seriesService.GetSeriesAsync(animeId, ct);
                }
                catch (SeriesNotFoundException)
                {
                    // The anime's story relations resolve to nothing else —
                    // not a failure, just nothing to store, exactly as a
                    // visit-triggered build behaves for a lone anime (task 5.4).
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Bulk series build failed for anime {AnimeId}.", animeId);
                }
            }

            processed++;
            progress.ReportProgress(processed);
        }

        progress.Complete();
    }

    // My-list anime with no SeriesMembers row yet.
    private static async Task<List<int>> GetTargetsAsync(
        AnimeTrackerDbContext db, IUserAnimeEntryRepository entryRepository, CancellationToken ct)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var animeIds = entries.Select(e => e.AnimeId).Distinct().ToList();

        var alreadyMembers = (await db.SeriesMembers.AsNoTracking()
            .Where(m => animeIds.Contains(m.AnimeId))
            .Select(m => m.AnimeId)
            .ToListAsync(ct))
            .ToHashSet();

        return animeIds.Where(id => !alreadyMembers.Contains(id)).ToList();
    }
}
