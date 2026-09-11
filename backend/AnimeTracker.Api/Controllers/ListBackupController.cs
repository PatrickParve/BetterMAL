using AnimeTracker.Api.Services.ListBackup;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>Serves the list backup (list-backup spec, design.md D1): a
/// read-only copy of my list, produced only on request. Sits under
/// <c>api/my-list</c>, the same data <c>MyListController</c> serves for the
/// My List page, but in its own controller, as the device-transfer export
/// sits apart from the import.</summary>
[ApiController]
public class ListBackupController(IListBackupService listBackupService) : ControllerBase
{
    /// <summary>GET, not POST: the request has no side effect and can be
    /// repeated freely (design.md D1).</summary>
    [HttpGet("api/my-list/backup")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var (bytes, fileName) = await listBackupService.ExportAsync(Request.Headers.UserAgent, ct);
        return File(bytes, "application/json", fileName);
    }
}
