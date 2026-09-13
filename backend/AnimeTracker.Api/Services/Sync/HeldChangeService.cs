using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

public class HeldChangeService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    IEntryPushService entryPushService,
    ILogger<HeldChangeService> logger) : IHeldChangeService
{
    // design.md D4: "capped at the most recent few per item, with a count of
    // the remainder", so one heavily-edited entry never turns its review row
    // into a wall.
    private const int RecentChangeCap = 5;

    public async Task<List<HeldChangeDto>> GetHeldAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var results = new List<HeldChangeDto>();

        var heldEntries = await db.UserAnimeEntries
            .Include(e => e.Anime)
            .Where(e => e.PendingSync && e.HeldForReviewAt != null)
            .ToListAsync(ct);

        foreach (var entry in heldEntries)
        {
            var (remoteStatus, remoteUnavailable) = await TryGetRemoteStatusAsync(entry.AnimeId, ct);

            // design.md D8a: judged over the same six pushed fields
            // MalStatusResolution.MatchesRemote already compares, via the
            // same "map MAL's status through ToUserAnimeEntry" pattern
            // ReconciliationService uses — remoteStatus is null here (no MAL
            // read failure but MyAnimeList has no entry at all) is a genuine
            // difference, not agreement, so it is excluded from clearing.
            if (!remoteUnavailable && remoteStatus is not null)
            {
                var remoteEntry = MalMappingExtensions.ToUserAnimeEntry(entry.AnimeId, remoteStatus, now);
                if (MalStatusResolution.MatchesRemote(entry, remoteEntry))
                {
                    entry.PendingSync = false;
                    entry.HeldForReviewAt = null;
                    entry.LastSyncedAt = now;
                    continue; // no activity recorded — nothing stored changed
                }
            }

            var (recent, additionalCount) = await GetUnsentChangesAsync(entry.AnimeId, entry.LastSyncedAt, ct);
            results.Add(new HeldChangeDto(
                entry.AnimeId,
                entry.Anime.Title,
                entry.Anime.EnglishTitle,
                entry.Anime.PictureUrl,
                HeldChangeKind.Entry,
                entry.HeldForReviewAt!.Value,
                HeldChangeValuesDto.FromEntry(entry),
                recent,
                additionalCount,
                remoteStatus is null ? null : HeldChangeValuesDto.FromEntry(MalMappingExtensions.ToUserAnimeEntry(entry.AnimeId, remoteStatus, now)),
                remoteUnavailable));
        }

        var heldRemovals = await db.PendingEntryDeletions
            .Include(d => d.Anime)
            .Where(d => d.HeldForReviewAt != null)
            .ToListAsync(ct);

        foreach (var removal in heldRemovals)
        {
            var (remoteStatus, remoteUnavailable) = await TryGetRemoteStatusAsync(removal.AnimeId, ct);

            // design.md D8a: a queued removal agrees with MyAnimeList exactly
            // when MyAnimeList no longer lists the anime at all.
            if (!remoteUnavailable && remoteStatus is null)
            {
                db.PendingEntryDeletions.Remove(removal);
                continue;
            }

            // The removal's own request is its one "unsent change" — there is
            // no field-by-field history to summarize for a deletion.
            var recent = new List<HeldChangeRecentChangeDto>
            {
                new(ActivityChangeType.Removed, ActivityDetail.Removed, removal.RequestedAt),
            };

            results.Add(new HeldChangeDto(
                removal.AnimeId,
                removal.Anime.Title,
                removal.Anime.EnglishTitle,
                removal.Anime.PictureUrl,
                HeldChangeKind.Removal,
                removal.HeldForReviewAt!.Value,
                null,
                recent,
                0,
                remoteStatus is null ? null : HeldChangeValuesDto.FromEntry(MalMappingExtensions.ToUserAnimeEntry(removal.AnimeId, remoteStatus, now)),
                remoteUnavailable));
        }

        await db.SaveChangesAsync(ct);
        return results;
    }

    public async Task<HeldChangeDecisionResult> AcceptAsync(int animeId, CancellationToken ct = default)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId && e.PendingSync && e.HeldForReviewAt != null, ct);
        if (entry is not null)
        {
            entry.HeldForReviewAt = null;
            await db.SaveChangesAsync(ct);

            // design.md D5: a failed push here is not a decision failure — it
            // just leaves the (now unheld) entry pending for the retry job.
            await entryPushService.PushIfPendingAsync(animeId, ct);
            return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Applied);
        }

        var removal = await db.PendingEntryDeletions.FirstOrDefaultAsync(d => d.AnimeId == animeId && d.HeldForReviewAt != null, ct);
        if (removal is not null)
        {
            removal.HeldForReviewAt = null;
            await db.SaveChangesAsync(ct);

            await entryPushService.PushPendingDeletionAsync(animeId, ct);
            return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Applied);
        }

        return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.NotHeld);
    }

    public async Task<HeldChangeDecisionResult> DeclineAsync(int animeId, CancellationToken ct = default)
    {
        var entry = await db.UserAnimeEntries.FirstOrDefaultAsync(e => e.AnimeId == animeId && e.PendingSync && e.HeldForReviewAt != null, ct);
        if (entry is not null)
            return await DeclineEntryAsync(entry, ct);

        var removal = await db.PendingEntryDeletions.FirstOrDefaultAsync(d => d.AnimeId == animeId && d.HeldForReviewAt != null, ct);
        if (removal is not null)
            return await DeclineRemovalAsync(removal, ct);

        return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.NotHeld);
    }

    public async Task<HeldChangeBulkResult> AcceptAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
    {
        var animeIds = await GetHeldAnimeIdsAsync(ct);
        progress?.SetTotal(animeIds.Count);
        int succeeded = 0, stillHeld = 0, done = 0;

        foreach (var animeId in animeIds)
        {
            var result = await AcceptAsync(animeId, ct);
            if (result.Outcome == HeldChangeDecisionOutcome.Applied) succeeded++;
            else stillHeld++;

            progress?.ReportProgress(++done);
        }

        return new HeldChangeBulkResult(succeeded, stillHeld);
    }

    public async Task<HeldChangeBulkResult> DeclineAllAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
    {
        var animeIds = await GetHeldAnimeIdsAsync(ct);
        progress?.SetTotal(animeIds.Count);
        int succeeded = 0, stillHeld = 0, done = 0;

        foreach (var animeId in animeIds)
        {
            var result = await DeclineAsync(animeId, ct);
            if (result.Outcome == HeldChangeDecisionOutcome.Applied) succeeded++;
            else stillHeld++;

            progress?.ReportProgress(++done);
        }

        return new HeldChangeBulkResult(succeeded, stillHeld);
    }

    // design.md D7: an unrecognized MyAnimeList list status is thrown here as
    // an unreadable read, inside the same try blocks that already handle a
    // genuine read failure — so R2's "never guess" rule and D8's "unreadable
    // = best-effort" rule stay in exactly one place each.
    private async Task<MalListStatus?> ReadRemoteStatusAsync(int animeId, CancellationToken ct)
    {
        var status = await malClient.GetMyListStatusAsync(animeId, ct);
        if (status is not null && !status.HasRecognizedStatus())
            throw new InvalidDataException($"MyAnimeList gave list status '{status.Status}' for anime {animeId}, which this app doesn't recognize.");

        return status;
    }

    // design.md D6: declining an entry MyAnimeList still lists overwrites the
    // six pushed fields with MyAnimeList's current value (via the shared
    // ApplyTo mapping, resolved through the same rewatch-preserving rule
    // every other MAL read-back uses); an entry MyAnimeList has no list entry
    // for is a local addition that never reached it, so adopting "not on my
    // list" means deleting it locally, with no PendingEntryDeletion queued
    // since MyAnimeList is already in the desired state.
    private async Task<HeldChangeDecisionResult> DeclineEntryAsync(UserAnimeEntry entry, CancellationToken ct)
    {
        MalListStatus? remoteStatus;
        try
        {
            remoteStatus = await ReadRemoteStatusAsync(entry.AnimeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read MyAnimeList's current status for anime {AnimeId} while declining a held change; leaving it held.", entry.AnimeId);
            return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Failed, "Could not read MyAnimeList's current value.");
        }

        var now = DateTimeOffset.UtcNow;

        if (remoteStatus is null)
        {
            db.UserAnimeEntries.Remove(entry);
            db.ActivityLogs.Add(new ActivityLog
            {
                AnimeId = entry.AnimeId,
                Timestamp = now,
                ChangeType = ActivityChangeType.Removed,
                ChangeDetail = ActivityDetail.Removed,
            });
            await db.SaveChangesAsync(ct);
            return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Applied);
        }

        var before = EntrySnapshot.Of(entry);
        remoteStatus.ApplyTo(entry, now);
        entry.PendingSync = false;
        entry.HeldForReviewAt = null;

        db.ActivityLogs.AddRange(EntryActivityRecorder.Diff(entry.AnimeId, before, entry, now));

        await db.SaveChangesAsync(ct);
        return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Applied);
    }

    // design.md D7: declining a held removal drops the queued deletion and,
    // where MyAnimeList still lists the anime, restores the local entry from
    // MyAnimeList's current value; where it does not, the removal is simply
    // dropped, since local and remote already agree.
    private async Task<HeldChangeDecisionResult> DeclineRemovalAsync(PendingEntryDeletion removal, CancellationToken ct)
    {
        MalListStatus? remoteStatus;
        try
        {
            remoteStatus = await ReadRemoteStatusAsync(removal.AnimeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read MyAnimeList's current status for anime {AnimeId} while declining a held removal; leaving it held.", removal.AnimeId);
            return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Failed, "Could not read MyAnimeList's current value.");
        }

        var now = DateTimeOffset.UtcNow;
        db.PendingEntryDeletions.Remove(removal);

        if (remoteStatus is not null)
        {
            var restored = MalMappingExtensions.ToUserAnimeEntry(removal.AnimeId, remoteStatus, now);
            db.UserAnimeEntries.Add(restored);
            db.ActivityLogs.Add(EntryActivityRecorder.Added(removal.AnimeId, restored.Status, now));
        }

        await db.SaveChangesAsync(ct);
        return new HeldChangeDecisionResult(HeldChangeDecisionOutcome.Applied);
    }

    private async Task<List<int>> GetHeldAnimeIdsAsync(CancellationToken ct)
    {
        var entryIds = await db.UserAnimeEntries.AsNoTracking()
            .Where(e => e.PendingSync && e.HeldForReviewAt != null)
            .Select(e => e.AnimeId)
            .ToListAsync(ct);

        var removalIds = await db.PendingEntryDeletions.AsNoTracking()
            .Where(d => d.HeldForReviewAt != null)
            .Select(d => d.AnimeId)
            .ToListAsync(ct);

        return entryIds.Concat(removalIds).Distinct().ToList();
    }

    // design.md D8: best-effort — an item whose read fails renders with its
    // local side and a note that MyAnimeList is unavailable, rather than
    // failing the whole review. An unrecognized MyAnimeList list status
    // counts as unreadable here too (report-partial-runs-and-mal-side-removals
    // design.md D7): ReadRemoteStatusAsync throws for it, so it's handled by
    // the same catch as a genuine read failure.
    private async Task<(MalListStatus? RemoteStatus, bool Unavailable)> TryGetRemoteStatusAsync(int animeId, CancellationToken ct)
    {
        try
        {
            return (await ReadRemoteStatusAsync(animeId, ct), false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to read MyAnimeList's current status for anime {AnimeId} while building the held-change review; showing it with no MyAnimeList side.", animeId);
            return (null, true);
        }
    }

    private async Task<(List<HeldChangeRecentChangeDto> Recent, int AdditionalCount)> GetUnsentChangesAsync(
        int animeId, DateTimeOffset? lastSyncedAt, CancellationToken ct)
    {
        var query = db.ActivityLogs.AsNoTracking()
            .Where(l => l.AnimeId == animeId);

        if (lastSyncedAt is { } since)
            query = query.Where(l => l.Timestamp > since);

        var all = await query
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .ToListAsync(ct);

        var recent = all.Take(RecentChangeCap)
            .Select(l => new HeldChangeRecentChangeDto(l.ChangeType, l.ChangeDetail, l.Timestamp))
            .ToList();

        return (recent, Math.Max(0, all.Count - RecentChangeCap));
    }
}
