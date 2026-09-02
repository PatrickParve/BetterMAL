using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SeasonController(ISeasonBrowseService seasonBrowseService) : ControllerBase
{
    private static readonly HashSet<string> ValidSeasons = ["winter", "spring", "summer", "fall"];

    /// <summary>A season's whole anime listing (not just my list), read from
    /// the cache only — never calls MAL. A season holds a few hundred anime
    /// at most, so one read serves the page for as long as it's open; sort
    /// and the page's other filters are applied client-side to what this
    /// returns. Pair with the refresh endpoint below to bring the cache up to
    /// date.</summary>
    [HttpGet("api/season/{year:int}/{season}")]
    public async Task<IActionResult> GetPage(
        int year,
        string season,
        [FromQuery] bool hideHentai = false,
        CancellationToken ct = default)
    {
        if (!ValidSeasons.Contains(season))
            return BadRequest(new { error = $"Unknown season '{season}'." });

        var page = await seasonBrowseService.GetPageAsync(year, season, hideHentai, ct);
        return Ok(page);
    }

    /// <summary>Triggers a background refresh of this season from MAL,
    /// subject to the once-per-local-day rule and per-season single-flight.
    /// Called whenever the client visits a season — never by a schedule or a
    /// user-facing refresh control. Reports which of four outcomes the
    /// refresh had (fetched anime / MAL has no listing for the season / it
    /// was already fetched today / the fetch failed) so the client can tell
    /// those apart rather than reading a single "did anything change" flag.</summary>
    [HttpPost("api/season/{year:int}/{season}/refresh")]
    public async Task<IActionResult> Refresh(int year, string season, CancellationToken ct = default)
    {
        if (!ValidSeasons.Contains(season))
            return BadRequest(new { error = $"Unknown season '{season}'." });

        var result = await seasonBrowseService.RefreshAsync(year, season, ct);
        return Ok(result);
    }

    /// <summary>The furthest season currently navigable. Doesn't collide with
    /// the page route above — its {year:int} route constraint keeps the
    /// literal segment "bounds" out of that template. Repository-only read,
    /// never calls MAL.</summary>
    [HttpGet("api/season/bounds")]
    public async Task<IActionResult> GetBounds(CancellationToken ct = default)
    {
        var bounds = await seasonBrowseService.GetBoundsAsync(ct);
        return Ok(bounds);
    }
}
