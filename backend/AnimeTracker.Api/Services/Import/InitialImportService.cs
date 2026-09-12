using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Jobs;
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
/// the missing entry backfilled, without a redundant details fetch.
///
/// Only anime this device's list actually lacks count as work (design.md D8):
/// a run that finds nothing missing ends quietly, and an anime whose removal
/// is still pending is left alone rather than re-added.</summary>
public class InitialImportService(
    IMalClient malClient,
    AnimeTrackerDbContext db,
    ListImportProgress progress,
    ILogger<InitialImportService> logger) : IInitialImportService
{
    public async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Starting MAL list import.");

        // A device with no entries at all is certain to have work, so it's
        // shown from the moment the run starts rather than staying quiet
        // until the list has been read (design.md D8 point 1).
        progress.BeginRun(visibleFromStart: !await db.UserAnimeEntries.AnyAsync(ct));

        List<MalUserAnimeListEdge> edges;
        HashSet<int> existingAnimeIds;
        HashSet<int> existingEntryIds;
        HashSet<int> pendingDeletionAnimeIds;

        try
        {
            edges = await malClient.GetFullUserAnimeListAsync(ct: ct);
            existingAnimeIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();
            existingEntryIds = (await db.UserAnimeEntries.Select(e => e.AnimeId).ToListAsync(ct)).ToHashSet();
            pendingDeletionAnimeIds = (await db.PendingEntryDeletions.Select(d => d.AnimeId).ToListAsync(ct)).ToHashSet();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            progress.FailBeforeRead(JobFailure.Describe(ex, "MyAnimeList"));
            throw;
        }

        // Work is an anime this device's list lacks — a full-detail fetch for
        // one with no cached AnimeMetadata, or just the missing entry for one
        // it already has cached. An anime whose removal hasn't reached MAL
        // yet is neither: MAL still listing it is the expected in-flight
        // state, and re-adding it would undo the removal (spec "The import
        // leaves a pending removal alone").
        var work = edges
            .Where(edge => !pendingDeletionAnimeIds.Contains(edge.Node.Id))
            .Where(edge => !existingAnimeIds.Contains(edge.Node.Id) || !existingEntryIds.Contains(edge.Node.Id))
            .ToList();

        if (work.Count == 0)
        {
            progress.EndWithoutWork();
            logger.LogInformation("MAL list import found nothing missing.");
            return;
        }

        progress.Reveal(work.Count);

        var done = 0;
        var failed = 0;

        foreach (var edge in work)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            if (!existingAnimeIds.Contains(animeId))
            {
                try
                {
                    await ImportOneAsync(animeId, edge.ListStatus, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogError(ex, "Failed to import anime {AnimeId} ({Title}); it will be retried on the next import run.",
                        animeId, edge.Node.Title);
                    failed++;
                    continue; // leave it missing from AnimeMetadata so the next run retries it
                }
            }
            else
            {
                // Metadata was already cached (e.g. from browsing a season before ever
                // connecting MAL) but the list entry itself was never created for it —
                // add just that, without re-fetching anime details.
                var now = DateTimeOffset.UtcNow;
                var entry = MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, now);
                db.UserAnimeEntries.Add(entry);
                await db.SaveChangesAsync(ct);
            }

            done++;
            progress.ReportProgress(done);
        }

        if (failed == 0)
        {
            progress.Complete();
            logger.LogInformation("MAL list import complete: {Done}/{Total} anime imported.", done, work.Count);
        }
        else
        {
            progress.Fail($"{failed} of {work.Count} anime couldn't be fetched.");
            logger.LogInformation(
                "MAL list import ended with failures: {Done}/{Total} anime imported, {Failed} couldn't be fetched.",
                done, work.Count, failed);
        }
    }

    private async Task ImportOneAsync(int animeId, MalListStatus? listStatus, CancellationToken ct)
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

        await db.SaveChangesAsync(ct);
    }
}
