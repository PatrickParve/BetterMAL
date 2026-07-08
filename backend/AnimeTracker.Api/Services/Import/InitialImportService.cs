using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Import;

/// <summary>Pages the full MAL list, then fetches full metadata per-anime at
/// the shared pacer's rate (~1 req/s), inserting each anime into Postgres as
/// its fetch completes. Resumable: anime already present in AnimeMetadata are
/// skipped, so a restart mid-import continues rather than starting over.</summary>
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

        var existingIds = (await db.AnimeMetadata.Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        var synced = 0;

        foreach (var edge in edges)
        {
            ct.ThrowIfCancellationRequested();
            var animeId = edge.Node.Id;

            if (!existingIds.Contains(animeId))
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
