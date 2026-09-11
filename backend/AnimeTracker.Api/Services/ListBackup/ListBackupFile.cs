using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Transfer;

namespace AnimeTracker.Api.Services.ListBackup;

/// <summary>The list backup's own shape (list-backup spec, design.md D4): a
/// saved copy of my list, distinct from the device-transfer file's
/// <c>ExportFile</c> and never read back by this app. <see cref="ExportDevice"/>
/// is reused, since the backup's <c>device</c> follows the transfer header's
/// own rules. No property here carries <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>:
/// every member is always written, null where there is no value.</summary>
public record ListBackupFile(
    string Kind,
    int FormatVersion,
    ExportDevice Device,
    DateTime ExportedAt,
    List<ListBackupEntry> Entries);

public record ListBackupEntry(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    int? TotalEpisodes,
    WatchStatus Status,
    int EpisodesWatched,
    int? Score,
    DateOnly? StartedAt,
    DateOnly? CompletedAt,
    int RewatchCount);
