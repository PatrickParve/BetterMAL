using AnimeTracker.Api.Services.Profile;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class ProfileController(IProfileService profileService) : ControllerBase
{
    /// <summary>Profile page data: anime stats, recent activity, my top anime
    /// (with tie-break candidates), score distribution, and opinion-divergence
    /// lists — all computed from cached Postgres data.</summary>
    [HttpGet("api/profile")]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var profile = await profileService.GetProfileAsync(ct);
        return Ok(profile);
    }

    /// <summary>Full edit history, most recent first — backs the "Latest
    /// updates" box's history overlay.</summary>
    [HttpGet("api/profile/activity")]
    public async Task<IActionResult> GetActivityHistory(CancellationToken ct)
    {
        var history = await profileService.GetActivityHistoryAsync(ct);
        return Ok(history);
    }
}
