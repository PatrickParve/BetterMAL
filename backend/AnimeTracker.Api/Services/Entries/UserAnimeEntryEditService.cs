using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Entries;

/// <summary>Applies list-entry edits with the app's business rules
/// (start/complete date lifecycle, unknown-episode completion guard, activity
/// logging, sync triggering). Reads/writes the DbContext directly with
/// tracking — unlike the read-only repositories that back page renders, this
/// is the write path.</summary>
public class UserAnimeEntryEditService(
    AnimeTrackerDbContext db,
    IEntrySyncScheduler syncScheduler,
    IEpisodeScheduleService scheduleService,
    IAiringRefreshTrigger airingRefreshTrigger,
    IServiceScopeFactory scopeFactory,
    ILogger<UserAnimeEntryEditService> logger) : IUserAnimeEntryEditService
{
    public async Task<UserAnimeEntryDto> UpdateEntryAsync(int animeId, UserAnimeEntryEditRequest request, CancellationToken ct = default)
    {
        var anime = await db.AnimeMetadata.FirstOrDefaultAsync(a => a.Id == animeId, ct)
            ?? throw new AnimeNotFoundException(animeId);

        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct);
        var isNew = entry is null;
        if (entry is null)
        {
            entry = new UserAnimeEntry { AnimeId = animeId, Status = WatchStatus.PlanToWatch };
            db.UserAnimeEntries.Add(entry);

            // Re-adding an anime cancels any removal still queued for it —
            // otherwise a delete that failed while offline could fire on the
            // next retry sweep and erase the entry just recreated.
            var pendingDeletion = await db.PendingEntryDeletions.FirstOrDefaultAsync(d => d.AnimeId == animeId, ct);
            if (pendingDeletion is not null)
                db.PendingEntryDeletions.Remove(pendingDeletion);
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var now = DateTimeOffset.UtcNow;
        var changes = new List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)>();

        // Captured before either Apply* runs: the editor always resends the
        // status it loaded with (never omits it), so this is the only reliable
        // way to tell "no explicit status change requested" from a real one once
        // ApplyEpisodesWatched may have already flipped entry.Status itself.
        var originalStatus = entry.Status;

        await ApplyEpisodesWatchedAsync(request, entry, anime, originalStatus, today, now, changes, ct);
        ApplyStatus(request, entry, anime, animeId, originalStatus, today, changes);
        ApplyDates(request, entry, changes);
        ApplyScore(request, entry, changes);
        ApplyRewatchCount(request, entry, changes);

        if (isNew)
        {
            db.ActivityLogs.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.Added,
                ChangeDetail = $"Added as {entry.Status}",
            });
        }
        else
        {
            foreach (var (type, detail, previousEpisodesWatched) in changes)
                db.ActivityLogs.Add(new ActivityLog
                {
                    AnimeId = animeId,
                    Timestamp = now,
                    ChangeType = type,
                    ChangeDetail = detail,
                    PreviousEpisodesWatched = previousEpisodesWatched,
                });
        }

        var triggersSync = isNew || changes.Count > 0;
        if (triggersSync)
            entry.PendingSync = true;

        await db.SaveChangesAsync(ct);

        if (triggersSync)
            syncScheduler.ScheduleSync(animeId);

        // Fire-and-forget: any anime just added gets its episode history
        // fetched now rather than waiting for the backfill, without delaying
        // this response on an AniList round-trip.
        if (isNew)
            airingRefreshTrigger.Enqueue(animeId);

        return UserAnimeEntryDto.FromEntity(entry);
    }

    public async Task RemoveEntryAsync(int animeId, CancellationToken ct = default)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId, ct)
            ?? throw new EntryNotFoundException(animeId);

        db.UserAnimeEntries.Remove(entry);
        db.ActivityLogs.Add(new ActivityLog
        {
            AnimeId = animeId,
            Timestamp = DateTimeOffset.UtcNow,
            ChangeType = ActivityChangeType.Removed,
            ChangeDetail = "Removed from list",
        });
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = animeId, RequestedAt = DateTimeOffset.UtcNow });

        await db.SaveChangesAsync(ct);

        // A removed entry has nothing left to coalesce with, so push it now
        // instead of through the edit debounce — fire-and-forget (like
        // airingRefreshTrigger.Enqueue above) so the response doesn't wait on
        // MAL. Runs in its own scope since this request's DbContext will be
        // disposed once the response returns.
        _ = PushRemovalAsync(animeId);
    }

    private async Task PushRemovalAsync(int animeId)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var pushService = scope.ServiceProvider.GetRequiredService<IEntryPushService>();
            await pushService.PushPendingDeletionAsync(animeId);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Immediate removal push failed for anime {AnimeId}; it remains pending.", animeId);
        }
    }

    private async Task ApplyEpisodesWatchedAsync(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, AnimeMetadata anime, WatchStatus originalStatus, DateOnly today, DateTimeOffset now,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes, CancellationToken ct)
    {
        if (request.EpisodesWatched is not { } newEpisodes || newEpisodes == entry.EpisodesWatched)
            return;

        if (newEpisodes < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Episodes watched cannot be negative.");

        // Cap against what's actually out, not just the eventual total: a
        // still-airing show can't be watched past its aired-so-far count even
        // once its total episode count is known. A finished show has no
        // stored rows in the future, so EpisodesAiredAsOfAsync already
        // resolves to its last stored episode — the `?? anime.TotalEpisodes`
        // fallback below only matters while no airing data is stored at all.
        var maxEpisodes = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct) ?? anime.TotalEpisodes;
        if (maxEpisodes is { } cap && newEpisodes > cap)
            throw new ArgumentOutOfRangeException(nameof(request), $"Episodes watched cannot exceed the number of episodes available ({cap}).");

        var previousEpisodesWatched = entry.EpisodesWatched;

        // Started-date rule fires on the 0 -> >0 transition, and never overwrites an existing start date.
        if (entry.EpisodesWatched == 0 && newEpisodes > 0 && entry.StartedAt is null)
            entry.StartedAt = today;

        entry.EpisodesWatched = newEpisodes;
        changes.Add((ActivityChangeType.EpisodeIncremented, $"Episode {newEpisodes}", previousEpisodesWatched));

        // Watching all episodes marks the show completed, mirroring MAL's own UI —
        // unless this same request is already setting a different status explicitly.
        if ((request.Status is null || request.Status == originalStatus) && originalStatus != WatchStatus.Completed &&
            anime.TotalEpisodes is { } total && newEpisodes == total)
        {
            entry.Status = WatchStatus.Completed;
            entry.CompletedAt ??= today; // never overwrites an existing finish date — see ApplyStatus
            changes.Add((ActivityChangeType.Completed, "Completed", null));
        }
    }

    private static void ApplyStatus(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, AnimeMetadata anime, int animeId, WatchStatus originalStatus, DateOnly today,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.Status is not { } newStatus || newStatus == originalStatus)
            return;

        if (newStatus == WatchStatus.Completed)
        {
            if (anime.TotalEpisodes is null)
                throw new CannotCompleteUnknownEpisodeCountException(animeId);

            entry.CompletedAt ??= today; // never overwrites an existing finish date — mirrors the StartedAt rule, and MAL itself keeps the original finish_date across rewatches
            entry.EpisodesWatched = anime.TotalEpisodes.Value; // mirror MAL's own UI, which auto-fills episodes on completion
            changes.Add((ActivityChangeType.Completed, "Completed", null));
        }
        else
        {
            changes.Add((ActivityChangeType.StatusChanged, $"{entry.Status} -> {newStatus}", null));
        }

        entry.Status = newStatus;
    }

    // Runs after ApplyEpisodesWatchedAsync/ApplyStatus so an explicit manual
    // date in this same request wins over whatever those steps auto-filled —
    // the automatic rules only ever fill an empty date (entry.StartedAt is
    // null / entry.CompletedAt ??=), never overwrite one, so a manual value
    // sent alongside a completing edit is untouched by them.
    private static void ApplyDates(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.HasStartedAt && request.StartedAt != entry.StartedAt)
        {
            entry.StartedAt = request.StartedAt;
            changes.Add((ActivityChangeType.StartDateChanged,
                entry.StartedAt is { } started ? $"Start date {started:yyyy-MM-dd}" : "Start date cleared", null));
        }

        if (request.HasCompletedAt && request.CompletedAt != entry.CompletedAt)
        {
            entry.CompletedAt = request.CompletedAt;
            changes.Add((ActivityChangeType.FinishDateChanged,
                entry.CompletedAt is { } finished ? $"Finish date {finished:yyyy-MM-dd}" : "Finish date cleared", null));
        }

        // Compares the resulting (post-edit) values, not the stored ones, so
        // this catches e.g. a finish date set earlier than a start date left
        // unchanged from a prior edit, not just both dates changed together.
        if (entry.StartedAt is { } start && entry.CompletedAt is { } finish && finish < start)
            throw new ArgumentOutOfRangeException(nameof(request), "Finish date cannot be earlier than start date.");
    }

    private static void ApplyScore(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.MyScore is not { } newScore || newScore == (entry.MyScore ?? 0))
            return;

        if (newScore is < 0 or > 10)
            throw new ArgumentOutOfRangeException(nameof(request), "Score must be between 0 and 10.");

        entry.MyScore = newScore == 0 ? null : newScore; // MAL convention: 0 means "no score"
        changes.Add((ActivityChangeType.ScoreChanged, $"Score {entry.MyScore?.ToString() ?? "cleared"}", null));
    }

    private static void ApplyRewatchCount(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.RewatchCount is not { } newRewatchCount || newRewatchCount == entry.RewatchCount)
            return;

        if (newRewatchCount is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(request), "Rewatch count must be between 0 and 100.");

        entry.RewatchCount = newRewatchCount;
        changes.Add((ActivityChangeType.RewatchCountChanged, $"Rewatch count {newRewatchCount}", null));
    }
}
