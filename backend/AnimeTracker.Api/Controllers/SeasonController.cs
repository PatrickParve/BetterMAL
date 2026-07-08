using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class SeasonController(ISeasonBrowseService seasonBrowseService) : ControllerBase
{
    private static readonly HashSet<string> ValidSeasons = ["winter", "spring", "summer", "fall"];

    /// <summary>One page of a season's full anime listing (not just my list),
    /// live-fetched and cached as needed before reading from Postgres.</summary>
    [HttpGet("api/season/{year:int}/{season}")]
    public async Task<IActionResult> GetPage(
        int year,
        string season,
        [FromQuery] string sort = "popularity",
        [FromQuery] int offset = 0,
        [FromQuery] int limit = 24,
        CancellationToken ct = default)
    {
        if (!ValidSeasons.Contains(season))
            return BadRequest(new { error = $"Unknown season '{season}'." });

        var page = await seasonBrowseService.GetPageAsync(year, season, sort, offset, Math.Clamp(limit, 1, 100), ct);
        return Ok(page);
    }
}
