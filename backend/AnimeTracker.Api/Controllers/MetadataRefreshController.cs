using AnimeTracker.Api.Services.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class MetadataRefreshController(IMetadataRefreshService refreshService) : ControllerBase
{
    /// <summary>On-demand full refresh of one anime's cached metadata — one MAL
    /// API call, updates the cached record and both sync timestamps.</summary>
    [HttpPost("api/anime/{animeId:int}/refresh")]
    public async Task<IActionResult> RefreshOne(int animeId, CancellationToken ct)
    {
        try
        {
            await refreshService.RefreshOneAsync(animeId, ct);
            return NoContent();
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
    }
}
