namespace AnimeTracker.Api.Models;

public enum ActivityChangeType
{
    Added,
    StatusChanged,
    EpisodeIncremented,
    ScoreChanged,
    Completed,
    RewatchCountChanged,
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
}
