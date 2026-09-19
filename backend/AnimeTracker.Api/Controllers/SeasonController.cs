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
    /// date. A season outside what MyAnimeList could list is refused before
    /// any cache read, MAL request or fetch-log write.</summary>
    [HttpGet("api/season/{year:int}/{season}")]
    public async Task<IActionResult> GetPage(
        int year,
        string season,
        [FromQuery] bool hideHentai = false,
        CancellationToken ct = default)
    {
        if (!ValidSeasons.Contains(season))
            return BadRequest(new { error = $"Unknown season '{season}'." });

        var range = await seasonBrowseService.GetRequestRangeAsync(ct);
        if (!range.ContainsSeason(year, season))
            return BadRequest(new { error = $"{season} {year} is outside the seasons MyAnimeList lists (winter {range.EarliestYear} to {range.LatestSeason} {range.LatestYear})." });

        var page = await seasonBrowseService.GetPageAsync(year, season, hideHentai, ct);
        return Ok(page);
    }

    /// <summary>Triggers a background refresh of this season from MAL,
    /// subject to the once-per-local-day rule and per-season single-flight.
    /// Called whenever the client visits a season — never by a schedule or a
    /// user-facing refresh control. Reports which of four outcomes the
    /// refresh had (fetched anime / MAL has no listing for the season / it
    /// was already fetched today / the fetch failed) so the client can tell
    /// those apart rather than reading a single "did anything change" flag.
    /// A season outside what MyAnimeList could list is refused before any
    /// cache read, MAL request or fetch-log write.</summary>
    [HttpPost("api/season/{year:int}/{season}/refresh")]
    public async Task<IActionResult> Refresh(int year, string season, CancellationToken ct = default)
    {
        if (!ValidSeasons.Contains(season))
            return BadRequest(new { error = $"Unknown season '{season}'." });

        var range = await seasonBrowseService.GetRequestRangeAsync(ct);
        if (!range.ContainsSeason(year, season))
            return BadRequest(new { error = $"{season} {year} is outside the seasons MyAnimeList lists (winter {range.EarliestYear} to {range.LatestSeason} {range.LatestYear})." });

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

    /// <summary>Probes the one season past the navigable ceiling that a page
    /// visit is allowed to reach for (see HorizonProbe), returning the bounds
    /// either way. Takes no year or season: the target is derived from the
    /// ceiling server-side, so reaching past the accepted range stays one
    /// fixed rule rather than something a caller can ask for — the season
    /// endpoints above still refuse that same season by name with a
    /// <c>400</c>. <paramref name="onDemand"/> is for a URL addressing the
    /// probe's own target directly: it keeps the once-per-day gate but skips
    /// the last-month window, so an explicit question is answered rather than
    /// refused.</summary>
    [HttpPost("api/season/horizon/probe")]
    public async Task<IActionResult> ProbeHorizon([FromQuery] bool onDemand = false, CancellationToken ct = default)
    {
        var bounds = await seasonBrowseService.ProbeHorizonAsync(onDemand, ct);
        return Ok(bounds);
    }
}
