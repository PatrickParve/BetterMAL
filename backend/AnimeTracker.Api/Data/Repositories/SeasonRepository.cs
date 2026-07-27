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
        int year, string season, SeasonSortKey sort, bool includeMyList, int offset, int limit, CancellationToken ct = default)
    {
        // Listing membership is authoritative: SeasonBrowseService already files
        // each anime under MAL's own start_season, which can differ from the quarter
        // its start_date falls in (e.g. an early-June premiere MAL lists as summer).
        // Re-deriving the season from AiredFrom here would wrongly drop those.
        var query = db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && l.Season == season);

        if (!includeMyList)
            query = query.Where(l => l.Anime.UserEntry == null);

        var projected = query.Select(l => new
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
            InMyList = l.Anime.UserEntry != null,
        });

        var totalCount = await projected.CountAsync(ct);

        projected = sort switch
        {
            SeasonSortKey.MalScore => projected.OrderByDescending(a => a.MalScore ?? -1).ThenBy(a => a.Title),
            // Scored anime first (highest score down), then every unscored anime
            // falls through to the same unranked-last popularity ordering as the
            // popularity sort (the client renders the "Unwatched" divider between
            // the two groups).
            SeasonSortKey.MyScore => projected
                .OrderBy(a => a.MyScore == null ? 1 : 0)
                .ThenByDescending(a => a.MyScore)
                .ThenBy(a => a.PopularityRank == null || a.PopularityRank == 0 ? 1 : 0)
                .ThenBy(a => a.PopularityRank)
                .ThenBy(a => a.Title),
            SeasonSortKey.Alphabetical => projected.OrderBy(a => a.Title),
            // PopularityRank 0/null means "unranked" on MAL — sort those last, then
            // by ascending rank (1 = most popular), then title.
            _ => projected
                .OrderBy(a => a.PopularityRank == null || a.PopularityRank == 0 ? 1 : 0)
                .ThenBy(a => a.PopularityRank)
                .ThenBy(a => a.Title),
        };

        var page = await projected.Skip(offset).Take(limit).ToListAsync(ct);

        var items = page
            .Select(a => new SeasonAnimeItem(a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.TotalEpisodes, a.MediaType, a.MalScore, a.PopularityRank, a.MyScore, a.InMyList))
            .ToList();

        return (items, totalCount);
    }
}
