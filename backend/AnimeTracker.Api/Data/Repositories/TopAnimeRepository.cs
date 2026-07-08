using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class TopAnimeRepository(AnimeTrackerDbContext db) : ITopAnimeRepository
{
    public Task<DateTimeOffset?> GetLastFetchedAsync(CancellationToken ct = default) =>
        db.TopAnimeFetchLogs.AsNoTracking()
            .Select(f => (DateTimeOffset?)f.LastFetchedAt)
            .FirstOrDefaultAsync(ct);

    public Task<List<TopAnimeRankingEntry>> GetRankingAsync(CancellationToken ct = default) =>
        db.TopAnimeRankingEntries.AsNoTracking()
            .Include(r => r.Anime)
            .ThenInclude(a => a.UserEntry)
            .OrderBy(r => r.Rank)
            .ToListAsync(ct);
}
