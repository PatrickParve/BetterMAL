using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

public class ReconciliationService(
    IMalClient malClient,
    AnimeTrackerDbContext db,
    ILogger<ReconciliationService> logger) : IReconciliationService
{
    public async Task<ReconciliationResult> RunAsync(CancellationToken ct = default)
    {
        var remoteEdges = await malClient.GetFullUserAnimeListAsync(ct);
        var now = DateTimeOffset.UtcNow;

        var localEntries = await db.UserAnimeEntries.ToDictionaryAsync(e => e.AnimeId, ct);
        var existingAnimeIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();

        int added = 0, updated = 0, unchanged = 0, skippedPending = 0;
        var diffEntries = new List<PendingReconciliationDiffEntry>();

        foreach (var edge in remoteEdges)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            // Anime missing locally (e.g. added directly on MAL's site) needs a
            // metadata row cached — this is just cache data, not user data, so
            // it's kept up to date immediately rather than held for review.
            if (existingAnimeIds.Add(animeId))
                db.AnimeMetadata.Add(edge.Node.ToAnimeMetadata(now));

            var remote = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);

            if (!localEntries.TryGetValue(animeId, out var local))
            {
                diffEntries.Add(ToDiffEntry(animeId, ReconciliationDiffChangeType.Added, remote));
                added++;
                continue;
            }

            if (local.PendingSync)
            {
                skippedPending++;
                continue;
            }

            if (local.Status == remote.Status && local.EpisodesWatched == remote.EpisodesWatched &&
                local.MyScore == remote.MyScore && local.RewatchCount == remote.RewatchCount &&
                local.StartedAt == remote.StartedAt && local.CompletedAt == remote.CompletedAt)
            {
                unchanged++;
                continue;
            }

            diffEntries.Add(ToDiffEntry(animeId, ReconciliationDiffChangeType.Updated, remote));
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
            "Reconciliation complete: {Added} added, {Updated} updated, {Unchanged} unchanged, {Skipped} skipped (pending local edits) — diff held for review.",
            added, updated, unchanged, skippedPending);

        return new ReconciliationResult(added, updated, unchanged, skippedPending);
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

            local.Status = entry.Status;
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

    private static PendingReconciliationDiffEntry ToDiffEntry(int animeId, ReconciliationDiffChangeType changeType, UserAnimeEntry remote) => new()
    {
        AnimeId = animeId,
        ChangeType = changeType,
        Status = remote.Status,
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
