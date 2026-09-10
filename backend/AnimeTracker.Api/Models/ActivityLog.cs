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

/// <summary>Timestamped change record. The only way to reconstruct a chronological
/// "latest updates" feed, since UserAnimeEntry only holds current state.</summary>
public class ActivityLog
{
    /// <summary>Local to this database and never exported. Only meaning: it
    /// breaks ties between rows that share a <see cref="Timestamp"/>, in the
    /// order they were stored here (see <c>ActivityLogRepository</c>).</summary>
    public long Id { get; set; }
    public DateTimeOffset Timestamp { get; set; }

    /// <summary>The identity that travels between devices. Assigned when the
    /// object is created, so every writer — now and later — produces one
    /// without doing anything. A row stored while already carrying an
    /// <see cref="EventId"/> (an imported row) keeps that value.</summary>
    public Guid EventId { get; set; } = Guid.NewGuid();

    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;

    public ActivityChangeType ChangeType { get; set; }
    public string? ChangeDetail { get; set; }

    /// <summary>Episodes-watched value before this change, when ChangeType is
    /// EpisodeIncremented — lets a reader determine increase vs decrease
    /// without re-deriving it from surrounding rows.</summary>
    public int? PreviousEpisodesWatched { get; set; }
}
