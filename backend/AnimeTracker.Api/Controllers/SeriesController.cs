using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Series;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SeriesController(
    ISeriesService seriesService,
    SeriesListService seriesListService,
    ISeriesBulkBuildTrigger bulkBuildTrigger,
    ISeriesBulkBuildProgressTracker bulkBuildProgress,
    IArtworkSelectionService artworkSelectionService,
    IPictureRefreshService pictureRefreshService) : ControllerBase
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

    [HttpPut("api/series/{seriesId:int}/title")]
    public async Task<IActionResult> SetTitle(int seriesId, [FromBody] SetSeriesTitleRequest request, CancellationToken ct)
    {
        try
        {
            var title = await artworkSelectionService.SetSeriesTitleAsync(seriesId, request.Title, ct);
            return Ok(new { title });
        }
        catch (SeriesIdNotFoundException)
        {
            return NotFound();
        }
        catch (ArtworkSelectionRejectedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("api/series/{seriesId:int}/title")]
    public async Task<IActionResult> ResetTitle(int seriesId, CancellationToken ct)
    {
        try
        {
            var title = await artworkSelectionService.ResetSeriesTitleAsync(seriesId, ct);
            return Ok(new { title });
        }
        catch (SeriesIdNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPut("api/series/{seriesId:int}/picture")]
    public async Task<IActionResult> SetPicture(int seriesId, [FromBody] SetSeriesPictureRequest request, CancellationToken ct)
    {
        try
        {
            var pictureUrl = await artworkSelectionService.SetSeriesPictureAsync(seriesId, request.PictureUrl, ct);
            return Ok(new { pictureUrl });
        }
        catch (SeriesIdNotFoundException)
        {
            return NotFound();
        }
        catch (ArtworkSelectionRejectedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("api/series/{seriesId:int}/picture")]
    public async Task<IActionResult> ResetPicture(int seriesId, CancellationToken ct)
    {
        try
        {
            var pictureUrl = await artworkSelectionService.ResetSeriesPictureAsync(seriesId, ct);
            return Ok(new { pictureUrl });
        }
        catch (SeriesIdNotFoundException)
        {
            return NotFound();
        }
    }

    /// <summary>Bounded pool backfill (design.md D6) — fetches picture sets
    /// for up to <see cref="PictureRefreshService.SeriesPictureFetchBudget"/>
    /// never-fetched, my-list main-line members of the series containing
    /// <paramref name="animeId"/>. Reports how many eligible members remain.</summary>
    [HttpPost("api/series/by-anime/{animeId:int}/pictures/refresh")]
    public async Task<IActionResult> RefreshPictures(int animeId, CancellationToken ct)
    {
        var seriesId = await seriesService.FindSeriesIdAsync(animeId, ct);
        if (seriesId is null)
            return NotFound();

        var remaining = await pictureRefreshService.RefreshSeriesMainLineAsync(
            seriesId.Value, PictureRefreshService.SeriesPictureFetchBudget, ct);
        return Ok(new { remaining });
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

public class SetSeriesTitleRequest
{
    public required string Title { get; set; }
}

public class SetSeriesPictureRequest
{
    public required string PictureUrl { get; set; }
}
