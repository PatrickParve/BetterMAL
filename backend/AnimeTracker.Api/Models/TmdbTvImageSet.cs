namespace AnimeTracker.Api.Models;

/// <summary>Fetch-log row for one TMDB TV show's series-level images, keyed by
/// TMDB TV id and never by MAL id, so every MAL entry that maps to the show
/// shares it. Its existence is what tells "fetched, and empty" from "never
/// fetched"; its age decides when it is due again (design.md D5, D10). A
/// refetch replaces the image rows wholesale and restamps
/// <c>FetchedAt</c>.</summary>
public class TmdbTvImageSet
{
    public int TvId { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public List<TmdbTvImage> Images { get; set; } = [];
}

/// <summary>One cached poster or backdrop of a <see cref="TmdbTvImageSet"/>.
/// Keyed on <c>(TvId, FilePath)</c>.</summary>
public class TmdbTvImage : TmdbImageBase
{
    public int TvId { get; set; }
}
