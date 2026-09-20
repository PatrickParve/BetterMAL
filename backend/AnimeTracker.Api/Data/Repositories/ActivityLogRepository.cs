using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class ActivityLogRepository(AnimeTrackerDbContext db) : IActivityLogRepository
{
    public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Include(l => l.Anime)
            .OrderByDescending(l => l.Timestamp)
            // Id breaks same-timestamp ties by storage order on this
            // database — it's local, never exported, but ordering by it is
            // what lets ActivityFeedComposer fold a completion and its score
            // into one row. An import must preserve this order when it
            // inserts (design.md D6, Open Questions).
            .ThenByDescending(l => l.Id)
            .Take(count)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Include(l => l.Anime)
            .Where(l => l.Timestamp >= cutoffUtc)
            .OrderByDescending(l => l.Timestamp)
            // See the comment on the same ordering in GetRecentAsync.
            .ThenByDescending(l => l.Id)
            .Take(maxRows)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Include(l => l.Anime)
            .OrderByDescending(l => l.Timestamp)
            // See the comment on the same ordering in GetRecentAsync.
            .ThenByDescending(l => l.Id)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Where(l => l.ChangeType == ActivityChangeType.EpisodeIncremented
                && l.Timestamp >= fromUtc && l.Timestamp < toUtc)
            .OrderBy(l => l.Timestamp)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .OrderBy(l => l.Timestamp)
            // See the comment on the same ordering, reversed, in GetRecentAsync.
            .ThenBy(l => l.Id)
            .ToListAsync(ct);
}
