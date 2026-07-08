using AnimeTracker.Api.Services.Import;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

[ApiController]
public class ImportController(IImportProgressTracker progress) : ControllerBase
{
    /// <summary>Import progress for the first-run "X / N synced" indicator.</summary>
    [HttpGet("api/import/status")]
    public IActionResult Status()
    {
        var snapshot = progress.Snapshot;
        return Ok(new
        {
            phase = snapshot.Phase.ToString(),
            synced = snapshot.Synced,
            total = snapshot.Total,
        });
    }
}
