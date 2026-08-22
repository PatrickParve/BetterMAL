using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Entries;

public class CompletedEntryReopenService(
    AnimeTrackerDbContext db,
    IEntrySyncScheduler syncScheduler,
    ILogger<CompletedEntryReopenService> logger) : ICompletedEntryReopenService
{
    public async Task ReopenAsync(
        IReadOnlyCollection<UserAnimeEntry> entries,
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        CancellationToken ct = default)
    {
        foreach (var entry in entries)
        {
            if (entry.Status != WatchStatus.Completed
                || entry.Anime.AiringStatus != "currently_airing"
                || !airedSoFarByAnimeId.TryGetValue(entry.AnimeId, out var airedSoFar)
                || airedSoFar <= entry.EpisodesWatched)
                continue;

            // Mutate the caller's own (typically untracked) object so a DTO
            // it builds from this same reference — before or after this call
            // — reflects the outcome, whichever of this call or a concurrent
            // one actually performed the write.
            entry.Status = await ReopenOneAsync(entry.AnimeId, ct);
        }
    }

    private async Task<WatchStatus> ReopenOneAsync(int animeId, CancellationToken ct)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);

        // Re-checked under this fresh, tracked read: a concurrent reopen (or
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
            ChangeDetail = $"{WatchStatus.Completed} -> {WatchStatus.Watching}",
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
}
