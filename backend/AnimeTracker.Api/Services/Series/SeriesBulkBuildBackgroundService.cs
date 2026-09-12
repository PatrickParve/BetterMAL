using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Jobs;
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
    SeriesBulkBuildProgress progress,
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
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Manual series bulk build failed.");
                progress.Fail(JobFailure.Describe(ex, "MyAnimeList"));
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
        progress.SetTotal(targets.Count);

        var processed = 0;
        foreach (var animeId in targets)
        {
            ct.ThrowIfCancellationRequested();

            // Re-check membership before each target: building one anime's
            // franchise can store several of its other members too, so a
            // later target in this same run may already be covered — count
            // it as processed without building it again, so progress reflects
            // real remaining work (design.md decision 6). "Covered" also
            // requires the member's series to be built under the current
            // classification rules, so a franchise rebuilt earlier in this
            // same run short-circuits its other members while one that
            // predates the run and hasn't been touched yet still gets
            // rebuilt (design.md decision 4). Filtered to the primary
            // membership (split-series-by-version task 8.5) — an anime held
            // only as a shared or boundary member of some other telling isn't
            // covered by that telling being up to date; it still needs its
            // own.
            var alreadyCovered = await db.SeriesMembers.AsNoTracking()
                .Where(m => m.AnimeId == animeId && m.IsPrimary)
                .Join(db.Series.AsNoTracking(), m => m.SeriesId, s => s.Id, (m, s) => new { m.AnimeId, s.BuiltAt })
                .AnyAsync(x => x.BuiltAt >= SeriesGraphBuilder.ClassificationRevisedAt, ct);
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

    // My-list anime with no up-to-date *primary* membership — no
    // SeriesMembers row at all, a stored series built before the current
    // classification rules took effect, or (split-series-by-version task 8.5)
    // held only as a shared or boundary member of some other telling — so one
    // press of the Settings button fills in missing series, heals ones stored
    // under superseded rules, and builds a telling's own series even when it
    // already appears as another telling's boundary member (design.md
    // decision 4; series-versions spec "A boundary-only membership does not
    // count as covered").
    private static async Task<List<int>> GetTargetsAsync(
        AnimeTrackerDbContext db, IUserAnimeEntryRepository entryRepository, CancellationToken ct)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var animeIds = entries.Select(e => e.AnimeId).Distinct().ToList();

        var upToDateMembers = (await db.SeriesMembers.AsNoTracking()
            .Where(m => animeIds.Contains(m.AnimeId) && m.IsPrimary)
            .Join(db.Series.AsNoTracking(), m => m.SeriesId, s => s.Id, (m, s) => new { m.AnimeId, s.BuiltAt })
            .Where(x => x.BuiltAt >= SeriesGraphBuilder.ClassificationRevisedAt)
            .Select(x => x.AnimeId)
            .ToListAsync(ct))
            .ToHashSet();

        return animeIds.Where(id => !upToDateMembers.Contains(id)).ToList();
    }
}
