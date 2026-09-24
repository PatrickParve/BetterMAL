namespace AnimeTracker.Api.Models;

/// <summary>Fetch-log row for one TMDB movie's images, keyed by TMDB movie id.
/// See <see cref="TmdbTvImageSet"/> for the shape shared by all three
/// stores.</summary>
public class TmdbMovieImageSet
{
    public int MovieId { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public List<TmdbMovieImage> Images { get; set; } = [];
}

/// <summary>One cached poster or backdrop of a <see cref="TmdbMovieImageSet"/>.
/// Keyed on <c>(MovieId, FilePath)</c>.</summary>
public class TmdbMovieImage : TmdbImageBase
{
    public int MovieId { get; set; }
}
