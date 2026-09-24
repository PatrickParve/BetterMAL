namespace AnimeTracker.Api.Services.Tmdb;

/// <summary>One poster or backdrop as TMDB lists it, after the language
/// filter: <c>Language</c> is "ja", "en", or null for an image with no
/// language. Whether it is a poster or a backdrop is which list of
/// <see cref="TmdbImagesResult.Images"/> it came from. Plain DTO, free of EF
/// types, as the AniList client's are — the caller maps it onto the cache
/// rows.</summary>
public record TmdbImage(string FilePath, string? Language, int Width, int Height);

/// <summary>What one images request came to (design.md D4). A closed set, so
/// the caller must say what each outcome does to the cached set:
/// <see cref="Images"/> replaces it, <see cref="NotFound"/> records it as
/// fetched and empty, and <see cref="Failed"/> leaves it as it was.</summary>
public abstract record TmdbImagesResult
{
    private TmdbImagesResult() { }

    /// <summary>TMDB's posters and backdrops, each in TMDB's own order. A
    /// season has no backdrops, so its list is empty.</summary>
    public sealed record Images(IReadOnlyList<TmdbImage> Posters, IReadOnlyList<TmdbImage> Backdrops) : TmdbImagesResult;

    /// <summary>TMDB does not know the id (404) — an answer, not a failure.</summary>
    public sealed record NotFound : TmdbImagesResult;

    /// <summary>Nothing usable came back: no key configured, a rejected key,
    /// rate limiting that outlasted its one retry, any other error response,
    /// a timeout or a network error. The client has already logged it (or, for
    /// no key, never sent a request); it is never thrown.</summary>
    public sealed record Failed : TmdbImagesResult;
}

/// <summary>Reads image sets from TMDB's v3 API (spec `tmdb-artwork`). Every
/// request asks for exactly Japanese, English and language-less images, and
/// only posters and backdrops are ever read out of the response: logos and
/// every other field are never modelled. Never throws except on the caller's
/// own cancellation, and makes no request at all while no API key is
/// configured.</summary>
public interface ITmdbClient
{
    /// <summary>A TV show's series-level posters and backdrops
    /// (<c>/tv/{id}/images</c>).</summary>
    Task<TmdbImagesResult> GetTvImagesAsync(int tvId, CancellationToken ct = default);

    /// <summary>One season's posters (<c>/tv/{id}/season/{n}/images</c>) —
    /// the only kind that endpoint returns.</summary>
    Task<TmdbImagesResult> GetSeasonImagesAsync(int tvId, int seasonNumber, CancellationToken ct = default);

    /// <summary>A movie's posters and backdrops (<c>/movie/{id}/images</c>).</summary>
    Task<TmdbImagesResult> GetMovieImagesAsync(int movieId, CancellationToken ct = default);
}
