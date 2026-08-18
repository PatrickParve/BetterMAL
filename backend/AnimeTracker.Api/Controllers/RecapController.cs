using AnimeTracker.Api.Services.Recap;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class RecapController(IRecapService recapService, IRecapAvailabilityService availabilityService) : ControllerBase
{
    private static readonly HashSet<string> ValidSeasons = ["winter", "spring", "summer", "fall"];

    /// <summary>One recap: stats, hot takes, the full included set, and the
    /// season/year rankings, for the period+filter given. `mode` selects
    /// which of `from`/`to` (multi-year), `year` (yearly), or `year`+`season`
    /// (season) are required; `filter` is honoured for multi-year/yearly and
    /// ignored for season (see `RecapDto.Filter`, which always echoes what
    /// was actually used).</summary>
    [HttpGet("api/recap")]
    public async Task<IActionResult> Get(
        [FromQuery] string? mode,
        [FromQuery] int? from,
        [FromQuery] int? to,
        [FromQuery] int? year,
        [FromQuery] string? season,
        [FromQuery] string filter = RecapTimeFilter.Watched,
        CancellationToken ct = default)
    {
        if (!RecapMode.IsSupported(mode))
            return BadRequest(new { error = $"Unknown recap mode '{mode}'." });
        if (!RecapTimeFilter.IsSupported(filter))
            return BadRequest(new { error = $"Unknown time filter '{filter}'." });

        RecapPeriod period;
        switch (mode)
        {
            case RecapMode.MultiYear:
                if (from is not { } fromYear || to is not { } toYear)
                    return BadRequest(new { error = "Multi-year recap requires 'from' and 'to'." });
                period = RecapPeriod.MultiYear(fromYear, toYear);
                break;

            case RecapMode.Yearly:
                if (year is not { } yearlyYear)
                    return BadRequest(new { error = "Yearly recap requires 'year'." });
                period = RecapPeriod.Yearly(yearlyYear);
                break;

            default: // RecapMode.Season — validated as supported above
                if (year is not { } seasonYear)
                    return BadRequest(new { error = "Season recap requires 'year'." });
                if (season is null || !ValidSeasons.Contains(season))
                    return BadRequest(new { error = $"Unknown season '{season}'." });
                period = RecapPeriod.OfSeason(seasonYear, season);
                break;
        }

        var recap = await recapService.GetRecapAsync(period, filter, ct);
        return Ok(recap);
    }

    /// <summary>Whole-list per-year/per-season entry counts backing the
    /// recap picker (design.md decision 2) — loaded once when it opens.</summary>
    [HttpGet("api/recap/availability")]
    public async Task<IActionResult> GetAvailability(CancellationToken ct)
    {
        var availability = await availabilityService.GetAvailabilityAsync(ct);
        return Ok(availability);
    }
}
