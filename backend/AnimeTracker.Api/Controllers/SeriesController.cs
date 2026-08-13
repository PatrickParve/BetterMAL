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

    /// <summary>Sets the explicit tie-break order for entries tied at my
    /// highest score (design.md decision 11): the given ids receive ranks
    /// 0..n-1 in the order given, and every other member of the series has
    /// its rank cleared.</summary>
    [HttpPut("api/series/{seriesId:int}/favourite-order")]
    public async Task<IActionResult> SetFavouriteOrder(int seriesId, [FromBody] SeriesFavouriteOrderRequest request, CancellationToken ct)
    {
        try
        {
            await seriesService.SetFavouriteOrderAsync(seriesId, request.AnimeIds, ct);
            return NoContent();
        }
        catch (SeriesIdNotFoundException)
        {
            return NotFound();
        }
        catch (UnknownSeriesMemberIdsException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class SeriesFavouriteOrderRequest
{
    public List<int> AnimeIds { get; set; } = [];
}
