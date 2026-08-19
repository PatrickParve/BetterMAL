namespace AnimeTracker.Api.Models;

public enum WatchStatus
{
    Watching,
    Completed,
    OnHold,
    Dropped,
    PlanToWatch,
}

// score-visibility: "always show MAL scores for completed and dropped
// shows" reveals a score when the entry is Completed or Dropped — both are
// statuses in which the user has settled their relationship with the anime,
// so a community average can no longer bias or spoil a viewing still ahead
// of them. A null status (entry not in my list at all) is neither.
public static class WatchStatusExtensions
{
    public static bool IsScoreRevealable(this WatchStatus status) =>
        status is WatchStatus.Completed or WatchStatus.Dropped;

    public static bool IsScoreRevealable(this WatchStatus? status) =>
        status is WatchStatus.Completed or WatchStatus.Dropped;
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
