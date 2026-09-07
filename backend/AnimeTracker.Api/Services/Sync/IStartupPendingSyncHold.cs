namespace AnimeTracker.Api.Services.Sync;

/// <summary>design.md D1/D2: stamps every row already pending at process
/// start as held for review, before anything can push it. Must run from
/// Program.cs's synchronous startup scope, after Database.Migrate() and
/// before app.Run() — the only point with a guarantee that no hosted
/// service, controller, or debounce timer has started yet.</summary>
public interface IStartupPendingSyncHold
{
    /// <summary>Idempotent: stamps HeldForReviewAt = now on every
    /// UserAnimeEntry with PendingSync && HeldForReviewAt == null and every
    /// PendingEntryDeletion with HeldForReviewAt == null. A second call
    /// neither re-dates an existing hold nor releases one, since the guard
    /// excludes rows already stamped.</summary>
    Task ApplyAsync(CancellationToken ct = default);
}
