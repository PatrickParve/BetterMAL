using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Ranking;
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
    IAnimeRankingService rankingService,
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

        // Resolved once so every Apply* step below gates against the same
        // answer (design.md D1/D2) — this is the same call
        // ApplyEpisodesWatchedAsync made on its own before this change.
        var airedSoFar = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        var hasAired = AiredEpisodeGate.HasAired(anime, airedSoFar);

        ApplyEpisodesWatched(request, entry, anime, animeId, originalStatus, today, airedSoFar, hasAired, changes);
        ApplyStatus(request, entry, anime, animeId, originalStatus, today, airedSoFar, hasAired, changes);
        ApplyDates(request, entry, today, changes);
        ApplyScore(request, entry, animeId, hasAired, changes);
        ApplyRewatchCount(request, entry, animeId, hasAired, changes);

        if (isNew)
        {
            db.ActivityLogs.Add(new ActivityLog
            {
                AnimeId = animeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.Added,
                ChangeDetail = ActivityDetail.Added(entry.Status),
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
        {
            entry.PendingSync = true;

            // A deliberate edit is current by definition and supersedes
            // whatever stale value was held (design.md D15) — unlike
            // AiringWatchStatusService's automatic transitions, which leave a
            // hold in place since they are inferences, not statements of intent.
            entry.HeldForReviewAt = null;
        }

        await db.SaveChangesAsync(ct);

        // list-editing "A saved score places the anime in the ranking"
        // (design.md D6): runs once the score itself is durably saved, so a
        // failure here never leaves a save half-applied from the caller's
        // perspective — the whole UpdateEntryAsync call still fails and the
        // client never sees success without both. Only a genuine score
        // change places anything; re-saving the same score is a no-op here
        // exactly as ApplyScore already made it one for the entry itself.
        if (changes.Any(c => c.Type == ActivityChangeType.ScoreChanged) && entry.MyScore is { } placedScore)
            await rankingService.PlaceLastInTierAsync(animeId, placedScore, ct);

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
            ChangeDetail = ActivityDetail.Removed,
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

    // design.md D4: the one completion gate shared by the automatic
    // (ApplyEpisodesWatched) and explicit (ApplyStatus) paths, so they can
    // never disagree about when a run is done.
    private static bool CanComplete(AnimeMetadata anime, int? airedSoFar, bool hasAired) =>
        hasAired && anime.TotalEpisodes is not null && AiredEpisodeGate.EverythingHasAired(anime, airedSoFar);

    private static void ApplyEpisodesWatched(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, AnimeMetadata anime, int animeId, WatchStatus originalStatus, DateOnly today,
        int? airedSoFar, bool hasAired,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.EpisodesWatched is not { } newEpisodes || newEpisodes == entry.EpisodesWatched)
            return;

        if (newEpisodes < 0)
            throw new EntryEditRejectedException("Episodes watched cannot be negative.");

        // Cap against what's actually out, not just the eventual total: a
        // still-airing show can't be watched past its aired-so-far count even
        // once its total episode count is known. A finished show has no
        // stored rows in the future, so airedSoFar already resolves to its
        // last stored episode — the `?? anime.TotalEpisodes` fallback only
        // matters while no airing data is stored at all. An anime that has
        // aired no episode has no fallback: its ceiling is 0 outright
        // (gate-editing-on-aired-episodes design.md D1).
        var maxEpisodes = hasAired ? airedSoFar ?? anime.TotalEpisodes : 0;
        if (maxEpisodes is { } cap && newEpisodes > cap)
        {
            if (!hasAired)
                throw new EpisodesWatchedRequiresAiredEpisodeException(animeId);
            throw new EntryEditRejectedException($"Episodes watched cannot exceed the number of episodes available ({cap}).");
        }

        var previousEpisodesWatched = entry.EpisodesWatched;

        // Started-date rule fires on the 0 -> >0 transition, and never overwrites an existing start date.
        if (entry.EpisodesWatched == 0 && newEpisodes > 0 && entry.StartedAt is null)
            entry.StartedAt = today;

        entry.EpisodesWatched = newEpisodes;
        changes.Add((ActivityChangeType.EpisodeIncremented, ActivityDetail.Episode(newEpisodes), previousEpisodesWatched));

        // Unless this same request is already setting a different status explicitly:
        // the completion target is always the anime's total (design.md D4) —
        // never the aired-so-far count. The cap above already guarantees an
        // entry can only reach the total once every episode has aired, so
        // using the total here (rather than a currently-airing-only aired
        // count) never lets a partial run complete early.
        var completionTarget = anime.TotalEpisodes;
        if ((request.Status is null || request.Status == originalStatus) && completionTarget is { } target)
        {
            // Watching every episode out marks the show completed, mirroring
            // MAL's own UI. A Rewatching entry reaching the total is watching
            // a rewatch to the end (design.md D2) — it re-completes and its
            // rewatch count silently increases by one; D0 guarantees the anime
            // has finished airing whenever originalStatus is Rewatching, so
            // `target` is always the real total in that case.
            if (originalStatus != WatchStatus.Completed && newEpisodes == target && CanComplete(anime, airedSoFar, hasAired))
            {
                entry.Status = WatchStatus.Completed;
                entry.CompletedAt ??= today; // never overwrites an existing finish date — see ApplyStatus
                if (originalStatus == WatchStatus.Rewatching)
                    entry.RewatchCount++;
                changes.Add((ActivityChangeType.Completed, ActivityDetail.Completed, null));
            }
            // The strict Completed rule (design.md D3): a count drop moves the
            // entry out of Completed — into Rewatching where a rewatch is
            // possible (the anime has finished airing and this same entry, still
            // Completed here, always satisfies D0's "finished once" clause),
            // Watching otherwise — which is also the only outcome for a
            // currently-airing anime, since Rewatching's own eligibility rule
            // refuses anything that hasn't finished airing.
            else if (originalStatus == WatchStatus.Completed && newEpisodes < target)
            {
                entry.Status = RewatchingEligibility.IsEligible(anime, entry, airedSoFar) ? WatchStatus.Rewatching : WatchStatus.Watching;
                changes.Add((ActivityChangeType.StatusChanged, ActivityDetail.StatusChange(originalStatus, entry.Status), null));
            }
        }

        // Resume-to-Watching (design.md D4): raising the count without reaching
        // everything available is a statement that the anime is being watched
        // now. Lives outside the completionTarget guard above — unlike the two
        // arms inside it, this one still applies when the target is unknown,
        // since a raise can then reach nothing by definition. HasStatus (not
        // just Status) gates this so an explicit resend of the stored status
        // suppresses the rule (design.md D5), which the two arms above don't
        // need since an equal Status leaves them alone regardless.
        if (newEpisodes > previousEpisodesWatched
            && newEpisodes != completionTarget
            && originalStatus is not (WatchStatus.Watching or WatchStatus.Rewatching or WatchStatus.Completed)
            && !request.HasStatus)
        {
            entry.Status = WatchStatus.Watching;
            changes.Add((ActivityChangeType.StatusChanged, ActivityDetail.StatusChange(originalStatus, entry.Status), null));
        }
    }

    private static void ApplyStatus(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, AnimeMetadata anime, int animeId, WatchStatus originalStatus, DateOnly today,
        int? airedSoFar, bool hasAired,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.Status is not { } newStatus || newStatus == originalStatus)
            return;

        // gate-editing-on-aired-episodes: Completed, Dropped, and On hold all
        // record having settled the anime, which nothing has aired to settle
        // yet. Rewatching needs no entry here — RewatchingEligibility already
        // refuses it whenever the anime hasn't finished airing, which every
        // anime with nothing aired satisfies trivially.
        if (!hasAired && newStatus is WatchStatus.Completed or WatchStatus.Dropped or WatchStatus.OnHold)
            throw new StatusRequiresAiredEpisodeException(animeId, newStatus);

        if (newStatus == WatchStatus.Completed)
        {
            // design.md D4: Completed means every episode of the total has
            // aired and been watched. An unknown total refuses outright;
            // a known total that hasn't fully aired yet (CanComplete, which
            // reads AiredEpisodeGate.EverythingHasAired rather than MAL's own
            // routinely-stale airing status) refuses too, rather than filling
            // to the aired-so-far count as "done".
            if (anime.TotalEpisodes is not { } total)
                throw new CannotCompleteUnknownEpisodeCountException(animeId);
            if (!CanComplete(anime, airedSoFar, hasAired))
                throw new CannotCompleteBeforeFullyAiredException(animeId);

            entry.CompletedAt ??= today; // never overwrites an existing finish date — mirrors the StartedAt rule, and MAL itself keeps the original finish_date across rewatches
            entry.EpisodesWatched = total; // mirror MAL's own UI, which auto-fills episodes on completion

            // Ending a rewatch early (design.md D2): choosing Completed while still
            // partway through only counts the rewatch when the user says it does —
            // unlike watching it to the end, which always counts (see ApplyEpisodesWatched).
            if (originalStatus == WatchStatus.Rewatching && request.CountsAsRewatch == true)
                entry.RewatchCount++;

            changes.Add((ActivityChangeType.Completed, ActivityDetail.Completed, null));
        }
        else if (newStatus == WatchStatus.Rewatching)
        {
            if (!RewatchingEligibility.IsEligible(anime, entry, airedSoFar))
                throw new RewatchingNotEligibleException(animeId, RewatchingEligibility.IneligibilityReason(anime, entry, airedSoFar));

            entry.EpisodesWatched = 0; // entering Rewatching restarts progress (design.md D1); dates/score/rewatch count are left alone
            changes.Add((ActivityChangeType.StatusChanged, ActivityDetail.StatusChange(entry.Status, newStatus), null));
        }
        else
        {
            changes.Add((ActivityChangeType.StatusChanged, ActivityDetail.StatusChange(entry.Status, newStatus), null));
        }

        entry.Status = newStatus;
    }

    // Runs after ApplyEpisodesWatchedAsync/ApplyStatus so an explicit manual
    // date in this same request wins over whatever those steps auto-filled —
    // the automatic rules only ever fill an empty date (entry.StartedAt is
    // null / entry.CompletedAt ??=), never overwrite one, so a manual value
    // sent alongside a completing edit is untouched by them.
    private static void ApplyDates(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, DateOnly today,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        // Neither date may be set into the future — checked against the submitted
        // values before either is applied, so a request touching only one field
        // never trips over a pre-existing stored date it didn't send.
        if (request.HasStartedAt && request.StartedAt > today)
            throw new EntryEditRejectedException("Start date cannot be in the future.");
        if (request.HasCompletedAt && request.CompletedAt > today)
            throw new EntryEditRejectedException("Finish date cannot be in the future.");

        if (request.HasStartedAt && request.StartedAt != entry.StartedAt)
        {
            entry.StartedAt = request.StartedAt;
            changes.Add((ActivityChangeType.StartDateChanged, ActivityDetail.StartDate(entry.StartedAt), null));
        }

        if (request.HasCompletedAt && request.CompletedAt != entry.CompletedAt)
        {
            entry.CompletedAt = request.CompletedAt;
            changes.Add((ActivityChangeType.FinishDateChanged, ActivityDetail.FinishDate(entry.CompletedAt), null));
        }

        // Compares the resulting (post-edit) values, not the stored ones, so
        // this catches e.g. a finish date set earlier than a start date left
        // unchanged from a prior edit, not just both dates changed together.
        if (entry.StartedAt is { } start && entry.CompletedAt is { } finish && finish < start)
            throw new EntryEditRejectedException("Finish date cannot be earlier than start date.");
    }

    private static void ApplyScore(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, int animeId, bool hasAired,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.MyScore is not { } newScore || newScore == (entry.MyScore ?? 0))
            return;

        if (newScore is < 0 or > 10)
            throw new EntryEditRejectedException("Score must be between 0 and 10.");

        // Clearing back to "no score" (0) always stays possible even with
        // nothing aired, so an entry that already carries one is never trapped.
        if (!hasAired && newScore > 0)
            throw new ScoreRequiresAiredEpisodeException(animeId);

        entry.MyScore = newScore == 0 ? null : newScore; // MAL convention: 0 means "no score"
        changes.Add((ActivityChangeType.ScoreChanged, ActivityDetail.Score(entry.MyScore), null));
    }

    private static void ApplyRewatchCount(
        UserAnimeEntryEditRequest request, UserAnimeEntry entry, int animeId, bool hasAired,
        List<(ActivityChangeType Type, string Detail, int? PreviousEpisodesWatched)> changes)
    {
        if (request.RewatchCount is not { } newRewatchCount || newRewatchCount == entry.RewatchCount)
            return;

        if (newRewatchCount is < 0 or > 100)
            throw new EntryEditRejectedException("Rewatch count must be between 0 and 100.");

        // Clearing back to 0 always stays possible even with nothing aired,
        // so an entry that already carries a rewatch count is never trapped.
        if (!hasAired && newRewatchCount > 0)
            throw new RewatchCountRequiresAiredEpisodeException(animeId);

        entry.RewatchCount = newRewatchCount;
        changes.Add((ActivityChangeType.RewatchCountChanged, ActivityDetail.RewatchCount(newRewatchCount), null));
    }
}
