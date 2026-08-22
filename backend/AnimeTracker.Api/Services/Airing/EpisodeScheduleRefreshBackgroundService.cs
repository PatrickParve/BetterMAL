using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>Hourly tick that evaluates due airing-refresh work rather than
/// running on a fixed interval: the one-time full-history backfill, the daily
/// refresh pass for tracked anime, out-of-band rechecks for anime with
/// incomplete data, and a forced pass at a season-quarter boundary. Runs
/// immediately on start — boot isn't special-cased; the daily pass's
/// not-already-today condition is what makes a boot-after-being-stopped
/// refresh happen.</summary>
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
                using var scope = scopeFactory.CreateScope();
                await RunTickAsync(scope.ServiceProvider, stoppingToken);
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

    private static async Task RunTickAsync(IServiceProvider services, CancellationToken ct)
    {
        var db = services.GetRequiredService<AnimeTrackerDbContext>();
        var refreshService = services.GetRequiredService<IEpisodeScheduleRefreshService>();
        var localTimeConverter = services.GetRequiredService<IBroadcastLocalTimeConverter>();
        var scheduleService = services.GetRequiredService<IEpisodeScheduleService>();
        var reopenService = services.GetRequiredService<ICompletedEntryReopenService>();

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

        // design.md D6 backstop: without this, a Completed entry for a show
        // nobody opens would never have its reopening pushed to MyAnimeList,
        // however far behind the aired count grows.
        var completedAiring = await db.UserAnimeEntries.AsNoTracking()
            .Include(e => e.Anime)
            .Where(e => e.Status == WatchStatus.Completed && e.Anime.AiringStatus == "currently_airing")
            .ToListAsync(ct);
        if (completedAiring.Count > 0)
        {
            var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(completedAiring.Select(e => e.Anime).ToList(), now, ct);
            await reopenService.ReopenAsync(completedAiring, airedSoFarByAnimeId, ct);
        }
    }
}
