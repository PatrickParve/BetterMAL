using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

public class ReconciliationService(
    IMalClient malClient,
    AnimeTrackerDbContext db,
    ReconciliationRunGate runGate,
    ILogger<ReconciliationService> logger) : IReconciliationService
{
    public async Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default)
    {
        await runGate.WaitAsync(ct);
        try
        {
            return await RunLockedAsync(progress, ct);
        }
        finally
        {
            runGate.Release();
        }
    }

    private async Task<ReconciliationResult> RunLockedAsync(IJobProgressSink? progress, CancellationToken ct)
    {
        // MyAnimeList never says how long the list is, so this never calls
        // SetTotal — the run stays "Starting…"/"N anime read" throughout
        // (design.md D5).
        var remoteEdges = await malClient.GetFullUserAnimeListAsync(
            onPageRead: done => progress?.ReportProgress(done), ct: ct);
        var now = DateTimeOffset.UtcNow;

        var localEntries = await db.UserAnimeEntries.ToDictionaryAsync(e => e.AnimeId, ct);
        var existingAnimeIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var pendingDeletionAnimeIds = (await db.PendingEntryDeletions.Select(d => d.AnimeId).ToListAsync(ct)).ToHashSet();

        int added = 0, updated = 0, unchanged = 0, skippedPending = 0, skippedRemoval = 0;
        var diffEntries = new List<PendingReconciliationDiffEntry>();

        foreach (var edge in remoteEdges)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            // MAL still listing an anime whose removal hasn't been pushed yet
            // is an expected in-flight state, not a difference to review —
            // including it would offer to restore the entry the user just deleted.
            if (pendingDeletionAnimeIds.Contains(animeId))
            {
                skippedRemoval++;
                continue;
            }

            // Anime missing locally (e.g. added directly on MAL's site) needs a
            // metadata row cached — this is just cache data, not user data, so
            // it's added immediately rather than held for review. The my-list
            // payload only carries listing fields (no synopsis/genres/related),
            // so cache it lean: LastSyncedAt stays default and the detail page
            // fills in the rich fields via a full fetch on first visit — same as
            // a Season/Top-Anime browse row. Stamping the full ToAnimeMetadata
            // here would set LastSyncedAt on a rich-field-empty row and suppress
            // that upgrade.
            //
            // This is create-only, and deliberately stays that way: an anime
            // already cached is left untouched here, because reconciliation is
            // about the user's *entries* — refreshing an existing row's metadata
            // is the metadata refresh's job, on its own cadence. So unlike the
            // Season/Top-Anime browses, which do re-write existing rows leanly
            // and therefore now detect (spec "Every path that writes anime data
            // detects the updates it can"), there is nothing here to detect
            // against: creating a row is a first observation, and the else-path
            // writes no anime data at all. A detector call could never fire.
            if (existingAnimeIds.Add(animeId))
                db.AnimeMetadata.Add(edge.Node.ToLeanAnimeMetadata(now));

            var remote = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);

            if (!localEntries.TryGetValue(animeId, out var local))
            {
                diffEntries.Add(ToDiffEntry(animeId, ReconciliationDiffChangeType.Added, remote, remote.Status));
                added++;
                continue;
            }

            if (local.PendingSync)
            {
                skippedPending++;
                continue;
            }

            // mal-write-sync: a Rewatching entry is pushed to MAL as `watching`
            // (design.md D4), so MAL reports it back as `watching` too — treated
            // by MalStatusResolution.MatchesRemote as matching rather than a
            // difference, so reconciliation never demotes a rewatch back to
            // Watching on that basis alone.
            if (MalStatusResolution.MatchesRemote(local, remote))
            {
                unchanged++;
                continue;
            }

            // The held diff records the status the entry will end up with
            // (design.md D2), not MAL's raw `watching` — otherwise accepting a
            // diff raised by some other field would demote a rewatch.
            var resolvedStatus = MalStatusResolution.ResolveAgainstLocal(local.Status, remote.Status);
            diffEntries.Add(ToDiffEntry(animeId, ReconciliationDiffChangeType.Updated, remote, resolvedStatus));
            updated++;
        }

        // Only the most recent run's diff is ever held for review — replace
        // whatever a previous, still-unreviewed run left behind.
        var previousDiffs = await db.PendingReconciliationDiffs.ToListAsync(ct);
        db.PendingReconciliationDiffs.RemoveRange(previousDiffs);

        if (diffEntries.Count > 0)
            db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff { ComputedAt = now, Entries = diffEntries });

        await db.SaveChangesAsync(ct);
        logger.LogInformation(
            "Reconciliation complete: {Added} added, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped (pending local edits), {SkippedRemovals} skipped (pending removal) — diff held for review.",
            added, updated, unchanged, skippedPending, skippedRemoval);

        return new ReconciliationResult(added, updated, unchanged, skippedPending, skippedRemoval);
    }

    public async Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default)
    {
        var diff = await db.PendingReconciliationDiffs
            .Include(d => d.Entries)
            .AsNoTracking()
            .OrderByDescending(d => d.ComputedAt)
            .FirstOrDefaultAsync(ct);

        if (diff is null)
            return null;

        var animeIds = diff.Entries.Select(e => e.AnimeId).ToList();
        var titles = await db.AnimeMetadata.AsNoTracking()
            .Where(a => animeIds.Contains(a.Id))
            .Select(a => new { a.Id, a.Title, a.PictureUrl })
            .ToDictionaryAsync(a => a.Id, ct);

        return ToDto(diff, titles.ToDictionary(kv => kv.Key, kv => (kv.Value.Title, kv.Value.PictureUrl)));
    }

    public async Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default)
    {
        var diff = await db.PendingReconciliationDiffs.Include(d => d.Entries).FirstOrDefaultAsync(ct);
        if (diff is null)
            return false;

        var animeIds = diff.Entries.Select(e => e.AnimeId).ToList();
        var localEntries = await db.UserAnimeEntries
            .Where(e => animeIds.Contains(e.AnimeId))
            .ToDictionaryAsync(e => e.AnimeId, ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var entry in diff.Entries)
        {
            if (localEntries.TryGetValue(entry.AnimeId, out var local))
            {
                // Mirror the compute-time guard: don't let a stale diff overwrite a
                // local edit made (and not yet pushed) after this diff was computed.
                if (local.PendingSync)
                    continue;
            }
            else
            {
                local = new UserAnimeEntry { AnimeId = entry.AnimeId };
                db.UserAnimeEntries.Add(local);
            }

            // Re-resolve against the entry as it stands now, not as it stood
            // when the diff was computed (design.md D2) — it may have become a
            // rewatch in the meantime, and a stale diff must not demote that.
            local.Status = MalStatusResolution.ResolveAgainstLocal(local.Status, entry.Status);
            local.EpisodesWatched = entry.EpisodesWatched;
            local.MyScore = entry.MyScore;
            local.StartedAt = entry.StartedAt;
            local.CompletedAt = entry.CompletedAt;
            local.RewatchCount = entry.RewatchCount;
            local.PendingSync = false;
            local.LastSyncedAt = now;
        }

        db.PendingReconciliationDiffs.Remove(diff);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelPendingDiffAsync(CancellationToken ct = default)
    {
        var diffs = await db.PendingReconciliationDiffs.ToListAsync(ct);
        if (diffs.Count == 0)
            return false;

        db.PendingReconciliationDiffs.RemoveRange(diffs);
        await db.SaveChangesAsync(ct);
        return true;
    }

    private static PendingReconciliationDiffEntry ToDiffEntry(int animeId, ReconciliationDiffChangeType changeType, UserAnimeEntry remote, WatchStatus status) => new()
    {
        AnimeId = animeId,
        ChangeType = changeType,
        Status = status,
        EpisodesWatched = remote.EpisodesWatched,
        MyScore = remote.MyScore,
        StartedAt = remote.StartedAt,
        CompletedAt = remote.CompletedAt,
        RewatchCount = remote.RewatchCount,
    };

    private static PendingReconciliationDiffDto ToDto(
        PendingReconciliationDiff diff, IReadOnlyDictionary<int, (string Title, string? PictureUrl)> titles) => new(
        diff.Id,
        diff.ComputedAt,
        diff.Entries.Select(e =>
        {
            var (title, pictureUrl) = titles.TryGetValue(e.AnimeId, out var found) ? found : (e.AnimeId.ToString(), null);
            return new PendingReconciliationDiffEntryDto(
                e.AnimeId, title, pictureUrl, e.ChangeType.ToString(), e.Status.ToString(),
                e.EpisodesWatched, e.MyScore, e.StartedAt, e.CompletedAt, e.RewatchCount);
        }).ToList());
}
