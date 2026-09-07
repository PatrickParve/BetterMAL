namespace AnimeTracker.Api.Services.Sync;

/// <summary>Pushes a pending entry's current state to MAL. Shared by the
/// debounce timer (DebouncedEntrySyncScheduler), the retry job
/// (PendingSyncRetryBackgroundService), and manual "sync now" (SyncController)
/// so there is one place that builds the MAL update and clears pending_sync.
/// A held item (HeldForReviewAt set — design.md D1/D3) is refused by every
/// method here without calling MAL, so every caller inherits the hold with no
/// guard of its own.</summary>
public interface IEntryPushService
{
    /// <summary>Pushes the entry if it is still pending_sync and not held for
    /// review (a no-op otherwise — e.g. another caller already pushed it, or
    /// it is awaiting a review decision). Returns whether a push happened and
    /// succeeded.</summary>
    Task<bool> PushIfPendingAsync(int animeId, CancellationToken ct = default);

    /// <summary>Pushes the anime's pending removal, if one exists and is not
    /// held for review (a no-op otherwise). On success the pending removal
    /// record is cleared; on failure it is left for the next attempt. Returns
    /// whether a push happened and succeeded.</summary>
    Task<bool> PushPendingDeletionAsync(int animeId, CancellationToken ct = default);

    /// <summary>Pushes every currently pending_sync entry and every pending
    /// removal, excluding anything held for review. Returns how many pushes
    /// succeeded.</summary>
    Task<int> DrainPendingAsync(CancellationToken ct = default);
}
