using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Detail;

public class AnimeDetailService(
    IAnimeMetadataRepository metadataRepository,
    IMetadataRefreshService refreshService,
    IEpisodeScheduleService scheduleService,
    AnimeTrackerDbContext db,
    RefreshGate refreshGate,
    ILogger<AnimeDetailService> logger) : IAnimeDetailService
{
    // Migration cutoff for AddAnimeRelatedAnime: rows last synced before this
    // predate related-anime storage entirely, so the live-fetch trigger below
    // treats them as detail-incomplete even when Genres is already populated.
    private static readonly DateTimeOffset RelatedAnimeMigrationCutoff = new(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);

    // Migration cutoff for AddRelatedAnimeMediaType: relation rows written
    // before this predate the MediaType column (and the related_anime{node{
    // media_type}} field selection that populates it), so they're stuck
    // showing "Unknown" in the More overlay for any related anime we haven't
    // separately cached — which MAL's own relation data would have resolved
    // directly, for free, had this anime been fetched after the column existed.
    private static readonly DateTimeOffset RelatedAnimeMediaTypeMigrationCutoff = new(2026, 8, 9, 0, 0, 0, TimeSpan.Zero);

    public async Task<AnimeDetailDto> GetDetailAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await metadataRepository.GetByIdAsync(animeId, ct);

        // Live-fetch full detail from MAL (RefreshOneAsync upserts) whenever the
        // row isn't detail-complete. Genres is the marker: a full-detail fetch
        // always populates it, and every row that lacks it never had one —
        //  - missing row: a sequel/prequel link or an un-interacted search
        //    result opened for the first time (a 404 before this fix);
        //  - lean row: only ever browsed via Season/Top-Anime;
        //  - reconciliation-added row: built from the fields-limited my-list
        //    payload, which omits genres/synopsis/background/related — so it can
        //    carry a LastSyncedAt yet still be missing every detail-only field.
        // A row with no related-anime entries and a LastSyncedAt predating the
        // related-anime migration also refetches, so relations repopulate on
        // the first visit after deploy even for rows that already had genres.
        // Same idea for a row whose relations exist but predate the
        // MediaType column: one more refetch backfills every relation's media
        // type directly from MAL, rather than leaving it to the (weaker)
        // per-related-anime cache-lookup fallback.
        // After the fetch Genres and RelatedAnime are set, so later visits are
        // plain cache hits. Single-flight via RefreshGate: a waiter re-checks
        // this same predicate after acquiring the gate, so it sees the
        // winner's fetch and skips a second MAL fetch instead of racing it.
        if (NeedsFullDetailFetch(anime))
        {
            using (await refreshGate.LockAsync($"anime:{animeId}", ct))
            {
                anime = await metadataRepository.GetByIdAsync(animeId, ct);
                if (NeedsFullDetailFetch(anime))
                {
                    try
                    {
                        await refreshService.RefreshOneAsync(animeId, ct);
                        anime = await metadataRepository.GetByIdAsync(animeId, ct) ?? anime;
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to live-fetch full detail for anime {AnimeId}; serving cached data if any.", animeId);
                    }
                }
            }
        }

        if (anime is null)
            throw new AnimeMetadataNotFoundException(animeId);

        var now = DateTimeOffset.UtcNow;
        var episodesAired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        var nextEpisode = ToEta(await scheduleService.NextAiringInstantAsync(anime, now, ct), now);
        var aniListId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.AnimeId == animeId)
            .Select(s => s.AniListId)
            .FirstOrDefaultAsync(ct);

        // Full-detail fetches store each relation's media type directly
        // (AnimeRelatedAnime.MediaType, from related_anime{node{media_type}});
        // this cache lookup is only a fallback for relation rows written
        // before that column existed.
        var relatedMediaTypeByAnimeId = await GetMediaTypesAsync(anime.RelatedAnime.Select(r => r.RelatedAnimeId), ct);

        return AnimeDetailDto.FromEntity(anime, episodesAired, nextEpisode, aniListId, relatedMediaTypeByAnimeId);
    }

    private static bool NeedsFullDetailFetch(AnimeMetadata? anime) =>
        anime is null
        || anime.Genres is not { Count: > 0 }
        || (anime.RelatedAnime.Count == 0 && anime.LastSyncedAt < RelatedAnimeMigrationCutoff)
        || (anime.RelatedAnime.Count > 0
            && anime.LastSyncedAt < RelatedAnimeMediaTypeMigrationCutoff
            && anime.RelatedAnime.Any(r => r.MediaType is null));

    private async Task<Dictionary<int, string?>> GetMediaTypesAsync(IEnumerable<int> animeIds, CancellationToken ct)
    {
        var ids = animeIds.Distinct().ToList();
        return await db.AnimeMetadata.AsNoTracking()
            .Where(m => ids.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id, m => m.MediaType, ct);
    }

    private static NextEpisodeEtaDto? ToEta(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var remaining = instant - now;
        return new NextEpisodeEtaDto(remaining.Days, remaining.Hours);
    }
}
