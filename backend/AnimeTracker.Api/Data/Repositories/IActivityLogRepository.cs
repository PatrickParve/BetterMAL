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
}
