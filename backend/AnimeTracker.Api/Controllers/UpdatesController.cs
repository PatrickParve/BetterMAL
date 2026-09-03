using AnimeTracker.Api.Services.Updates;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class UpdatesController(IAnimeUpdateService updateService) : ControllerBase
{
    /// <summary>The last-30-days eligible updates, newest first — backs the
    /// navbar menu's window. Eligibility is identical to <see cref="GetHistory"/>'s.</summary>
    [HttpGet("api/updates/recent")]
    public async Task<IActionResult> GetRecent(CancellationToken ct)
    {
        var recent = await updateService.GetRecentAsync(ct);
        return Ok(recent);
    }

    /// <summary>The whole eligible updates log, newest first — backs the
    /// Updates history overlay (design.md D12).</summary>
    [HttpGet("api/updates/history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        var history = await updateService.GetHistoryAsync(ct);
        return Ok(history);
    }
}
