using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>The TMDB sets a series draws from (design.md D8, spec `tmdb-artwork`
/// "Which TMDB sets a series draws from"): the shows of its main line, the
/// seasons of those shows, and the movies of every member. Pure: it reads only
/// the mapping rows it is handed.
///
/// A series often spans more than one TMDB TV id (Naruto has four, the Gundam
/// series rooted at 0083 thirty-six), so "the franchise's TV id" is plural.
/// TV ids come from the main line only. That keeps a spin-off show filed as an
/// extra, such as a chibi parody TMDB lists as a separate show, out of the
/// franchise's pool, as <c>SeriesPicturePool</c> keeps extras' MAL pictures
/// out. Movies are the exception: franchise films are usually extras, and the
/// brief asks for them by name.</summary>
/// <param name="Series">Distinct TV ids of main-line members, in main-line order.</param>
/// <param name="Seasons">Distinct <c>(TV id, n ≥ 1)</c> over all members whose
/// TV id is one of <paramref name="Series"/>, ordered by that TV id's position
/// and then by <c>n</c>. This picks up, for example, a special MAL files as an
/// extra but TMDB counts as a numbered season of the main show.</param>
/// <param name="Movies">Distinct movie ids over all members, in main-line
/// order first and then the extras' display order.</param>
public sealed record TmdbFranchiseKeys(
    IReadOnlyList<TmdbSetKey> Series, IReadOnlyList<TmdbSetKey> Seasons, IReadOnlyList<TmdbSetKey> Movies)
{
    /// <param name="mainLine">MAL ids of the main-line members, in main-line
    /// watch order.</param>
    /// <param name="allMembers">MAL ids of every stored member: the main line
    /// first, in that same order, then the extras in their display order —
    /// the order the movies' options are listed in. Related entries shown
    /// beside a series are not members and never belong here.</param>
    /// <param name="mappingsById">The mapping rows of the members, by MAL id.
    /// A member with no row simply contributes nothing.</param>
    public static TmdbFranchiseKeys For(
        IReadOnlyList<int> mainLine, IReadOnlyList<int> allMembers, IReadOnlyDictionary<int, AnimeIdMapping> mappingsById)
    {
        var tvIds = new List<int>();
        foreach (var animeId in mainLine)
            if (mappingsById.TryGetValue(animeId, out var mapping) && mapping.TmdbTvId is { } tvId && !tvIds.Contains(tvId))
                tvIds.Add(tvId);

        var seasons = new HashSet<(int TvId, int Number)>();
        var movieIds = new List<int>();
        foreach (var animeId in allMembers)
        {
            if (!mappingsById.TryGetValue(animeId, out var mapping))
                continue;

            // Season 0 is TMDB's "Specials" bucket and is skipped, as it is for
            // one anime's own scopes (TmdbAnimeScopes).
            if (mapping.TmdbTvId is { } seasonTvId && mapping.TmdbSeasonNumber is >= 1 and var number && tvIds.Contains(seasonTvId))
                seasons.Add((seasonTvId, number));

            foreach (var movieId in mapping.TmdbMovieIds)
                if (!movieIds.Contains(movieId))
                    movieIds.Add(movieId);
        }

        return new TmdbFranchiseKeys(
            tvIds.Select(TmdbSetKey.Tv).ToList(),
            seasons.OrderBy(s => tvIds.IndexOf(s.TvId)).ThenBy(s => s.Number)
                .Select(s => TmdbSetKey.Season(s.TvId, s.Number)).ToList(),
            movieIds.Select(TmdbSetKey.Movie).ToList());
    }

    /// <summary>Every key, in fetch order: the series sets, then the seasons,
    /// then the movies — so a budget-limited visit fetches the most
    /// representative art first.</summary>
    public IEnumerable<TmdbSetKey> Keys => Series.Concat(Seasons).Concat(Movies);
}
