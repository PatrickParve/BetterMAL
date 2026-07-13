using AnimeTracker.Api.Data;
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
        var synced = 0;

        foreach (var edge in edges)
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
                    continue; // leave it missing from AnimeMetadata so the next run retries it
                }
            }
            else if (!existingEntryIds.Contains(animeId))
            {
                // Metadata was already cached (e.g. from browsing a season before ever
                // connecting MAL) but the list entry itself was never created for it —
                // add just that, without re-fetching anime details.
                db.UserAnimeEntries.Add(MalMappingExtensions.ToUserAnimeEntry(animeId, edge.ListStatus, DateTimeOffset.UtcNow));
                await db.SaveChangesAsync(ct);
            }

            synced++;
            progress.ReportProgress(synced);
        }

        progress.Complete();
        logger.LogInformation("MAL list import complete: {Synced}/{Total} anime synced.", synced, edges.Count);
    }

    private async Task ImportOneAsync(int animeId, MalListStatus? listStatus, CancellationToken ct)
    {
        var details = await malClient.GetAnimeDetailsAsync(animeId, ct: ct);
        var now = DateTimeOffset.UtcNow;

        db.AnimeMetadata.Add(details.ToAnimeMetadata(now));
        db.UserAnimeEntries.Add(MalMappingExtensions.ToUserAnimeEntry(animeId, listStatus, now));

        await db.SaveChangesAsync(ct);
    }
}
