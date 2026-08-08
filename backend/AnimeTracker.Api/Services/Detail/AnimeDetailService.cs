using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Metadata;

namespace AnimeTracker.Api.Services.Detail;

public class AnimeDetailService(
    IAnimeMetadataRepository metadataRepository,
    IMetadataRefreshService refreshService,
    IEpisodeScheduleService scheduleService,
    ILogger<AnimeDetailService> logger) : IAnimeDetailService
{
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
        // After the fetch Genres is set, so later visits are plain cache hits.
        // Mirrors SeasonBrowseService's visit-triggered live fetch.
        if (anime is null || anime.Genres is not { Count: > 0 })
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

        var episodesAired = await scheduleService.EpisodesAiredAsOfAsync(anime, DateTimeOffset.UtcNow, ct);
        return AnimeDetailDto.FromEntity(anime, episodesAired);
    }
}
