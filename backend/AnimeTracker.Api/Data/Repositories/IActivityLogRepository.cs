using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to the activity feed. Backs every page read —
/// never call the MAL client on a render path.</summary>
public interface IActivityLogRepository
{
    Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default);

    /// <summary>Every log newer than <paramref name="cutoffUtc"/>, most recent
    /// first, bounded by <paramref name="maxRows"/> as a safety valve — backs
    /// the "Latest updates" feed's 30-day window. Ordering matches <see
    /// cref="GetRecentAsync"/> (<c>Timestamp desc, Id desc</c>); that ordering
    /// is not optional, since <see
    /// cref="AnimeTracker.Api.Services.Profile.ProfileService.BuildActivityFeed"/>'s
    /// completion/score merge depends on rows arriving in this exact
    /// order.</summary>
    Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default);

    /// <summary>Full history, most recent first — backs the "Latest updates"
    /// box's edit-history overlay.</summary>
    Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Every <see cref="ActivityChangeType.EpisodeIncremented"/> row
    /// in range, oldest first, whose Timestamp falls in the half-open instant
    /// range [fromUtc, toUtc) — the log holds only changes made in the app, so
    /// every row is mine. Backs the recap's per-period logged-progress arm
    /// (list-recaps "Dynamic time filter", design.md decision 3/5) — raw
    /// rows, not yet reduced to a per-anime figure, since <see
    /// cref="AnimeTracker.Api.Services.Recap.RecapWatchLog"/> does that
    /// reduction.</summary>
    Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);

    /// <summary>Every record, oldest first: <see cref="GetRecentAsync"/> and
    /// <see cref="GetAllAsync"/>'s <c>(Timestamp, Id)</c> descending order,
    /// reversed. That order is what lets an import reproduce each save's
    /// internal order (device-transfer). No <c>Include</c>: the export reads
    /// anime ids only, never anime metadata.</summary>
    Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default);
}
