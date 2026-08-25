using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class ActivityLogRepository(AnimeTrackerDbContext db) : IActivityLogRepository
{
    public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Include(l => l.Anime)
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .Take(count)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Include(l => l.Anime)
            .OrderByDescending(l => l.Timestamp)
            .ThenByDescending(l => l.Id)
            .ToListAsync(ct);

    public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
        db.ActivityLogs.AsNoTracking()
            .Where(l => l.ChangeType == ActivityChangeType.EpisodeIncremented && l.Source == ActivityChangeSource.BetterMal
                && l.Timestamp >= fromUtc && l.Timestamp < toUtc)
            .OrderBy(l => l.Timestamp)
            .ToListAsync(ct);
}
