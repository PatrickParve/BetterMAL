using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Import;

/// <summary>Pages the full MAL list, then fetches full metadata per-anime at
/// the shared pacer's rate (~1 req/s), inserting each anime into Postgres as
/// its fetch completes. Resumable: anime already present in AnimeMetadata skip
/// the re-fetch, so a restart mid-import continues rather than starting over.
/// Separately, an anime whose metadata was already cached (e.g. from browsing
/// a season before ever connecting MAL) but has no list entry yet gets just
/// the missing entry backfilled, without a redundant details fetch.</summary>
public class InitialImportService(
    IMalClient malClient,
    AnimeTrackerDbContext db,
    IImportProgressTracker progress,
    ILogger<InitialImportService> logger) : IInitialImportService
{
    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting MAL list import.");

        var edges = await malClient.GetFullUserAnimeListAsync(ct);
        progress.Start(edges.Count);

        var existingAnimeIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var existingEntryIds = (await db.UserAnimeEntries.Select(e => e.AnimeId).ToListAsync(ct)).ToHashSet();
        // "No history yet" rather than "first run" (design D8): the import is
        // resumable, so an interrupted first run must still resume unrecorded
        // rather than flooding the log with the remainder. Judged once, from
        // the state at this run's start, so an edit made mid-import — which
        // writes the first row — doesn't make this same run start recording
        // halfway through.
        var isBaselineRun = !await db.ActivityLogs.AnyAsync(ct);
        var synced = 0;

        foreach (var edge in edges)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            if (!existingAnimeIds.Contains(animeId))
            {
                try
                {
                    await ImportOneAsync(animeId, edge.ListStatus, isBaselineRun, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to import anime {AnimeId} ({Title}); it will be retried on the next import run.",
                        animeId, edge.Node.Title);
                    continue; // leave it missing from AnimeMetadata so the next run retries it
                }
            }
            else if (!existingEntryIds.Contains(animeId))
            {
                // Metadata was already cached (e.g. from browsing a season before ever
                // connecting MAL) but the list entry itself was never created for it —
                // add just that, without re-fetching anime details.
                var now = DateTimeOffset.UtcNow;
                var entry = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);
                db.UserAnimeEntries.Add(entry);
                if (!isBaselineRun)
                    db.ActivityLogs.Add(EntryActivityRecorder.Added(animeId, entry.Status, ActivityChangeSource.MalStartupImport, now));
                await db.SaveChangesAsync(ct);
            }

            synced++;
            progress.ReportProgress(synced);
        }

        progress.Complete();
        logger.LogInformation("MAL list import complete: {Synced}/{Total} anime synced.", synced, edges.Count);
    }

    private async Task ImportOneAsync(int animeId, MalListStatus? listStatus, bool isBaselineRun, CancellationToken ct)
    {
        // Every anime this method imports is, by definition, a my-list entry
        // (it's built from the user's own MAL list), so it always qualifies
        // for the with-pictures field set (design.md D4a).
        var details = await malClient.GetAnimeDetailsAsync(
            animeId, fields: [MalClient.FullDetailWithPicturesAnimeFields], ct: ct);
        var now = DateTimeOffset.UtcNow;

        db.AnimeMetadata.Add(details.ToAnimeMetadata(now));
        var entry = MalMappingExtensions.ToUserAnimeEntry(animeId, listStatus, now);
        db.UserAnimeEntries.Add(entry);
        if (!isBaselineRun)
            db.ActivityLogs.Add(EntryActivityRecorder.Added(animeId, entry.Status, ActivityChangeSource.MalStartupImport, now));

        await db.SaveChangesAsync(ct);
    }
}
