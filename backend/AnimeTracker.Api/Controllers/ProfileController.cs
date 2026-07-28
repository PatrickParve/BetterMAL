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

    /// <summary>"My top anime" recomputed over one media-type scope (or "all")
    /// for the profile page's filter tabs.</summary>
    [HttpGet("api/profile/top-anime")]
    public async Task<IActionResult> GetTopAnime([FromQuery] string? mediaType, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(mediaType))
            return BadRequest(new { error = $"Unknown mediaType: {mediaType}" });

        var section = await profileService.GetTopAnimeSectionAsync(mediaType, ct);
        return Ok(section);
    }

    /// <summary>"Most rewatched" recomputed over one media-type scope (or
    /// "all") for the profile page's filter tabs.</summary>
    [HttpGet("api/profile/rewatched")]
    public async Task<IActionResult> GetRewatched([FromQuery] string? mediaType, CancellationToken ct)
    {
        if (!TopAnimeMediaTypeScope.IsValid(mediaType))
            return BadRequest(new { error = $"Unknown mediaType: {mediaType}" });

        var section = await profileService.GetRewatchedSectionAsync(mediaType, ct);
        return Ok(section);
    }
}
