using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Transfer;

/// <summary>The export file's shape (device-transfer spec, design.md D4).
/// Deliberately its own types, distinct from any API DTO, so the file's
/// shape is a contract with 04 and with future builds rather than whatever
/// the rest of the API happens to return today.</summary>
public record ExportFile(
    int FormatVersion,
    ExportDevice Device,
    DateTime ExportedAt,
    ExportRanking Ranking,
    List<ExportAnimePicture> AnimePictures,
    List<ExportSeries> Series,
    List<ExportActivity> Activity);

public record ExportDevice(Guid Id, string? Name);

public record ExportRanking(List<int> AnimeIds, DateTime? ModifiedAt);

public record ExportAnimePicture(int AnimeId, string? SelectedPictureUrl, DateTime ModifiedAt);

/// <summary>A series' title/picture choice blocks. Each block is present
/// exactly when its choice has a time; a block never set or cleared is
/// omitted from the file entirely, never written as null (spec
/// "Series choices travel as independent blocks"). No other property in
/// this file carries <see cref="JsonIgnoreAttribute"/>.</summary>
public record ExportSeries(
    int SeriesId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExportSeriesChoice? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ExportSeriesChoice? Picture);

/// <summary><see cref="Value"/> carries no <see cref="JsonIgnoreAttribute"/>:
/// within a present block, a cleared choice is always written as
/// <c>"value": null</c>, distinct from the block itself being absent.
/// </summary>
public record ExportSeriesChoice(string? Value, DateTime ModifiedAt);

public record ExportActivity(
    Guid Id,
    DateTime Timestamp,
    int AnimeId,
    ActivityChangeType ChangeType,
    string? ChangeDetail,
    int? PreviousEpisodesWatched);
