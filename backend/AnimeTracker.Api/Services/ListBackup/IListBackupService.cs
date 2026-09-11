namespace AnimeTracker.Api.Services.ListBackup;

/// <summary>Builds the list backup: a read-only, on-request copy of my list
/// (list-backup spec, design.md D1). Never imported by this app.</summary>
public interface IListBackupService
{
    /// <summary><paramref name="userAgent"/> is the backup request's own
    /// <c>User-Agent</c> header, used only to derive the device name, as the
    /// device-transfer export does. Returns the file's serialized UTF-8
    /// bytes together with its download file name.</summary>
    Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default);
}
