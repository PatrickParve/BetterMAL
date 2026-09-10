using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Artwork;

public class PictureRefreshService(
    AnimeTrackerDbContext db,
    IMalClient malClient,
    RefreshGate refreshGate,
    ILogger<PictureRefreshService> logger) : IPictureRefreshService
{
    // Ties to SeriesGraphBuilder.VisitFetchBudget: the same "bounded, obviously
    // terminating amount of work on a page visit" reasoning applies to backfilling
    // the series picker's pool (design.md D6).
    public const int SeriesPictureFetchBudget = 8;

    public async Task<bool> RefreshOneAsync(int animeId, bool evenIfFetched = false, CancellationToken ct = default)
    {
        if (!await IsEligibleAsync(animeId, evenIfFetched, ct))
            return false;

        // Single-flighted on the same key the detail page's own full-detail
        // fetch uses, so a double-mount of the detail page can't double-fetch.
        using (await refreshGate.LockAsync($"anime:{animeId}", ct))
        {
            if (!await IsEligibleAsync(animeId, evenIfFetched, ct))
                return false;

            return await FetchAndApplyAsync(animeId, ct);
        }
    }

    public async Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default)
    {
        var eligibleAnimeIds = await db.SeriesMembers
            .Where(m => m.SeriesId == seriesId && m.IsMainLine)
            .OrderBy(m => m.Order)
            .Where(m => m.Anime.UserEntry != null && m.Anime.PicturesSyncedAt == null)
            .Select(m => m.AnimeId)
            .ToListAsync(ct);

        var toFetch = eligibleAnimeIds.Take(budget).ToList();
        foreach (var animeId in toFetch)
        {
            ct.ThrowIfCancellationRequested();
            using (await refreshGate.LockAsync($"anime:{animeId}", ct))
            {
                if (await IsEligibleAsync(animeId, evenIfFetched: false, ct))
                    await FetchAndApplyAsync(animeId, ct);
            }
        }

        return eligibleAnimeIds.Count - toFetch.Count;
    }

    private Task<bool> IsEligibleAsync(int animeId, bool evenIfFetched, CancellationToken ct) =>
        db.AnimeMetadata.AnyAsync(a => a.Id == animeId && a.UserEntry != null && (evenIfFetched || a.PicturesSyncedAt == null), ct);

    private async Task<bool> FetchAndApplyAsync(int animeId, CancellationToken ct)
    {
        try
        {
            var node = await malClient.GetAnimeDetailsAsync(animeId, fields: ["pictures"], ct: ct);
            var anime = await db.AnimeMetadata.FirstAsync(a => a.Id == animeId, ct);
            node.ApplyPictureSetTo(anime, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception ex)
        {
            // PicturesSyncedAt is untouched by a failed fetch, so the next
            // visit's eligibility check retries it.
            logger.LogWarning(ex, "Failed to backfill pictures for anime {AnimeId}; will retry next visit.", animeId);
            return false;
        }
    }
}
