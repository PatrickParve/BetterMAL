using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class TopAnimeRepository(AnimeTrackerDbContext db) : ITopAnimeRepository
{
    public Task<DateTimeOffset?> GetLastFetchedAsync(string rankingType, CancellationToken ct = default) =>
        db.TopAnimeFetchLogs.AsNoTracking()
            .Where(f => f.RankingType == rankingType)
            .Select(f => (DateTimeOffset?)f.LastFetchedAt)
            .FirstOrDefaultAsync(ct);

    public Task<List<TopAnimeRankingEntry>> GetRankingAsync(string rankingType, CancellationToken ct = default) =>
        db.TopAnimeRankingEntries.AsNoTracking()
            .Where(r => r.RankingType == rankingType)
            .Include(r => r.Anime)
            .ThenInclude(a => a.UserEntry)
            .OrderBy(r => r.Rank)
            .ToListAsync(ct);
}
