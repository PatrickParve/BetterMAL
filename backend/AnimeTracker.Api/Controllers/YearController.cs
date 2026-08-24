using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class YearController(ISeasonBrowseService seasonBrowseService) : ControllerBase
{
    /// <summary>One page of a year's combined anime listing — the union of
    /// its four seasons (design D1) — read from the cache only, never calls
    /// MAL. Pair with the refresh endpoint below to bring the cache up to
    /// date. Parameter handling mirrors SeasonController.GetPage exactly.</summary>
    [HttpGet("api/year/{year:int}")]
    public async Task<IActionResult> GetPage(
        int year,
        [FromQuery] string sort = "popularity",
        [FromQuery] bool includeMyList = true,
        [FromQuery] bool hideHentai = false,
        [FromQuery] string? type = null,
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 24,
        CancellationToken ct = default)
    {
        var types = string.IsNullOrWhiteSpace(type) ? null : type.Split(',', StringSplitOptions.RemoveEmptyEntries);
        var page = await seasonBrowseService.GetYearPageAsync(year, sort, includeMyList, hideHentai, types, offset, Math.Clamp(limit, 1, 100), ct);
        return Ok(page);
    }

    /// <summary>Triggers a background refresh of this year's four seasons
    /// from MAL, sequentially (design D2), each subject to the same
    /// once-per-local-day rule and per-season single-flight as a season-page
    /// visit. Called whenever the client visits a year — never by a schedule
    /// or a user-facing refresh control. There is no year-level bounds
    /// endpoint (design D3): the year ceiling is GET /api/season/bounds's
    /// latestYear, since a year is navigable exactly when any season in it
    /// is — do not add one.</summary>
    [HttpPost("api/year/{year:int}/refresh")]
    public async Task<IActionResult> Refresh(int year, CancellationToken ct = default)
    {
        var result = await seasonBrowseService.RefreshYearAsync(year, ct);
        return Ok(result);
    }
}
