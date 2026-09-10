using System.Data;
using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Transfer;

public class ExportService(
    AnimeTrackerDbContext db,
    ITopAnimeSelectionRepository topAnimeSelectionRepository,
    IActivityLogRepository activityLogRepository) : IExportService
{
    public async Task<(byte[] Bytes, string FileName)> ExportAsync(string? userAgent, CancellationToken ct = default)
    {
        // RepeatableRead: every statement in this transaction sees the
        // snapshot taken at its first statement (Postgres semantics), so a
        // save landing mid-export appears in the file wholly or not at all —
        // in particular, the ranking's list and its time always come from
        // the same moment (design.md D2). The transaction writes nothing, so
        // it is never committed; disposal rolls back nothing.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct);

        var deviceId = await db.DeviceIdentities.AsNoTracking()
            .Select(d => d.DeviceId)
            .SingleAsync(ct);

        var animeIds = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        var rankingModifiedAt = await topAnimeSelectionRepository.GetModifiedAtAsync(ct);

        // D8: filtered on the time alone, with no join against the current
        // list — a stamped choice is exported wherever it is stored, even
        // for an anime since removed from my list. A projection, not a
        // repository read: only the columns the file needs, not a whole
        // AnimeMetadata row with its picture set.
        var pictureRows = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.SelectedPictureModifiedAt != null)
            .OrderBy(a => a.Id)
            .Select(a => new { a.Id, a.SelectedPictureUrl, a.SelectedPictureModifiedAt })
            .ToListAsync(ct);

        var seriesRows = await db.Series.AsNoTracking()
            .Where(s => s.SelectedTitleModifiedAt != null || s.SelectedPictureModifiedAt != null)
            .OrderBy(s => s.Id)
            .Select(s => new
            {
                s.Id,
                s.SelectedTitle,
                s.SelectedTitleModifiedAt,
                s.SelectedPictureUrl,
                s.SelectedPictureModifiedAt,
            })
            .ToListAsync(ct);

        var logRows = await activityLogRepository.GetAllOldestFirstAsync(ct);

        var exportedAt = DateTime.UtcNow;

        var file = new ExportFile(
            FormatVersion: 1,
            Device: new ExportDevice(deviceId, DeviceName.Describe(userAgent)),
            ExportedAt: exportedAt,
            Ranking: new ExportRanking(animeIds, rankingModifiedAt?.UtcDateTime),
            AnimePictures: pictureRows
                .Select(a => new ExportAnimePicture(a.Id, a.SelectedPictureUrl, a.SelectedPictureModifiedAt!.Value.UtcDateTime))
                .ToList(),
            Series: seriesRows
                .Select(s => new ExportSeries(
                    s.Id,
                    s.SelectedTitleModifiedAt is { } titleModifiedAt
                        ? new ExportSeriesChoice(s.SelectedTitle, titleModifiedAt.UtcDateTime)
                        : null,
                    s.SelectedPictureModifiedAt is { } pictureModifiedAt
                        ? new ExportSeriesChoice(s.SelectedPictureUrl, pictureModifiedAt.UtcDateTime)
                        : null))
                .ToList(),
            Activity: logRows
                .Select(l => new ExportActivity(l.EventId, l.Timestamp.UtcDateTime, l.AnimeId, l.ChangeType, l.ChangeDetail, l.PreviousEpisodesWatched))
                .ToList());

        var bytes = JsonSerializer.SerializeToUtf8Bytes(file, ExportJson.Options);
        var fileName = BuildFileName(deviceId, exportedAt);

        return (bytes, fileName);
    }

    /// <summary>design.md D6: the first 8 hex digits of the device id name
    /// the device; the <paramref name="exportedAtUtc"/> timestamp tells two
    /// exports from the same device apart. internal so file-name tests
    /// (task 5.6) can call it directly without exercising the whole service
    /// — AnimeTracker.Api.Tests holds InternalsVisibleTo.</summary>
    internal static string BuildFileName(Guid deviceId, DateTime exportedAtUtc)
    {
        var shortId = deviceId.ToString("N")[..8];
        return $"bettermal-{shortId}-{exportedAtUtc:yyyyMMdd-HHmmss}.json";
    }
}
