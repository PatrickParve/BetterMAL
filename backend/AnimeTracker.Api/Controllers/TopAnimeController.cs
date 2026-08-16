using AnimeTracker.Api.Services.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class TopAnimeController(ITopAnimeService topAnimeService) : ControllerBase
{
    /// <summary>A Top Anime ranking list (rank, my/MAL score, and whether each
    /// anime is already in my list), served from the cached snapshot after a
    /// visit-triggered daily refresh. <c>type</c> selects which of MAL's
    /// rankings to serve (see <see cref="TopAnimeRankingType"/>), defaulting
    /// to <c>all</c>; an unrecognised value is rejected rather than silently
    /// falling back to All.</summary>
    [HttpGet("api/top-anime")]
    public async Task<IActionResult> Get([FromQuery] string? type, CancellationToken ct)
    {
        if (!TopAnimeRankingType.TryParse(type, out var rankingType))
            return BadRequest($"Unsupported ranking type '{type}'.");

        var ranking = await topAnimeService.GetRankingAsync(rankingType, ct);
        return Ok(ranking);
    }
}
