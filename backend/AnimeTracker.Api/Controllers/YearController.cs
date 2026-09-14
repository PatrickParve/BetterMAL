using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class YearController(ISeasonBrowseService seasonBrowseService) : ControllerBase
{
    /// <summary>A year's whole combined anime listing — the union of its four
    /// seasons (design D1) — read from the cache only, never calls MAL. A
    /// year holds a few thousand anime at most, so one read serves the page
    /// for as long as it's open; sort and the page's other filters are
    /// applied client-side to what this returns. Pair with the refresh
    /// endpoint below to bring the cache up to date. Parameter handling
    /// mirrors SeasonController.GetPage exactly, range check included: a
    /// year outside what MyAnimeList could list is refused before any cache
    /// read, MAL request or fetch-log write.</summary>
    [HttpGet("api/year/{year:int}")]
    public async Task<IActionResult> GetPage(
        int year,
        [FromQuery] bool hideHentai = false,
        CancellationToken ct = default)
    {
        var range = await seasonBrowseService.GetRequestRangeAsync(ct);
        if (!range.ContainsYear(year))
            return BadRequest(new { error = $"{year} is outside the years MyAnimeList lists ({range.EarliestYear} to {range.LatestYear})." });

        var page = await seasonBrowseService.GetYearPageAsync(year, hideHentai, ct);
        return Ok(page);
    }

    /// <summary>Triggers a background refresh of this year's four seasons
    /// from MAL, sequentially (design D2), each subject to the same
    /// once-per-local-day rule and per-season single-flight as a season-page
    /// visit. Called whenever the client visits a year — never by a schedule
    /// or a user-facing refresh control. There is no year-level bounds
    /// endpoint (design D3): the year ceiling is GET /api/season/bounds's
    /// latestYear, since a year is navigable exactly when any season in it
    /// is — do not add one. A year outside what MyAnimeList could list is
    /// refused before any cache read, MAL request or fetch-log write; the
    /// accepted range comes from GetRequestRangeAsync, not
    /// GET /api/season/bounds.</summary>
    [HttpPost("api/year/{year:int}/refresh")]
    public async Task<IActionResult> Refresh(int year, CancellationToken ct = default)
    {
        var range = await seasonBrowseService.GetRequestRangeAsync(ct);
        if (!range.ContainsYear(year))
            return BadRequest(new { error = $"{year} is outside the years MyAnimeList lists ({range.EarliestYear} to {range.LatestYear})." });

        var result = await seasonBrowseService.RefreshYearAsync(year, ct);
        return Ok(result);
    }
}
