using AnimeTracker.Api.Services.Airing;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AiringController(
    IAiringScheduleService airingScheduleService,
    IAiringFullRefreshTrigger fullRefreshTrigger,
    IAiringFullRefreshProgressTracker fullRefreshProgress) : ControllerBase
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
    /// through AniList's rate limit) — poll the status endpoint below rather
    /// than waiting on this call.</summary>
    [HttpPost("api/airing/refresh-all")]
    public IActionResult TriggerFullRefresh()
    {
        fullRefreshTrigger.Signal();
        return Accepted(ToDto(fullRefreshProgress.Snapshot));
    }

    /// <summary>Progress of the manual "refresh all airing data" action, for
    /// the settings page's progress indicator.</summary>
    [HttpGet("api/airing/refresh-all/status")]
    public IActionResult GetFullRefreshStatus() => Ok(ToDto(fullRefreshProgress.Snapshot));

    private static object ToDto(AiringFullRefreshStatusSnapshot snapshot) => new
    {
        phase = snapshot.Phase.ToString(),
        synced = snapshot.Synced,
        total = snapshot.Total,
    };
}
