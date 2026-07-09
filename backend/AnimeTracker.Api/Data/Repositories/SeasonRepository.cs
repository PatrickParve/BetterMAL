using AnimeTracker.Api.Services.Season;
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
        // Listing membership is authoritative: SeasonBrowseService already files
        // each anime under MAL's own start_season, which can differ from the quarter
        // its start_date falls in (e.g. an early-June premiere MAL lists as summer).
        // Re-deriving the season from AiredFrom here would wrongly drop those.
        var query = db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && l.Season == season)
            .Select(l => new
            {
                l.Anime.Id,
                l.Anime.Title,
                l.Anime.EnglishTitle,
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
            // PopularityRank 0/null means "unranked" on MAL — sort those last, then
            // by ascending rank (1 = most popular), then title.
            _ => query
                .OrderBy(a => a.PopularityRank == null || a.PopularityRank == 0 ? 1 : 0)
                .ThenBy(a => a.PopularityRank)
                .ThenBy(a => a.Title),
        };

        var page = await query.Skip(offset).Take(limit).ToListAsync(ct);

        var items = page
            .Select(a => new SeasonAnimeItem(a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.TotalEpisodes, a.MediaType, a.MalScore, a.PopularityRank, a.MyScore))
            .ToList();

        return (items, totalCount);
    }
}
