using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Entries;

public class AiringWatchStatusService(
    AnimeTrackerDbContext db,
    IEntrySyncScheduler syncScheduler,
    ILogger<AiringWatchStatusService> logger) : IAiringWatchStatusService
{
    public async Task SettleAsync(
        IReadOnlyCollection<UserAnimeEntry> entries,
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        foreach (var entry in entries)
        {
            // Re-open: kept to currently-airing anime, and load-bearing for
            // it — MAL lists commonly hold entries marked completed at a
            // partial count on a *finished* show (people do this on MAL's own
            // site), and dropping this guard would re-open every one of them
            // on the next page read and push watching back to MAL. Reads the
            // total rather than the aired count so a stale currently_airing
            // status can never repeatedly un-complete a run the user has
            // actually finished (design.md D3/D5).
            if (entry.Status == WatchStatus.Completed
                && entry.Anime.AiringStatus == "currently_airing"
                && !(entry.Anime.TotalEpisodes is { } reopenTotal && entry.EpisodesWatched >= reopenTotal))
            {
                entry.Status = await ReopenOneAsync(entry.AnimeId, ct);
                continue;
            }

            // Complete: the increment path (UserAnimeEntryEditService)
            // already completes an entry the moment it reaches the total, so
            // the only entries this catches are ones that reached a total
            // *before it was known* — the AniList-fallback case (design.md
            // D6). EverythingHasAired's status arm still lets a finished show
            // with incomplete AniList airing rows complete here.
            if (entry.Status == WatchStatus.Watching
                && entry.Anime.TotalEpisodes is { } completeTotal
                && entry.EpisodesWatched >= completeTotal
                && AiredEpisodeGate.EverythingHasAired(entry.Anime, airedSoFarByAnimeId.TryGetValue(entry.AnimeId, out var aired) ? aired : null))
            {
                entry.Status = await CompleteOneAsync(entry.AnimeId, today, ct);
            }
        }
    }

    private async Task<WatchStatus> ReopenOneAsync(int animeId, CancellationToken ct)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);

        // Re-checked under this fresh, tracked read: a concurrent settle (or
        // an interactive edit) may have already moved the entry off Completed
        // since the caller's own read resolved the condition that got us here.
        if (entry is null || entry.Status != WatchStatus.Completed)
            return entry?.Status ?? WatchStatus.Completed;

        entry.Status = WatchStatus.Watching;
        entry.PendingSync = true;
        var activityLog = new ActivityLog
        {
            AnimeId = animeId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeType = ActivityChangeType.StatusChanged,
            ChangeDetail = ActivityDetail.StatusChange(WatchStatus.Completed, WatchStatus.Watching),
        };
        db.ActivityLogs.Add(activityLog);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Another read raced the same flip and won between our read and
            // our save. The condition is now false either way, so there is
            // nothing left to retry — drop our queued log row and reload so
            // this DbContext's tracked state matches the winner's value,
            // rather than surfacing an error on a page render.
            db.ActivityLogs.Remove(activityLog);
            await db.Entry(entry).ReloadAsync(ct);
            logger.LogInformation("Anime {AnimeId} was reopened by a concurrent read; nothing further to do.", animeId);
            return entry.Status;
        }

        syncScheduler.ScheduleSync(animeId);
        return entry.Status;
    }

    private async Task<WatchStatus> CompleteOneAsync(int animeId, DateOnly today, CancellationToken ct)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);

        // Re-checked under this fresh, tracked read for the same reason as
        // ReopenOneAsync — the entry may already have moved off Watching.
        if (entry is null || entry.Status != WatchStatus.Watching)
            return entry?.Status ?? WatchStatus.Watching;

        entry.Status = WatchStatus.Completed;
        entry.CompletedAt ??= today; // never overwrites an existing finish date
        entry.PendingSync = true;
        var activityLog = new ActivityLog
        {
            AnimeId = animeId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeType = ActivityChangeType.Completed,
            ChangeDetail = ActivityDetail.Completed,
        };
        db.ActivityLogs.Add(activityLog);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ActivityLogs.Remove(activityLog);
            await db.Entry(entry).ReloadAsync(ct);
            logger.LogInformation("Anime {AnimeId} was settled by a concurrent read; nothing further to do.", animeId);
            return entry.Status;
        }

        syncScheduler.ScheduleSync(animeId);
        return entry.Status;
    }
}
