using AnimeTracker.Api.Services.Detail;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AnimeDetailController(IAnimeDetailService detailService) : ControllerBase
{
    /// <summary>Single anime detail page read — picture, title, scores, my
    /// entry, info fields, synopsis/background, and prequel/sequel links.</summary>
    [HttpGet("api/anime/{animeId:int}")]
    public async Task<IActionResult> GetDetail(int animeId, CancellationToken ct)
    {
        try
        {
            var result = await detailService.GetDetailAsync(animeId, ct);
            return Ok(result);
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Backfills media types for this anime's not-yet-cached related
    /// entries (one paced MAL call each, capped) and returns the refreshed
    /// related-anime list — called when the More overlay opens.</summary>
    [HttpPost("api/anime/{animeId:int}/related-anime/refresh")]
    public async Task<IActionResult> RefreshRelatedMediaTypes(int animeId, CancellationToken ct)
    {
        try
        {
            var result = await detailService.RefreshRelatedMediaTypesAsync(animeId, ct);
            return Ok(result);
        }
        catch (AnimeMetadataNotFoundException)
        {
            return NotFound();
        }
    }
}
