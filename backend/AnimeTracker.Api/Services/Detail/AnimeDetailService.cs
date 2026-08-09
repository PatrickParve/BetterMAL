using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Detail;

public class AnimeDetailService(
    IAnimeMetadataRepository metadataRepository,
    IMetadataRefreshService refreshService,
    IEpisodeScheduleService scheduleService,
    AnimeTrackerDbContext db,
    ILogger<AnimeDetailService> logger) : IAnimeDetailService
{
    // Migration cutoff for AddAnimeRelatedAnime: rows last synced before this
    // predate related-anime storage entirely, so the live-fetch trigger below
    // treats them as detail-incomplete even when Genres is already populated.
    private static readonly DateTimeOffset RelatedAnimeMigrationCutoff = new(2026, 8, 8, 0, 0, 0, TimeSpan.Zero);

    // Caps how many uncached related anime a single More-overlay open will
    // fetch from MAL (one paced call each, ~1/sec) — bounds worst-case
    // request latency for a large franchise instead of fetching all of them.
    private const int MaxRelatedBackfillCount = 20;

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
        // After the fetch Genres and RelatedAnime are set, so later visits are
        // plain cache hits. Mirrors SeasonBrowseService's visit-triggered live fetch.
        if (anime is null
            || anime.Genres is not { Count: > 0 }
            || (anime.RelatedAnime.Count == 0 && anime.LastSyncedAt < RelatedAnimeMigrationCutoff))
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

        if (anime is null)
            throw new AnimeMetadataNotFoundException(animeId);

        var now = DateTimeOffset.UtcNow;
        var episodesAired = await scheduleService.EpisodesAiredAsOfAsync(anime, now, ct);
        var nextEpisode = ToEta(await scheduleService.NextAiringInstantAsync(anime, now, ct), now);
        var aniListId = await db.AnimeAiringSyncs.AsNoTracking()
            .Where(s => s.AnimeId == animeId)
            .Select(s => s.AniListId)
            .FirstOrDefaultAsync(ct);

        // MAL's related_anime field never reports the related anime's media
        // type, so it's opportunistically looked up from our own cache —
        // present only for related anime we've already fetched ourselves.
        var relatedMediaTypeByAnimeId = await GetMediaTypesAsync(anime.RelatedAnime.Select(r => r.RelatedAnimeId), ct);

        return AnimeDetailDto.FromEntity(anime, episodesAired, nextEpisode, aniListId, relatedMediaTypeByAnimeId);
    }

    /// <summary>Fetches full detail for up to <see cref="MaxRelatedBackfillCount"/>
    /// of this anime's related-anime entries we don't have cached yet — so the
    /// More overlay's "Unknown" media types can resolve on demand instead of
    /// only ever self-healing when each related anime happens to get visited
    /// or browsed elsewhere. One paced MAL call per uncached entry.</summary>
    public async Task<List<RelatedAnimeDto>> RefreshRelatedMediaTypesAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await metadataRepository.GetByIdAsync(animeId, ct);
        if (anime is null)
            throw new AnimeMetadataNotFoundException(animeId);

        var relatedIds = anime.RelatedAnime.Select(r => r.RelatedAnimeId).Distinct().ToList();
        var cachedIds = await db.AnimeMetadata.AsNoTracking()
            .Where(m => relatedIds.Contains(m.Id))
            .Select(m => m.Id)
            .ToListAsync(ct);
        var uncachedIds = relatedIds.Except(cachedIds).Take(MaxRelatedBackfillCount).ToList();

        foreach (var relatedId in uncachedIds)
        {
            try
            {
                await refreshService.RefreshOneAsync(relatedId, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to backfill related anime {RelatedAnimeId} for {AnimeId}'s More overlay.", relatedId, animeId);
            }
        }

        var mediaTypeByAnimeId = await GetMediaTypesAsync(relatedIds, ct);
        return anime.RelatedAnime.Select(r => RelatedAnimeDto.FromEntity(r, mediaTypeByAnimeId)).ToList();
    }

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
