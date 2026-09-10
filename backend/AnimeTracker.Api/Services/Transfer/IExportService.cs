namespace AnimeTracker.Api.Services.Transfer;

/// <summary>Builds the device-transfer export file: everything that exists
/// only in this app, and nothing that comes from MyAnimeList or rebuilds
/// itself (device-transfer spec, design.md D1). Read-only — producing an
/// export writes nothing.</summary>
public interface IExportService
{
    /// <summary><paramref name="userAgent"/> is the export request's own
    /// <c>User-Agent</c> header, used only to derive the device name
    /// (design.md D6). Returns the file's serialized UTF-8 bytes together
    /// with its download file name.</summary>
    Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default);
}
