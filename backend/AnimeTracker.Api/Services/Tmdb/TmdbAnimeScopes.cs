using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>The TMDB sets one anime draws from, in its three scopes (design.md
/// D7, spec `tmdb-artwork` "Which TMDB sets an anime draws from"). Pure: it
/// reads only the anime's mapping row.</summary>
/// <param name="Series">The set of its TMDB TV id, when it has one.</param>
/// <param name="Season">The set for its TV id and TMDB season number, when
/// that number is 1 or more. Season 0 is TMDB's "Specials" bucket, whose
/// posters describe the bucket rather than the anime (for a MOVIE-type entry
/// mapped there, such as Naruto's films, they would say nothing about it), so
/// it is skipped here — the one place that is decided — while the stored
/// mapping stays a faithful copy of the source.</param>
/// <param name="Movie">Every movie id it maps to, taken together as its one
/// Movie scope, in the mapping's order.</param>
public sealed record TmdbAnimeScopes(TmdbSetKey? Series, TmdbSetKey? Season, IReadOnlyList<TmdbSetKey> Movie)
{
    public static TmdbAnimeScopes For(AnimeIdMapping? mapping)
    {
        if (mapping is null)
            return new TmdbAnimeScopes(null, null, []);

        // A season number never stands without a TV id (the parser only keeps
        // it alongside one), but the pairing is checked here too.
        TmdbSetKey? series = mapping.TmdbTvId is { } tvId ? TmdbSetKey.Tv(tvId) : null;
        TmdbSetKey? season = mapping.TmdbTvId is { } seasonTvId && mapping.TmdbSeasonNumber is >= 1 and var seasonNumber
            ? TmdbSetKey.Season(seasonTvId, seasonNumber)
            : null;
        var movies = mapping.TmdbMovieIds.Distinct().Select(TmdbSetKey.Movie).ToList();

        return new TmdbAnimeScopes(series, season, movies);
    }

    /// <summary>Every key, in fetch order: the series set, the season set,
    /// then the movie sets.</summary>
    public IEnumerable<TmdbSetKey> Keys
    {
        get
        {
            if (Series is { } series) yield return series;
            if (Season is { } season) yield return season;
            foreach (var movie in Movie) yield return movie;
        }
    }
}
