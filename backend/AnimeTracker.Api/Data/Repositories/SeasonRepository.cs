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

    // Unfiltered on purpose — GetListingAsync's result is filtered by
    // hideHentai, so trusting its emptiness here would make a hentai-only
    // season that genuinely has a MAL listing look unlisted.
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

    public Task<List<SeasonAnimeItem>> GetListingAsync(
        int year, string season, bool hideHentai, CancellationToken ct = default) =>
        GetListingAsync([(year, season)], hideHentai, ct);

    // A year is passed as its four (year, season) points (design D1); the
    // ordering and filtering rules below are identical whatever the point
    // count.
    public async Task<List<SeasonAnimeItem>> GetListingAsync(
        IReadOnlyCollection<(int Year, string Season)> points, bool hideHentai, CancellationToken ct = default)
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

        // EF translates `!=` against a non-null constant with C# null semantics,
        // emitting `("Rating" IS NULL OR "Rating" <> 'rx')` — a not-yet-rated
        // anime is never hidden on suspicion, only a confirmed "rx" is excluded.
        // The in-my-list and type filters that used to live here are now
        // applied client-side (design D3) — hideHentai stays server-side
        // because it reads a field (MAL's rating) the browse DTO doesn't
        // carry to the client.
        if (hideHentai)
            query = query.Where(l => l.Anime.Rating != HentaiRating);

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

        var items = await projected.ToListAsync(ct);

        // The four orderings below are deliberately left in SQL and run as
        // id-only projections rather than reimplemented over `items` in C#
        // (design D3): they carry the anime-ranking capability's banding and
        // Postgres's own title collation, and shipping each as a per-item
        // position is what lets the client re-sort its already-loaded
        // listing without a second copy of either rule.
        var popularityIds = await projected
            // PopularityRank 0/null means "unranked" on MAL — sort those last, then
            // by ascending rank (1 = most popular), then title.
            .OrderBy(a => a.PopularityRank == null || a.PopularityRank == 0 ? 1 : 0)
            .ThenBy(a => a.PopularityRank)
            .ThenBy(a => a.Title)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var malScoreIds = await projected
            .OrderByDescending(a => a.MalScore ?? -1)
            .ThenBy(a => a.Title)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var alphabeticalIds = await projected
            .OrderBy(a => a.Title)
            .Select(a => a.Id)
            .ToListAsync(ct);

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
        var myScoreIds = await (from a in projected
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
            .Select(x => x.a.Id)
            .ToListAsync(ct);

        var popularityPositions = ToPositionMap(popularityIds);
        var malScorePositions = ToPositionMap(malScoreIds);
        var alphabeticalPositions = ToPositionMap(alphabeticalIds);
        var myScorePositions = ToPositionMap(myScoreIds);

        return items
            .Select(a => new SeasonAnimeItem(
                a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.TotalEpisodes, a.MediaType, a.MalScore, a.PopularityRank, a.MyScore, a.InMyList,
                new SeasonSortOrder(popularityPositions[a.Id], malScorePositions[a.Id], alphabeticalPositions[a.Id], myScorePositions[a.Id])))
            .ToList();
    }

    // Positions in a total order over the whole listing (design D3): index 0
    // is first under that ordering, and removing items from the listing
    // client-side preserves the relative order of what's left.
    private static Dictionary<int, int> ToPositionMap(List<int> orderedIds)
    {
        var positions = new Dictionary<int, int>(orderedIds.Count);
        for (var i = 0; i < orderedIds.Count; i++)
            positions[orderedIds[i]] = i;
        return positions;
    }

    // Every point set this repository is ever asked for shares one year — a
    // single (year, season) for the season page, or that year's four seasons
    // for the year page (design D1) — which is what lets the query above test
    // year equality once and season membership via a translatable `Contains`.
    private static (int Year, string[] Seasons) SplitPoints(IReadOnlyCollection<(int Year, string Season)> points) =>
        (points.First().Year, points.Select(p => p.Season).ToArray());
}
