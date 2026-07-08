using AnimeTracker.Api.Services.Airing;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class AiringController(IAiringScheduleService airingScheduleService) : ControllerBase
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
}
