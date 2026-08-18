using AnimeTracker.Api.Services.Season;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Data.Repositories;

public class SeasonRepository(AnimeTrackerDbContext db) : ISeasonRepository
{
    // MAL returns this lowercase; rx is the only rating that means Hentai.
    private const string HentaiRating = "rx";

    public Task<DateTimeOffset?> GetLastFetchedAsync(int year, string season, CancellationToken ct = default) =>
        db.SeasonFetchLogs.AsNoTracking()
            .Where(f => f.Year == year && f.Season == season)
            .Select(f => (DateTimeOffset?)f.LastFetchedAt)
            .FirstOrDefaultAsync(ct);

    // Unfiltered on purpose — GetPageAsync's TotalCount is computed after the
    // type/hentai/in-my-list filters, so a type filter could make a season
    // that genuinely has a MAL listing look unlisted.
    public Task<bool> HasListingAsync(int year, string season, CancellationToken ct = default) =>
        db.SeasonAnimeListings.AsNoTracking().AnyAsync(l => l.Year == year && l.Season == season, ct);

    public async Task<SeasonHorizonInputs> GetHorizonInputsAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default)
    {
        var years = points.Select(p => p.Year).ToHashSet();
        var seasons = points.Select(p => p.Season).ToHashSet();

        var fetchedAtByPoint = (await db.SeasonFetchLogs.AsNoTracking()
                .Where(f => years.Contains(f.Year) && seasons.Contains(f.Season))
                .Select(f => new { f.Year, f.Season, f.LastFetchedAt })
                .ToListAsync(ct))
            .Where(f => points.Contains((f.Year, f.Season)))
            .ToDictionary(f => (f.Year, f.Season), f => (DateTimeOffset?)f.LastFetchedAt);

        // Distinct (year, season) pairs ever cached — bounded by the number of
        // season-quarters this deployment has ever fetched, not by anime
        // count, so pulling them into memory to find both the per-point flags
        // and the overall latest (via SeasonCalendar's ordering, which SQL
        // can't express without duplicating its season order) is cheap.
        var listingSeasons = (await db.SeasonAnimeListings.AsNoTracking()
                .Select(l => new { l.Year, l.Season })
                .Distinct()
                .ToListAsync(ct))
            .Select(x => (x.Year, x.Season))
            .ToList();
        var listingSeasonSet = listingSeasons.ToHashSet();

        var resultPoints = points
            .Select(p => new SeasonHorizonPoint(
                p.Year, p.Season,
                fetchedAtByPoint.GetValueOrDefault((p.Year, p.Season)),
                listingSeasonSet.Contains((p.Year, p.Season))))
            .ToList();

        var latest = listingSeasons.Count == 0
            ? ((int Year, string Season)?)null
            : listingSeasons.MaxBy(p => SeasonCalendar.GetSeasonPointIndex(p.Year, p.Season));

        return new SeasonHorizonInputs(resultPoints, latest);
    }

    public async Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        int year, string season, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default)
    {
        // Listing membership is authoritative: SeasonBrowseService already files
        // each anime under MAL's own start_season, which can differ from the quarter
        // its start_date falls in (e.g. an early-June premiere MAL lists as summer).
        // Re-deriving the season from AiredFrom here would wrongly drop those.
        var query = db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && l.Season == season);

        if (!includeMyList)
            query = query.Where(l => l.Anime.UserEntry == null);

        // EF translates `!=` against a non-null constant with C# null semantics,
        // emitting `("Rating" IS NULL OR "Rating" <> 'rx')` — a not-yet-rated
        // anime is never hidden on suspicion, only a confirmed "rx" is excluded.
        if (hideHentai)
            query = query.Where(l => l.Anime.Rating != HentaiRating);

        if (types is { Count: > 0 })
        {
            // "unknown" (an untyped anime) can't be matched by Contains against
            // MediaType's real values, so it needs its own null check — mirrors
            // the client-side `item.mediaType ?? 'unknown'` convention used by
            // My List's and Search's type filters.
            var includeUnknown = types.Contains("unknown");
            var knownTypes = types.Where(t => t != "unknown").ToArray();
            query = query.Where(l =>
                (includeUnknown && l.Anime.MediaType == null) ||
                (l.Anime.MediaType != null && knownTypes.Contains(l.Anime.MediaType)));
        }

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
