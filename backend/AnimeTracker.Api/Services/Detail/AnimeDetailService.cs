using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Metadata;

namespace AnimeTracker.Api.Services.Detail;

public class AnimeDetailService(
    IAnimeMetadataRepository metadataRepository,
    IMetadataRefreshService refreshService,
    ILogger<AnimeDetailService> logger) : IAnimeDetailService
{
    public async Task<AnimeDetailDto> GetDetailAsync(int animeId, CancellationToken ct = default)
    {
        var anime = await metadataRepository.GetByIdAsync(animeId, ct)
            ?? throw new AnimeMetadataNotFoundException(animeId);

        // LastSyncedAt is only ever set by a rich/full-detail upsert (see
        // MalMappingExtensions.ApplyTo vs ApplyLeanTo) — default means this row
        // has only ever been lean-fetched via Season/Top-Anime browsing, so the
        // detail-only fields (genres, synopsis, studio, aired dates, ...) are
        // still empty. Mirrors SeasonBrowseService's visit-triggered live fetch.
        if (anime.LastSyncedAt == default)
        {
            try
            {
                await refreshService.RefreshOneAsync(animeId, ct);
                anime = await metadataRepository.GetByIdAsync(animeId, ct) ?? anime;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to live-fetch full detail for anime {AnimeId} on first visit; serving lean data.", animeId);
            }
        }

        return AnimeDetailDto.FromEntity(anime);
    }
}
