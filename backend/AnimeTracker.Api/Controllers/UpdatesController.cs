using AnimeTracker.Api.Services.Updates;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

public record MarkUpdatesSeenRequest(IReadOnlyList<long>? Ids);

[ApiController]
public class UpdatesController(IAnimeUpdateService updateService) : ControllerBase
{
    // The client only ever reports cards that were actually on screen or
    // hovered within one batching window, so a real request never comes
    // close to this (store-seen-updates-on-server design.md D4).
    private const int MaxSeenBatch = 500;

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

    /// <summary>Marks a batch of update ids seen (store-seen-updates-on-server
    /// design.md D4). Idempotent, and ids with no row are ignored.</summary>
    [HttpPost("api/updates/seen")]
    public async Task<IActionResult> MarkSeen([FromBody] MarkUpdatesSeenRequest? request, CancellationToken ct)
    {
        if (request?.Ids is null || request.Ids.Count > MaxSeenBatch)
            return BadRequest(new { error = "Ids is required and must not exceed " + MaxSeenBatch + " ids." });

        await updateService.MarkSeenAsync(request.Ids, ct);
        return NoContent();
    }
}
