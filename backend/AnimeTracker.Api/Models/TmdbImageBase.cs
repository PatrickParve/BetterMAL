namespace AnimeTracker.Api.Models;

public enum TmdbImageKind
{
    Poster,
    Backdrop,
}

/// <summary>The columns shared by every cached TMDB image row, whichever of the
/// three stores it lives in (<see cref="TmdbTvImage"/>,
/// <see cref="TmdbSeasonImage"/>, <see cref="TmdbMovieImage"/>). A plain base
/// class, deliberately not part of the EF model: each store gets these as
/// ordinary columns of its own table rather than a mapped hierarchy
/// (design.md D5). No URL is stored; it is built on read from
/// <c>FilePath</c> (design.md D6).</summary>
public abstract class TmdbImageBase
{
    /// <summary>TMDB's own path, which already begins with "/".</summary>
    public required string FilePath { get; set; }
    public TmdbImageKind Kind { get; set; }

    /// <summary>"ja" or "en", or null for an image with no language.</summary>
    public string? Language { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    /// <summary>TMDB's own order within its kind — Postgres rows have no
    /// inherent order to fall back on.</summary>
    public int Position { get; set; }
}
