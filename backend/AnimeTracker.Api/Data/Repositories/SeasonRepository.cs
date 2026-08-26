using AnimeTracker.Api.Models;
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
        HasListingAsync([(year, season)], ct);

    public Task<bool> HasListingAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default)
    {
        var (year, seasons) = SplitPoints(points);
        return db.SeasonAnimeListings.AsNoTracking().AnyAsync(l => l.Year == year && seasons.Contains(l.Season), ct);
    }

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

    public Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        int year, string season, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default) =>
        GetPageAsync([(year, season)], sort, includeMyList, hideHentai, types, offset, limit, ct);

    // A year is passed as its four (year, season) points (design D1); the
    // ordering, filtering, counting, and paging rules below are identical
    // whatever the point count.
    public async Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        IReadOnlyCollection<(int Year, string Season)> points, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default)
    {
        var (year, seasons) = SplitPoints(points);

        // Listing membership is authoritative: SeasonBrowseService already files
        // each anime under MAL's own start_season, which can differ from the quarter
        // its start_date falls in (e.g. an early-June premiere MAL lists as summer).
        // Re-deriving the season from AiredFrom here would wrongly drop those.
        //
        // EF can't translate `Contains` over a collection of value tuples, so
        // the predicate is built from the points explicitly instead: every
        // caller's point set shares a single year — the season endpoint
        // passes one point, the year endpoint passes that year's four
        // seasons — so a year equality test plus a season-membership test
        // (translated to a SQL IN clause) covers both and still translates to
        // SQL rather than falling back to client evaluation.
        var query = db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && seasons.Contains(l.Season));

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
            l.Anime.AiringStatus,
            MyScore = l.Anime.UserEntry != null ? l.Anime.UserEntry.MyScore : null,
            InMyList = l.Anime.UserEntry != null,
            Status = l.Anime.UserEntry != null ? (WatchStatus?)l.Anime.UserEntry.Status : null,
        });

        var totalCount = await projected.CountAsync(ct);

        projected = sort switch
        {
            SeasonSortKey.MalScore => projected.OrderByDescending(a => a.MalScore ?? -1).ThenBy(a => a.Title),
            // Scored anime first (highest score down), then every unscored anime
            // falls through to the same unranked-last popularity ordering as the
            // popularity sort (the client renders the "Unwatched" divider between
            // the two groups). Equal scores are broken by the anime-ranking
            // capability's ordering rule (design.md D3): band ascending, then
            // stored position ascending within the hand-ordered band, then —
            // for anime the ranking doesn't cover (band 3: dropped's
            // hand-ordered peers rank above it, but a still-scored plan-to-
            // watch or unaired anime lands in band 3 here) — the same
            // unranked-last popularity fallback the plain popularity sort
            // uses, per the season-browser capability. This inlines
            // Services/Ranking/AnimeRankingKey's rule rather than calling it,
            // since EF cannot translate a call into shared C# logic and this
            // query's shape isn't IQueryable<UserAnimeEntry> to begin with —
            // the two must move together.
            SeasonSortKey.MyScore =>
                (from a in projected
                 join p in db.TopAnimeSelections.AsNoTracking() on a.Id equals p.AnimeId into positionJoin
                 from p in positionJoin.DefaultIfEmpty()
                 select new
                 {
                     a,
                     Position = (int?)p.Position,
                     Band = a.MyScore == null || a.Status == WatchStatus.PlanToWatch || a.AiringStatus == "not_yet_aired" ? 3
                         : a.Status == WatchStatus.Dropped ? 2
                         : (a.MediaType == "music" || a.MediaType == "cm" || a.MediaType == "pv") ? 1
                         : 0,
                 })
                .OrderBy(x => x.a.MyScore == null ? 1 : 0)
                .ThenByDescending(x => x.a.MyScore)
                .ThenBy(x => x.Band)
                .ThenBy(x => x.Band == 0 ? (x.Position ?? int.MaxValue) : int.MaxValue)
                .ThenBy(x => x.Band == 3 ? (x.a.PopularityRank == null || x.a.PopularityRank == 0 ? 1 : 0) : 0)
                .ThenBy(x => x.Band == 3 ? x.a.PopularityRank : null)
                .ThenBy(x => x.a.Title)
                .Select(x => x.a),
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

    // Every point set this repository is ever asked for shares one year — a
    // single (year, season) for the season page, or that year's four seasons
    // for the year page (design D1) — which is what lets the query above test
    // year equality once and season membership via a translatable `Contains`.
    private static (int Year, string[] Seasons) SplitPoints(IReadOnlyCollection<(int Year, string Season)> points) =>
        (points.First().Year, points.Select(p => p.Season).ToArray());
}
