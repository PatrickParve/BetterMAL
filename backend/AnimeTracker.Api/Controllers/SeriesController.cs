using AnimeTracker.Api.Services.Series;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SeriesController(ISeriesService seriesService) : ControllerBase
{
    /// <summary>Series page read, resolved from any member's anime id —
    /// builds or refreshes the series first when it's missing, partial, or
    /// stale (more than 30 days old).</summary>
    [HttpGet("api/series/by-anime/{animeId:int}")]
    public async Task<IActionResult> GetByAnime(int animeId, CancellationToken ct)
    {
        try
        {
            return Ok(await seriesService.GetSeriesAsync(animeId, ct));
        }
        catch (SeriesNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Forces recomputation with the larger (20-fetch) budget and
    /// returns the same projection the read endpoint does.</summary>
    [HttpPost("api/series/by-anime/{animeId:int}/rebuild")]
    public async Task<IActionResult> Rebuild(int animeId, CancellationToken ct)
    {
        try
        {
            return Ok(await seriesService.RebuildSeriesAsync(animeId, ct));
        }
        catch (SeriesNotFoundException)
        {
            return NotFound();
        }
    }
}
