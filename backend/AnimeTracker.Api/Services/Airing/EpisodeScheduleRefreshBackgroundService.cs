using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Hourly tick that evaluates due airing-refresh work rather than
/// running on a fixed interval: the one-time full-history backfill, the daily
/// refresh pass for tracked anime, out-of-band rechecks for anime with
/// incomplete data, and a forced pass at a season-quarter boundary. Runs
/// immediately on start — boot isn't special-cased; the daily pass's
/// not-already-today condition is what makes a boot-after-being-stopped
/// refresh happen. Each iteration also opens with two housekeeping steps, each
/// in a scope and so a DbContext of its own: the weekly MAL → TMDB/IMDb
/// id-mapping sync (design.md D2), then the deletion of cached TMDB image sets
/// that have outlived what TMDB's terms allow (design.md D20). A failed save in
/// one can't leave tracked entities behind for the others' saves, and none of
/// them can stop another.</summary>
public class EpisodeScheduleRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<EpisodeScheduleRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan DailyPassMaxAge = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunIterationAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Airing-refresh tick failed.");
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

    /// <summary>One loop iteration: the mapping sync, the TMDB cache purge,
    /// then the airing tick, each in its own scope. Exposed internally so tests
    /// can drive a single iteration without the hourly delay
    /// (`InternalsVisibleTo`).</summary>
    internal async Task RunIterationAsync(CancellationToken ct)
    {
        // First, so the once-ever AniList backfill (potentially tens of
        // minutes) can't hold up the weekly sync or the purge. The sync itself
        // is one download and one diff-write; the purge is three reads and,
        // nearly always, no write.
        await RunMappingSyncAsync(ct);
        await RunTmdbCachePurgeAsync(ct);

        using var scope = scopeFactory.CreateScope();
        await RunTickAsync(scope.ServiceProvider, ct);
    }

    private async Task RunMappingSyncAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var mappingSync = scope.ServiceProvider.GetRequiredService<IAnimeIdMappingSyncService>();
            await mappingSync.SyncIfDueAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // SyncIfDueAsync logs and records its own failures and never
            // throws them; this is the backstop for anything around it, such
            // as opening its scope or resolving it. The airing tick must run
            // whatever happens here.
            logger.LogError(ex, "Id-mapping sync step failed.");
        }
    }

    private async Task RunTmdbCachePurgeAsync(CancellationToken ct)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var purge = scope.ServiceProvider.GetRequiredService<ITmdbCachePurgeService>();
            await purge.PurgeExpiredAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Local reads and deletes only, so this fails when the database
            // does. The airing tick must run whatever happens here; the next
            // tick runs the purge again.
            logger.LogError(ex, "TMDB cache purge step failed.");
        }
    }

    private static async Task RunTickAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AnimeTrackerDbContext>();
        var refreshService = services.GetRequiredService<IEpisodeScheduleRefreshService>();
        var localTimeConverter = services.GetRequiredService<IBroadcastLocalTimeConverter>();
        var scheduleService = services.GetRequiredService<IEpisodeScheduleService>();
        var airingWatchStatusService = services.GetRequiredService<IAiringWatchStatusService>();

        var state = await db.AiringRefreshStates.FirstOrDefaultAsync(ct);
        if (state is null)
        {
            state = new AiringRefreshState();
            db.AiringRefreshStates.Add(state);
            await db.SaveChangesAsync(ct);
        }

        // Backfill first, once ever.
        if (state.BackfillCompletedAtUtc is null)
            await refreshService.BackfillAsync(ct);

        var now = DateTimeOffset.UtcNow;
        var today = localTimeConverter.GetLocalDate(now);
        var (seasonYear, season) = SeasonCalendar.GetSeasonFor(today);
        var currentSeasonKey = $"{seasonYear}-{season}";

        var lastPassAt = state.LastSuccessfulPassAtUtc;
        var lastPassLocalDate = lastPassAt is { } at ? localTimeConverter.GetLocalDate(at) : (DateOnly?)null;
        var seasonChanged = state.LastPassSeason is not null && state.LastPassSeason != currentSeasonKey;
        var dailyPassDue = lastPassAt is null || now - lastPassAt > DailyPassMaxAge || lastPassLocalDate < today;

        var passCoverage = new HashSet<int>();
        if (dailyPassDue || seasonChanged)
        {
            var targets = await refreshService.GetTrackedAnimeIdsAsync(ct);
            passCoverage.UnionWith(targets);
            await refreshService.RefreshManyAsync(targets, ct);

            state.LastSuccessfulPassAtUtc = now;
            state.LastPassSeason = currentSeasonKey;
            await db.SaveChangesAsync(ct);
        }

        // Out-of-band rechecks: anime whose recheck is due that the pass above
        // (if it ran) didn't already cover.
        var dueRechecks = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.NextRecheckAtUtc != null && s.NextRecheckAtUtc <= now)
            .Select(s => s.AnimeId)
            .ToListAsync(ct);
        var uncovered = dueRechecks.Where(id => !passCoverage.Contains(id)).ToList();
        if (uncovered.Count > 0)
            await refreshService.RefreshManyAsync(uncovered, ct);

        // design.md D6 backstop: without this, an entry for a show nobody
        // opens would never have its status settled — reopened, or completed
        // by an AniList-filled total — pushed to MyAnimeList, however far its
        // aired count moves. Loads every entry (design.md D6/D7's shared
        // dictionary) rather than a pre-filtered subset, since the two
        // settle directions select from different subsets.
        var allEntries = await db.UserAnimeEntries.AsNoTracking()
            .Include(e => e.Anime)
            .ToListAsync(ct);
        if (allEntries.Count > 0)
        {
            var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(allEntries.Select(e => e.Anime).ToList(), now, ct);
            await airingWatchStatusService.SettleAsync(allEntries, airedSoFarByAnimeId, ct);
        }
    }
}
