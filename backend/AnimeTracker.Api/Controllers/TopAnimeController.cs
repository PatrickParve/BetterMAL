using AnimeTracker.Api.Services.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class TopAnimeController(ITopAnimeService topAnimeService) : ControllerBase
{
    /// <summary>The global Top Anime ranking (rank, my/MAL score, and whether
    /// each anime is already in my list), served from the cached snapshot
    /// after a visit-triggered daily refresh.</summary>
    [HttpGet("api/top-anime")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var ranking = await topAnimeService.GetRankingAsync(ct);
        return Ok(ranking);
    }
}
