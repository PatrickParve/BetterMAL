namespace AnimeTracker.Api.Models;

/// <summary>Fetch-log row for one season of a TMDB TV show, keyed on
/// <c>(TvId, SeasonNumber)</c>. Season 0 (TMDB's "Specials" bucket) is never
/// fetched (design.md D7). See <see cref="TmdbTvImageSet"/> for the shape
/// shared by all three stores.</summary>
public class TmdbSeasonImageSet
{
    public int TvId { get; set; }
    public int SeasonNumber { get; set; }
    public DateTimeOffset FetchedAt { get; set; }

    public List<TmdbSeasonImage> Images { get; set; } = [];
}

/// <summary>One cached poster of a <see cref="TmdbSeasonImageSet"/>. TMDB's
/// season endpoint returns posters only. Keyed on
/// <c>(TvId, SeasonNumber, FilePath)</c>.</summary>
public class TmdbSeasonImage : TmdbImageBase
{
    public int TvId { get; set; }
    public int SeasonNumber { get; set; }
}
