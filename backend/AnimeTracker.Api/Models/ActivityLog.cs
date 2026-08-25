namespace AnimeTracker.Api.Models;

// Appended only, never reordered or renumbered — existing rows already carry
// these values in the database.
public enum ActivityChangeType
{
    Added,
    StatusChanged,
    EpisodeIncremented,
    ScoreChanged,
    Completed,
    RewatchCountChanged,
    Removed,
    StartDateChanged,
    FinishDateChanged,
}

// Appended only, never reordered or renumbered — existing rows already carry
// these values in the database.
public enum ActivityChangeSource
{
    BetterMal,
    MalStartupImport,
    MalReconciliation,
    MalResync,
}

/// <summary>Timestamped change record. The only way to reconstruct a chronological
/// "latest updates" feed, since UserAnimeEntry only holds current state.</summary>
public class ActivityLog
{
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;

    public ActivityChangeType ChangeType { get; set; }
    public string? ChangeDetail { get; set; }

    /// <summary>Episodes-watched value before this change, when ChangeType is
    /// EpisodeIncremented — lets a reader determine increase vs decrease
    /// without re-deriving it from surrounding rows.</summary>
    public int? PreviousEpisodesWatched { get; set; }

    /// <summary>Where the change came from — BetterMal for my own edits (the
    /// enum's zero value, so it falls out for every existing row) or one of
    /// the three MAL-origin paths. Names where the change came from only;
    /// never used to decide whether the change is applied or pushed
    /// (design D3, spec "A record names where the change came from").</summary>
    public ActivityChangeSource Source { get; set; }
}
