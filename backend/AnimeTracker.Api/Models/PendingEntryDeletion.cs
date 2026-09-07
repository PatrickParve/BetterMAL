namespace AnimeTracker.Api.Models;

/// <summary>A queued-but-not-yet-pushed removal of an anime from my list. Kept
/// separate from UserAnimeEntry (rather than a tombstone column on it) since
/// the entry itself is deleted the moment a removal is requested — this
/// record is the only thing that outlives it until the MAL push succeeds. No
/// relationship to UserAnimeEntry; the FK is to AnimeMetadata, which is
/// retained as cache data after the entry is gone.</summary>
public class PendingEntryDeletion
{
    public int AnimeId { get; set; }
    public AnimeMetadata Anime { get; set; } = null!;
    public DateTimeOffset RequestedAt { get; set; }

    /// <summary>Non-null means this removal was already waiting to be pushed
    /// when the process started, and is awaiting my decision.</summary>
    public DateTimeOffset? HeldForReviewAt { get; set; }
}
