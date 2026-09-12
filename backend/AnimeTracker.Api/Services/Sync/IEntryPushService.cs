using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>How a drain went: <see cref="Pushed"/> succeeded, and
/// <see cref="NotPushed"/> stayed pending for the next attempt (design.md
/// D5) — a caller running this as a job ends failed when NotPushed is
/// greater than zero, and complete otherwise.</summary>
public record DrainResult(int Pushed, int NotPushed);

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
    /// removal, excluding anything held for review. Both id lists are read
    /// before the first push, so <paramref name="progress"/>'s total counts
    /// exactly what this run set out to push (design.md D5). A null
    /// <paramref name="progress"/> makes the run quiet — used by the
    /// automatic 2-minute retry pass, whose runs are never reported as a
    /// "sync now" job (design.md D6).</summary>
    Task<DrainResult> DrainPendingAsync(IJobProgressSink? progress = null, CancellationToken ct = default);
}
