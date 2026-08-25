using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to the activity feed. Backs every page read —
/// never call the MAL client on a render path.</summary>
public interface IActivityLogRepository
{
    Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default);

    /// <summary>Full history, most recent first — backs the "Latest updates"
    /// box's edit-history overlay.</summary>
    Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Every <see cref="ActivityChangeType.EpisodeIncremented"/> row
    /// recorded from my own use of the app (<see
    /// cref="ActivityChangeSource.BetterMal"/>) whose Timestamp falls in the
    /// half-open instant range [fromUtc, toUtc), oldest first. Backs the
    /// recap's per-period logged-progress arm (list-recaps "Dynamic time
    /// filter", design.md decision 3/5) — raw rows, not yet reduced to a
    /// per-anime figure, since <see
    /// cref="AnimeTracker.Api.Services.Recap.RecapWatchLog"/> does that
    /// reduction. MAL-origin rows are excluded because a synced row's
    /// timestamp is when the sync ran, not when the episode was watched
    /// (record-mal-origin-activity design D6).</summary>
    Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default);
}
