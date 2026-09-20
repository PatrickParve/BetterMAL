using AnimeTracker.Api.Services.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class TopAnimeController(ITopAnimeService topAnimeService) : ControllerBase
{
    /// <summary>A Top Anime ranking list (rank, my/MAL score, and whether each
    /// anime is already in my list), read from the cached snapshot only —
    /// never calls MAL. <c>type</c> selects which of MAL's rankings to serve
    /// (see <see cref="TopAnimeRankingType"/>), defaulting to <c>all</c>; an
    /// unrecognised value is rejected rather than silently falling back to
    /// All. Pair with the refresh endpoint below to bring the cache up to
    /// date.</summary>
    [HttpGet("api/top-anime")]
    public async Task<IActionResult> Get([FromQuery] string? type, CancellationToken ct)
    {
        if (!TopAnimeRankingType.TryParse(type, out var rankingType))
            return BadRequest($"Unsupported ranking type '{type}'.");

        var ranking = await topAnimeService.GetRankingAsync(rankingType, ct);
        return Ok(ranking);
    }

    /// <summary>Triggers a background refresh of this ranking list from MAL,
    /// subject to the once-per-local-day rule and per-list single-flight.
    /// Called whenever the client visits a ranking list — never by a schedule
    /// or a user-facing refresh control. Reports whether it fetched, skipped
    /// (already fresh today), or failed. Rejects an unrecognised <c>type</c>
    /// with the same <c>400</c> the <c>GET</c> gives, before any cache read or
    /// MAL request.</summary>
    [HttpPost("api/top-anime/refresh")]
    public async Task<IActionResult> Refresh([FromQuery] string? type, CancellationToken ct)
    {
        if (!TopAnimeRankingType.TryParse(type, out var rankingType))
            return BadRequest($"Unsupported ranking type '{type}'.");

        var result = await topAnimeService.RefreshAsync(rankingType, ct);
        return Ok(result);
    }
}
