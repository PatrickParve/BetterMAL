using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MetadataRefreshController(
    IMetadataRefreshService refreshService,
    IEpisodeScheduleRefreshService airingRefreshService,
    ITmdbArtworkService tmdbArtwork,
    ILogger<MetadataRefreshController> logger) : ControllerBase
{
    /// <summary>On-demand full refresh of one anime's cached metadata, its
    /// airing data and its TMDB pictures — one MAL API call, one AniList
    /// refresh, and a forced refetch of the TMDB sets the anime draws from
    /// (a no-op for an anime not in my list, one with no TMDB mapping, or an
    /// install with no key). Updates the cached record and both sync
    /// timestamps. Only the MAL step can fail the action: the other two are
    /// isolated from it and from each other.</summary>
    [HttpPost("api/anime/{animeId:int}/refresh")]
    public async Task<IActionResult> RefreshOne(int animeId, CancellationToken ct)
    {
        try
        {
            await refreshService.RefreshOneAsync(animeId, ct);
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }

        // The MAL refresh already succeeded at this point; an AniList failure
        // here is logged, not surfaced — the caller still gets updated
        // metadata, and previously stored airing rows are left intact.
        try
        {
            // By hand, so an anime AniList was recorded as not knowing is looked
            // up again whatever the age of that record.
            await airingRefreshService.RefreshOneAsync(animeId, relookupAbsent: true, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-demand airing refresh failed for anime {AnimeId}.", animeId);
        }

        // The TMDB refetch belongs to this action only — not to
        // MetadataRefreshService.RefreshOneAsync, which the first visit to a
        // lean row, relation resolution and announcements also call, and
        // which must never put TMDB calls on a background path (design.md
        // D15, spec metadata-refresh "Scheduled refreshes never call TMDB").
        // Isolated like the AniList step: the sets a failed fetch would have
        // replaced are left as they were, and the action still succeeds.
        try
        {
            await tmdbArtwork.RefreshAnimeAsync(animeId, force: true, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-demand TMDB refresh failed for anime {AnimeId}.", animeId);
        }

        return NoContent();
    }
}
