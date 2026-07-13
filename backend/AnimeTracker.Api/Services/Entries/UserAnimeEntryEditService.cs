using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
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
    IEntrySyncScheduler syncScheduler) : IUserAnimeEntryEditService
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
        }

        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime);
        var now = DateTimeOffset.UtcNow;
        var changes = new List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)>();

        // Captured before either Apply* runs: the editor always resends the
        // status it loaded with (never omits it), so this is the only reliable
        // way to tell "no explicit status change requested" from a real one once
        // ApplyEpisodesWatched may have already flipped entry.Status itself.
        var originalStatus = entry.Status;

        ApplyEpisodesWatched(request, entry, anime, originalStatus, today, changes);
        ApplyStatus(request, entry, anime, animeId, originalStatus, today, changes);
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

        return UserAnimeEntryDto.FromEntity(entry);
    }

    private static void ApplyEpisodesWatched(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, AnimeMetadata anime, WatchStatus originalStatus, DateOnly today,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.EpisodesWatched is not { } newEpisodes || newEpisodes == entry.EpisodesWatched)
            return;

        if (newEpisodes < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Episodes watched cannot be negative.");

        if (anime.TotalEpisodes is { } totalEpisodes && newEpisodes > totalEpisodes)
            throw new ArgumentOutOfRangeException(nameof(request), $"Episodes watched cannot exceed the anime's total episode count ({totalEpisodes}).");

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

        if (newRewatchCount < 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Rewatch count cannot be negative.");

        entry.RewatchCount = newRewatchCount;
        changes.Add((ActivityChangeType.RewatchCountChanged, $"Rewatch count {newRewatchCount}", null));
    }
}
