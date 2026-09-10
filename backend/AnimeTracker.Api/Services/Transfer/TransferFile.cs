namespace AnimeTracker.Api.Services.Transfer;

/// <summary>The import-side reading of an export file's shape (design.md
/// D3) — deliberately its own types, distinct from <see cref="ExportFile"/>
/// and its family: a reader needs a notion of a required member that the
/// export-side records have no use for, and one change type this build
/// doesn't know must not fail deserializing the whole file the way
/// <c>ExportActivity</c>'s enum-typed <c>ChangeType</c> would. Times are
/// <see cref="DateTimeOffset"/>: a "…Z" string reads back to the exact
/// stored tick, since 03 D5 writes full precision.</summary>
public record TransferFile(
    int FormatVersion,
    TransferDevice Device,
    DateTimeOffset ExportedAt,
    TransferRanking Ranking,
    List<TransferAnimePicture> AnimePictures,
    List<TransferSeries> Series,
    List<TransferActivity> Activity);

public record TransferDevice(Guid Id, string? Name);

public record TransferRanking(List<int> AnimeIds, DateTimeOffset? ModifiedAt);

public record TransferAnimePicture(int AnimeId, string? SelectedPictureUrl, DateTimeOffset ModifiedAt);

/// <summary>A series' title/picture choice blocks. A block that is absent
/// from the file reads as null here and means "no choice to compare" —
/// distinct from a present block whose <see cref="TransferSeriesChoice.Value"/>
/// is null, which is a clear (design.md D3 "Absent versus cleared series
/// blocks"). The default values are what let the block be absent from the
/// JSON at all under <c>RespectRequiredConstructorParameters</c>, rather
/// than refusing the file as damaged.</summary>
public record TransferSeries(int SeriesId, TransferSeriesChoice? Title = null, TransferSeriesChoice? Picture = null);

public record TransferSeriesChoice(string? Value, DateTimeOffset ModifiedAt);

/// <summary><see cref="ChangeType"/> is a string, not <c>ActivityChangeType</c>
/// (design.md D3): a value this build doesn't know must stay with its
/// record rather than fail deserializing the whole file. See
/// <see cref="TransferFileReader.MapChangeType"/>.</summary>
public record TransferActivity(
    Guid Id,
    DateTimeOffset Timestamp,
    int AnimeId,
    string ChangeType,
    string? ChangeDetail,
    int? PreviousEpisodesWatched);
