using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Updates;

public class AnnouncementResolutionService(
    AnimeTrackerDbContext db,
    IMetadataRefreshService metadataRefresh,
    IAnimeUpdateRecorder updateRecorder,
    ILogger<AnnouncementResolutionService> logger) : IAnnouncementResolutionService
{
    public async Task<int> ResolveAsync(int maxAnime, CancellationToken ct = default)
    {
        if (maxAnime <= 0)
            return 0;

        // Oldest first, one row per discovery: an anime discovered weeks ago
        // and still unresolved should not be starved by a fresh discovery
        // landing right behind it. Grouping by RelatedAnimeId below means two
        // discoveries naming the same anime cost one MAL call, and taking
        // maxAnime rows bounds this pass to at most maxAnime distinct anime.
        var discoveries = await db.RelationDiscoveries
            .Where(d => d.ProcessedAt == null)
            .OrderBy(d => d.DiscoveredAt)
            .Take(maxAnime)
            .ToListAsync(ct);

        if (discoveries.Count == 0)
            return 0;

        var now = DateTimeOffset.UtcNow;
        var malCalls = 0;

        foreach (var group in discoveries.GroupBy(d => d.RelatedAnimeId))
        {
            var animeId = group.Key;
            var anime = await db.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animeId, ct);

            if (anime is null)
            {
                try
                {
                    await metadataRefresh.RefreshOneAsync(animeId, ct);
                    malCalls++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to resolve newly-related anime {AnimeId}; will retry next pass.", animeId);
                    continue; // leave this group's discoveries unprocessed
                }

                anime = await db.AnimeMetadata.AsNoTracking().FirstOrDefaultAsync(a => a.Id == animeId, ct);
                if (anime is null)
                {
                    logger.LogWarning("Anime {AnimeId} still has no cached record after a successful resolution fetch; will retry next pass.", animeId);
                    continue;
                }
            }

            // Gated at write time (design.md D6): only an anime that has not
            // finished airing is announced. MAL adds missing edges to
            // long-finished anime routinely, and those are a data correction
            // reaching us, not news.
            if (anime.AiringStatus is "not_yet_aired" or "currently_airing")
                await updateRecorder.RecordAsync(anime, AnimeUpdateKinds.Announced, default, now, ct);

            foreach (var discovery in group)
                discovery.ProcessedAt = now;
        }

        await db.SaveChangesAsync(ct);
        return malCalls;
    }
}
