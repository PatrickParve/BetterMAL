using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>SettleAsync picks entries to move with no database access, then
/// makes one tracked read and one save for however many qualify — nothing
/// touches the database when none do (design.md D5-D7).</summary>
public class AiringWatchStatusService(
    AnimeTrackerDbContext db,
    IEntrySyncScheduler syncScheduler,
    ILogger<AiringWatchStatusService> logger) : IAiringWatchStatusService
{
    private enum Direction { Reopen, Complete }

    public async Task SettleAsync(
        IReadOnlyCollection<UserAnimeEntry> entries,
        IReadOnlyDictionary<int, int> airedSoFarByAnimeId,
        CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);

        // Pass 1, no database access: pick which entries qualify from the
        // caller's own read.
        var picked = new List<(UserAnimeEntry CallerEntry, Direction Direction)>();
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
                picked.Add((entry, Direction.Reopen));
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
                picked.Add((entry, Direction.Complete));
            }
        }

        if (picked.Count == 0)
            return;

        // One tracked read for every picked entry.
        var ids = picked.Select(p => p.CallerEntry.AnimeId).Distinct().ToList();
        var trackedById = await db.UserAnimeEntries.Where(e => ids.Contains(e.AnimeId)).ToDictionaryAsync(e => e.AnimeId, ct);

        // Pass 2, in caller order: re-check under the fresh read, then queue
        // the change and its log row.
        var applied = new Dictionary<int, (UserAnimeEntry Entry, ActivityLog Log, Direction Direction)>();
        foreach (var (callerEntry, direction) in picked)
        {
            if (!trackedById.TryGetValue(callerEntry.AnimeId, out var tracked))
                continue;

            var log = direction == Direction.Reopen ? TryReopen(tracked) : TryComplete(tracked, today);
            if (log is not null)
                applied[callerEntry.AnimeId] = (tracked, log, direction);
        }

        await SaveDroppingConflictsAsync(applied, ct);

        foreach (var animeId in applied.Keys)
            syncScheduler.ScheduleSync(animeId);

        foreach (var (callerEntry, _) in picked)
            if (trackedById.TryGetValue(callerEntry.AnimeId, out var tracked))
                callerEntry.Status = tracked.Status;
    }

    private ActivityLog? TryReopen(UserAnimeEntry tracked)
    {
        // Re-checked under this fresh, tracked read: a concurrent settle (or
        // an interactive edit) may have already moved the entry off Completed
        // since the caller's own read resolved the condition that got us here.
        if (tracked.Status != WatchStatus.Completed)
            return null;

        tracked.Status = WatchStatus.Watching;
        tracked.PendingSync = true;
        var activityLog = new ActivityLog
        {
            AnimeId = tracked.AnimeId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeType = ActivityChangeType.StatusChanged,
            ChangeDetail = ActivityDetail.StatusChange(WatchStatus.Completed, WatchStatus.Watching),
        };
        db.ActivityLogs.Add(activityLog);
        return activityLog;
    }

    private ActivityLog? TryComplete(UserAnimeEntry tracked, DateOnly today)
    {
        // Re-checked under this fresh, tracked read for the same reason as
        // TryReopen — the entry may already have moved off Watching.
        if (tracked.Status != WatchStatus.Watching)
            return null;

        tracked.Status = WatchStatus.Completed;
        tracked.CompletedAt ??= today; // never overwrites an existing finish date
        tracked.PendingSync = true;
        var activityLog = new ActivityLog
        {
            AnimeId = tracked.AnimeId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeType = ActivityChangeType.Completed,
            ChangeDetail = ActivityDetail.Completed,
        };
        db.ActivityLogs.Add(activityLog);
        return activityLog;
    }

    private async Task SaveDroppingConflictsAsync(
        Dictionary<int, (UserAnimeEntry Entry, ActivityLog Log, Direction Direction)> applied,
        CancellationToken ct)
    {
        // This needs a loop, not one pass over ex.Entries: a failed save
        // rolls back every command in it, so the entries that didn't
        // conflict weren't saved either and still need saving again; and EF
        // raises at the first command with the wrong affected-row count and
        // stops reading results there, so a second conflicting entry in the
        // same batch only surfaces on the next attempt. Every attempt either
        // succeeds, removes at least one entry from applied, or rethrows, so
        // the loop always ends. There's no re-apply after a reload, the same
        // as before this change — the reloaded entry may still qualify, and
        // the next read or the schedule job settles it. This relies on
        // SaveChangesAsync's own transaction, so it must not run inside a
        // caller's explicit transaction.
        while (applied.Count > 0)
        {
            try
            {
                await db.SaveChangesAsync(ct);
                return;
            }
            catch (DbUpdateConcurrencyException ex)
            {
                var conflicted = ex.Entries.Select(e => e.Entity).OfType<UserAnimeEntry>().ToList();
                if (conflicted.Count == 0 || conflicted.Any(c => !applied.ContainsKey(c.AnimeId)))
                    throw;

                foreach (var entry in conflicted)
                {
                    var (_, log, direction) = applied[entry.AnimeId];
                    applied.Remove(entry.AnimeId);
                    db.ActivityLogs.Remove(log);
                    await db.Entry(entry).ReloadAsync(ct);
                    logger.LogInformation(
                        direction == Direction.Reopen
                            ? "Anime {AnimeId} was reopened by a concurrent read; nothing further to do."
                            : "Anime {AnimeId} was settled by a concurrent read; nothing further to do.",
                        entry.AnimeId);
                }
            }
        }
    }
}
