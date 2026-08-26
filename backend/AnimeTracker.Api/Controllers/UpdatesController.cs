using AnimeTracker.Api.Services.Updates;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class UpdatesController(IAnimeUpdateService updateService) : ControllerBase
{
    /// <summary>The whole eligible updates log, newest first — backs the Home
    /// section's history overlay (design.md D12).</summary>
    [HttpGet("api/updates/history")]
    public async Task<IActionResult> GetHistory(CancellationToken ct)
    {
        var history = await updateService.GetHistoryAsync(ct);
        return Ok(history);
    }
}
