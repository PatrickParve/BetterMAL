using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MetadataRefreshController(
    IMetadataRefreshService refreshService,
    IEpisodeScheduleRefreshService airingRefreshService,
    ILogger<MetadataRefreshController> logger) : ControllerBase
{
    /// <summary>On-demand full refresh of one anime's cached metadata and its
    /// airing data — one MAL API call plus one AniList refresh, updates the
    /// cached record and both sync timestamps.</summary>
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
            await airingRefreshService.RefreshOneAsync(animeId, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "On-demand airing refresh failed for anime {AnimeId}.", animeId);
        }

        return NoContent();
    }
}
