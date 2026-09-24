namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>Which of the three TMDB image stores a set lives in (design.md
/// D5) — and, unchanged, the scope name the pickers and DTOs use (D7, D11).
/// Serialised by name, like every enum in the API.</summary>
public enum TmdbScope
{
    /// <summary>A TV show's series-level posters and backdrops, keyed by TV id.</summary>
    Series,

    /// <summary>One season's posters, keyed by TV id and season number.</summary>
    Season,

    /// <summary>A movie's posters and backdrops, keyed by movie id.</summary>
    Movie,
}

/// <summary>The identity of one cached TMDB image set. Always a TMDB identity,
/// never a MyAnimeList id, so every anime that maps to the same set shares
/// it. <c>Id</c> is the TV id for a series or season set and the movie id for
/// a movie set; <c>SeasonNumber</c> means something for a season set only.</summary>
public readonly record struct TmdbSetKey(TmdbScope Scope, int Id, int SeasonNumber = 0)
{
    public static TmdbSetKey Tv(int tvId) => new(TmdbScope.Series, tvId);

    public static TmdbSetKey Season(int tvId, int seasonNumber) => new(TmdbScope.Season, tvId, seasonNumber);

    public static TmdbSetKey Movie(int movieId) => new(TmdbScope.Movie, movieId);

    /// <summary>The <c>RefreshGate</c> key. Every fetch of one set takes the
    /// same one, whichever page asked, so a season-2 page and a season-3 page
    /// opened together fetch their shared series set once (design.md
    /// D10).</summary>
    public string GateKey => Scope switch
    {
        TmdbScope.Series => $"tmdb:tv:{Id}",
        TmdbScope.Season => $"tmdb:season:{Id}:{SeasonNumber}",
        _ => $"tmdb:movie:{Id}",
    };
}
