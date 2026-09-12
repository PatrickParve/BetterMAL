using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>One-time corrective re-sync: re-fetches the entire MAL list, and for
/// every anime does a full-detail fetch and upserts AnimeMetadata (backfilling
/// English title/duration/source/broadcast/AiredFrom) and the UserAnimeEntry's
/// status/episodes/score from list_status. Unlike InitialImportService, this
/// updates anime already cached rather than skipping them; unlike
/// ReconciliationService, it applies changes immediately instead of holding a
/// diff for review, and fetches full detail per anime rather than relying on
/// the lean listing fields. Entries with unsynced local edits (PendingSync)
/// are left untouched, mirroring the guard in ReconciliationService. Every
/// already-cached anime's refresh also goes through
/// <see cref="IAnimeMetadataChangeDetector"/>, the same detection
/// MetadataRefreshService uses — a corrective re-sync is still a write to
/// cached anime metadata, so it feeds the anime-updates feed like every other
/// refresh path (spec "Every path that writes anime data detects the updates
/// it can").</summary>
public class ResyncService(
    IMalClient malClient,
    AnimeTrackerDbContext db,
    IAnimeMetadataChangeDetector changeDetector,
    ResyncProgress progress,
    ILogger<ResyncService> logger) : IResyncService
{
    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting corrective MAL re-sync.");

        var edges = await malClient.GetFullUserAnimeListAsync(ct: ct);
        progress.SetTotal(edges.Count);

        // Related-anime must be loaded before ApplyTo replaces the collection —
        // without a tracked snapshot, EF has nothing to diff against and would
        // try to re-insert rows that already exist instead of deleting stale ones.
        var existingAnime = await db.AnimeMetadata.Include(a => a.RelatedAnime).ToDictionaryAsync(a => a.Id, ct);
        var existingEntries = await db.UserAnimeEntries.ToDictionaryAsync(e => e.AnimeId, ct);

        var synced = 0;
        var skippedPending = 0;

        foreach (var edge in edges)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            try
            {
                var now = DateTimeOffset.UtcNow;
                var details = await malClient.GetAnimeDetailsAsync(animeId, ct: ct);

                if (existingAnime.TryGetValue(animeId, out var metadata))
                {
                    var before = changeDetector.Snapshot(metadata);
                    details.ApplyTo(metadata, now);
                    await changeDetector.RecordAsync(metadata, before, now, ct);
                }
                else
                {
                    metadata = details.ToAnimeMetadata(now);
                    db.AnimeMetadata.Add(metadata);
                    existingAnime[animeId] = metadata;
                }

                if (existingEntries.TryGetValue(animeId, out var entry))
                {
                    if (entry.PendingSync)
                        skippedPending++;
                    else
                        edge.ListStatus.ApplyTo(entry, now);
                }
                else
                {
                    var newEntry = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);
                    db.UserAnimeEntries.Add(newEntry);
                    existingEntries[animeId] = newEntry;
                }

                await db.SaveChangesAsync(ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                // Catches per-item HTTP failures too, including a request that
                // times out — HttpClient reports that as OperationCanceledException
                // even though our own ct was never cancelled. Excluding it here
                // (matching the ex-is-not-OperationCanceledException filter used
                // elsewhere) would silently kill this loop on the next transient
                // MAL hiccup with nothing logged.
                logger.LogError(ex, "Failed to re-sync anime {AnimeId} ({Title}); it will be retried on the next re-sync run.",
                    animeId, edge.Node.Title);
            }

            synced++;
            progress.ReportProgress(synced);
        }

        progress.Complete();
        logger.LogInformation(
            "Corrective MAL re-sync complete: {Synced}/{Total} anime processed, {Skipped} skipped (pending local edits).",
            synced, edges.Count, skippedPending);
    }
}
