using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Ranking;

/// <summary>design.md D1/D3: the one ranking ordering rule, in the two forms
/// it has to exist in — an in-memory comparator over loaded
/// <see cref="UserAnimeEntry"/> rows, and an <see cref="IQueryable{T}"/>
/// extension expressing the identical key in columns so the season/year
/// browsers can keep paging in SQL. The key is, in order: my score
/// descending, then band ascending (<see cref="RankBand"/>), then — within
/// the hand-ordered band only, per the anime-ranking capability's banding
/// requirement — stored position ascending (never-placed last), then title
/// case-insensitively ascending. Short-form and dropped members order by
/// title alone: their place follows from their band, never from a stored
/// position. These two forms are a drift risk (design.md Risks) — a test
/// asserts they produce identical output over one shared fixture, and any
/// change here needs the matching change in the other.</summary>
public static class AnimeRankingKey
{
    public static int Compare(UserAnimeEntry a, UserAnimeEntry b, IReadOnlyDictionary<int, int> positionByAnimeId)
    {
        var scoreCompare = (b.MyScore ?? -1).CompareTo(a.MyScore ?? -1);
        if (scoreCompare != 0)
            return scoreCompare;

        var bandA = RankBandResolver.Resolve(a);
        var bandB = RankBandResolver.Resolve(b);
        var bandCompare = bandA.CompareTo(bandB);
        if (bandCompare != 0)
            return bandCompare;

        if (bandA == RankBand.HandOrdered)
        {
            var positionCompare = PositionOf(a, positionByAnimeId).CompareTo(PositionOf(b, positionByAnimeId));
            if (positionCompare != 0)
                return positionCompare;
        }

        var titleCompare = string.Compare(a.Anime.Title, b.Anime.Title, StringComparison.OrdinalIgnoreCase);
        if (titleCompare != 0)
            return titleCompare;

        return a.AnimeId.CompareTo(b.AnimeId);
    }

    public static List<UserAnimeEntry> OrderEntries(
        IEnumerable<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> positionByAnimeId)
    {
        var ordered = entries.ToList();
        ordered.Sort((a, b) => Compare(a, b, positionByAnimeId));
        return ordered;
    }

    private static int PositionOf(UserAnimeEntry entry, IReadOnlyDictionary<int, int> positionByAnimeId) =>
        positionByAnimeId.GetValueOrDefault(entry.AnimeId, int.MaxValue);

    /// <summary>SQL-translatable form of the same key (design.md D3): left
    /// joins <paramref name="positions"/> for the stored position, computes
    /// the band inline (EF cannot translate a call into
    /// <see cref="RankBandResolver.Resolve"/>), and orders by score
    /// descending, band ascending, position ascending within the
    /// hand-ordered band only, then title. Ordering survives a later
    /// <c>Select</c> projecting back down to <typeparamref name="T"/> alone,
    /// so callers can keep composing the query (e.g. paging) after
    /// this.</summary>
    public static IQueryable<UserAnimeEntry> OrderByRanking(
        this IQueryable<UserAnimeEntry> source, IQueryable<TopAnimeSelection> positions)
    {
        var withKey =
            from e in source
            join p in positions on e.AnimeId equals p.AnimeId into positionJoin
            from p in positionJoin.DefaultIfEmpty()
            select new
            {
                Entry = e,
                Position = (int?)p.Position,
                Band = e.MyScore == null || e.Status == WatchStatus.PlanToWatch || e.Anime.AiringStatus == "not_yet_aired" ? 3
                    : e.Status == WatchStatus.Dropped ? 2
                    : (e.Anime.MediaType == "music" || e.Anime.MediaType == "cm" || e.Anime.MediaType == "pv") ? 1
                    : 0,
            };

        return withKey
            .OrderByDescending(x => x.Entry.MyScore ?? -1)
            .ThenBy(x => x.Band)
            .ThenBy(x => x.Band == 0 ? (x.Position ?? int.MaxValue) : int.MaxValue)
            .ThenBy(x => x.Entry.Anime.Title)
            .Select(x => x.Entry);
    }
}
