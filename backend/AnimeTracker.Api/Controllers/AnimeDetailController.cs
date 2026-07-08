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
}
