using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class SeasonRepository(AnimeTrackerDbContext db) : ISeasonRepository
{
    public Task<DateTimeOffset?> GetLastFetchedAsync(int year, string season, CancellationToken ct = default) =>
        db.SeasonFetchLogs.AsNoTracking()
            .Where(f => f.Year == year && f.Season == season)
            .Select(f => (DateTimeOffset?)f.LastFetchedAt)
            .FirstOrDefaultAsync(ct);

    public async Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        int year, string season, SeasonSortKey sort, int offset, int limit, CancellationToken ct = default)
    {
        var query = db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && l.Season == season)
            .Select(l => new
            {
                l.Anime.Id,
                l.Anime.Title,
                l.Anime.PictureUrl,
                l.Anime.TotalEpisodes,
                l.Anime.MediaType,
                l.Anime.MalScore,
                l.Anime.PopularityRank,
                MyScore = l.Anime.UserEntry != null ? l.Anime.UserEntry.MyScore : null,
            });

        var totalCount = await query.CountAsync(ct);

        query = sort switch
        {
            SeasonSortKey.MalScore => query.OrderByDescending(a => a.MalScore ?? -1).ThenBy(a => a.Title),
            SeasonSortKey.MyScore => query.OrderByDescending(a => a.MyScore ?? -1).ThenBy(a => a.Title),
            SeasonSortKey.Alphabetical => query.OrderBy(a => a.Title),
            _ => query.OrderBy(a => a.PopularityRank ?? int.MaxValue).ThenBy(a => a.Title),
        };

        var page = await query.Skip(offset).Take(limit).ToListAsync(ct);

        var items = page
            .Select(a => new SeasonAnimeItem(a.Id, a.Title, a.PictureUrl, a.TotalEpisodes, a.MediaType, a.MalScore, a.PopularityRank, a.MyScore))
            .ToList();

        return (items, totalCount);
    }
}
