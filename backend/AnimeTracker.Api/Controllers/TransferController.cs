using AnimeTracker.Api.Services.Transfer;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Controllers;

/// <summary>Moves what exists only in this app between my devices, as a
/// file (device-transfer spec). Sits under <c>api/transfer/</c>, not
/// <c>api/export</c>, so 04's import can land beside it (<c>POST
/// api/transfer/import</c>) under the same name as the Settings page's
/// Transfer group (design.md D1).</summary>
[ApiController]
public class TransferController(IExportService exportService, ITransferImportService importService, ITransferImportProgressTracker importProgress) : ControllerBase
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

    /// <summary>Reads the raw body (never model-bound — the file has its own
    /// reading rules, D3), runs design.md D2's refusals, and hands the file
    /// to the background job. Answers <c>202</c> with the status — already
    /// <c>Running</c> — so a single request is enough for the Settings page
    /// to start polling (design.md D1).</summary>
    [HttpPost("api/transfer/import")]
    public async Task<IActionResult> Import(CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body);
        var bytes = System.Text.Encoding.UTF8.GetBytes(await reader.ReadToEndAsync(ct));

        try
        {
            var status = await importService.AcceptAsync(bytes, ct);
            return Accepted(ToDto(status));
        }
        catch (TransferFileRefusedException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
        catch (TransferImportBlockedException ex)
        {
            return Conflict(new { error = ex.Message });
        }
    }

    /// <summary>Progress of the current or most recent import, for the
    /// Settings page's shared job presentation (design.md D1).</summary>
    [HttpGet("api/transfer/import/status")]
    public IActionResult GetImportStatus() => Ok(ToDto(importProgress.Snapshot));

    private static object ToDto(TransferImportStatusSnapshot snapshot) => new
    {
        phase = snapshot.Phase.ToString(),
        done = snapshot.Done,
        total = snapshot.Total,
        deviceName = snapshot.DeviceName,
        exportedAt = snapshot.ExportedAt,
        report = snapshot.Report,
        error = snapshot.Error,
    };
}
