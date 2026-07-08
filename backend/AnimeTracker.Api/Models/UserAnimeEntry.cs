namespace AnimeTracker.Api.Models;

public enum WatchStatus
{
    Watching,
    Completed,
    OnHold,
    Dropped,
    PlanToWatch,
}

/// <summary>My relationship to an anime. Keyed by AnimeId (shared PK/FK with
/// AnimeMetadata) since there is at most one entry per anime for a single user.</summary>
public class UserAnimeEntry
{
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;

    public WatchStatus Status { get; set; }
    public int EpisodesWatched { get; set; }
    public int? MyScore { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
    public int RewatchCount { get; set; }
    public bool PendingSync { get; set; }
    public DateTimeOffset? LastSyncedAt { get; set; }
}
