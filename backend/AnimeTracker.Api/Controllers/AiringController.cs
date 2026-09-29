using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AiringController(
    IAiringScheduleService airingScheduleService,
    IAiringFullRefreshTrigger fullRefreshTrigger,
    AiringFullRefreshProgress fullRefreshProgress,
    IBroadcastLocalTimeConverter localTimeConverter) : ControllerBase
{
    /// <summary>My-list anime broadcast slots for one local week, grouped into
    /// seven day-columns. week is any date inside the desired week (defaults
    /// to today); the response always covers the Monday-to-Sunday week that
    /// contains it. A present week outside 1 January EarliestArchiveYear
    /// through 31 December of next year (<see cref="AiringWeekRange"/>) is
    /// refused with a 400 before the schedule is read; an absent week is
    /// never checked.</summary>
    [HttpGet("api/airing")]
    public async Task<IActionResult> GetWeek([FromQuery] DateOnly? week, CancellationToken ct)
    {
        if (week is { } requestedWeek)
        {
            var today = localTimeConverter.GetLocalDate(DateTimeOffset.UtcNow);
            if (!AiringWeekRange.Contains(requestedWeek, today))
                return BadRequest(new { error = $"{requestedWeek:yyyy-MM-dd} is outside the airing schedule's range ({AiringWeekRange.Earliest:yyyy-MM-dd} to {AiringWeekRange.Latest(today):yyyy-MM-dd})." });
        }

        var result = await airingScheduleService.GetWeekAsync(week, ct);
        return Ok(result);
    }

    /// <summary>Kicks off the settings page's manual "Refresh all airing dates"
    /// action: re-fetches AniList data for every my-list anime except those
    /// whose airing history is complete. With <c>?force=true</c> ("Force all
    /// airing dates") it re-fetches every my-list anime. Both are one job, on
    /// one tracker: runs in the background (paced through AniList's rate limit),
    /// starts once (background-jobs "A job is started once") and the answer
    /// already reports it as running either way, so a second press of either
    /// while it runs gets the running job back. The page reads its progress from
    /// the combined status read rather than polling this endpoint.</summary>
    [HttpPost("api/airing/refresh-all")]
    public IActionResult TriggerFullRefresh([FromQuery] bool force = false)
    {
        if (fullRefreshProgress.TryBegin())
            fullRefreshTrigger.Signal(force);

        return Accepted(JobDto.From(fullRefreshProgress.Snapshot));
    }
}
