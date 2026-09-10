using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>Moves what exists only in this app between my devices, as a
/// file (device-transfer spec). Sits under <c>api/transfer/</c>, not
/// <c>api/export</c>, so 04's import can land beside it (<c>POST
/// api/transfer/import</c>) under the same name as the Settings page's
/// Transfer group (design.md D1).</summary>
[ApiController]
public class TransferController(IExportService exportService) : ControllerBase
{
    /// <summary>GET, not POST: the request has no side effect and can be
    /// repeated freely (design.md D1). ASP.NET writes the
    /// <c>Content-Disposition: attachment; filename=…</c> header from the
    /// file name passed to <see cref="ControllerBase.File(byte[], string, string)"/>.</summary>
    [HttpGet("api/transfer/export")]
    public async Task<IActionResult> Export(CancellationToken ct)
    {
        var (bytes, fileName) = await exportService.ExportAsync(Request.Headers.UserAgent, ct);
        return File(bytes, "application/json", fileName);
    }
}
