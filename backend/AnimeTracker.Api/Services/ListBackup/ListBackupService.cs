using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Transfer;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.ListBackup;

public class ListBackupService(AnimeTrackerDbContext db) : IListBackupService
{
    public async Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default)
    {
        var deviceId = await db.DeviceIdentities.AsNoTracking()
            .Select(d => d.DeviceId)
            .SingleAsync(ct);

        // One projection is the snapshot (design.md D3): Postgres runs this
        // single statement against one moment, so an edit landing mid-backup
        // appears in every column or none, and an added or removed anime is
        // present or absent as a whole. No explicit transaction is needed,
        // unlike the transfer export's D2 — there is only one data read.
        var rows = await db.UserAnimeEntries.AsNoTracking()
            .OrderBy(e => e.AnimeId)
            .Select(e => new
            {
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.TotalEpisodes,
                e.Status,
                e.EpisodesWatched,
                e.MyScore,
                e.StartedAt,
                e.CompletedAt,
                e.RewatchCount,
            })
            .ToListAsync(ct);

        var exportedAt = DateTime.UtcNow;

        var file = new ListBackupFile(
            Kind: "listBackup",
            FormatVersion: 1,
            Device: new ExportDevice(deviceId, DeviceName.Describe(userAgent)),
            ExportedAt: exportedAt,
            Entries: rows
                .Select(r => new ListBackupEntry(
                    r.AnimeId, r.Title, r.EnglishTitle, r.TotalEpisodes,
                    r.Status, r.EpisodesWatched, r.MyScore, r.StartedAt, r.CompletedAt, r.RewatchCount))
                .ToList());

        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, ExportJson.Options);
        var fileName = BuildFileName(deviceId, exportedAt);

        return (bytes, fileName);
    }

    /// <summary>design.md D7: the <c>list</c> segment keeps a backup from
    /// ever reading as a transfer file; the device prefix and timestamp keep
    /// backups from two devices, and several from one, apart. internal so
    /// file-name tests can call it directly — AnimeTracker.Api.Tests holds
    /// InternalsVisibleTo.</summary>
    internal static string BuildFileName(Guid deviceId, DateTime exportedAtUtc)
    {
        var shortId = deviceId.ToString("N")[..8];
        return $"bettermal-list-{shortId}-{exportedAtUtc:yyyyMMdd-HHmmss}.json";
    }
}
