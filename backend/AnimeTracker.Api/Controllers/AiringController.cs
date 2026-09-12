using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AiringController(
    IAiringScheduleService airingScheduleService,
    IAiringFullRefreshTrigger fullRefreshTrigger,
    AiringFullRefreshProgress fullRefreshProgress) : ControllerBase
{
    /// <summary>My-list anime broadcast slots for one local week, grouped into
    /// seven day-columns. week is any date inside the desired week (defaults
    /// to today); the response always covers the Monday-to-Sunday week that
    /// contains it.</summary>
    [HttpGet("api/airing")]
    public async Task<IActionResult> GetWeek([FromQuery] DateOnly? week, CancellationToken ct)
    {
        var result = await airingScheduleService.GetWeekAsync(week, ct);
        return Ok(result);
    }

    /// <summary>Kicks off the settings page's manual "refresh all airing data"
    /// action: re-fetches AniList data for every my-list anime except finished
    /// shows already fetched at least once. Runs in the background (paced
    /// through AniList's rate limit) — starts once (background-jobs "A job is
    /// started once") and the answer already reports it as running either
    /// way, so the page reads its progress from the combined status read
    /// rather than polling this endpoint.</summary>
    [HttpPost("api/airing/refresh-all")]
    public IActionResult TriggerFullRefresh()
    {
        if (fullRefreshProgress.TryBegin())
            fullRefreshTrigger.Signal();

        return Accepted(JobDto.From(fullRefreshProgress.Snapshot));
    }
}
