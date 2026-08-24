using AnimeTracker.Api.Services.Series;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SeriesController(
    ISeriesService seriesService,
    SeriesListService seriesListService,
    ISeriesBulkBuildTrigger bulkBuildTrigger,
    ISeriesBulkBuildProgressTracker bulkBuildProgress) : ControllerBase
{
    /// <summary>The Series page's whole-list read (add-series-browser
    /// design.md D1). Builds nothing, refreshes nothing, and makes no MAL
    /// call — every figure is computed at read time from stored members, so
    /// its cost is bounded by what is stored no matter how incomplete the
    /// store is.</summary>
    [HttpGet("api/series/list")]
    public async Task<IActionResult> GetList(CancellationToken ct) => Ok(await seriesListService.GetSeriesListAsync(ct));

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

    /// <summary>Kicks off the settings page's manual "build all series from my
    /// list" action: builds a series for every my-list anime that belongs to
    /// no stored series yet, or whose stored series predates the current
    /// classification rules. Runs in the background (through the same fetch
    /// budget and single-flight gate as every other build) — poll the status
    /// endpoint below rather than waiting on this call. Marks the run pending
    /// before returning, so the response already reports a run in flight
    /// rather than whatever the previous run left (design.md decision
    /// 5).</summary>
    [HttpPost("api/series/build-all")]
    public IActionResult TriggerBulkBuild()
    {
        bulkBuildProgress.MarkPending();
        bulkBuildTrigger.Signal();
        return Accepted(ToDto(bulkBuildProgress.Snapshot));
    }

    /// <summary>Progress of the manual "build all series from my list" action,
    /// for the settings page's progress indicator.</summary>
    [HttpGet("api/series/build-all/status")]
    public IActionResult GetBulkBuildStatus() => Ok(ToDto(bulkBuildProgress.Snapshot));

    private static object ToDto(SeriesBulkBuildStatusSnapshot snapshot) => new
    {
        phase = snapshot.Phase.ToString(),
        built = snapshot.Built,
        total = snapshot.Total,
    };
}

public class SeriesFavouriteOrderRequest
{
    public List<int> AnimeIds { get; set; } = [];
}
