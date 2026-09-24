using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// A scriptable ITmdbClient that records each request as the TmdbSetKey it was
// for. Shared by the artwork-service, artwork-selection and device-transfer
// tests, which all need "what did TMDB get asked for" and "what did it
// answer". Thread-safe, since a test may run two services against it at once.
internal sealed class FakeTmdbClient : ITmdbClient
{
    private readonly object _lock = new();
    private readonly Dictionary<TmdbSetKey, TmdbImagesResult> _results = [];
    private readonly List<TmdbSetKey> _calls = [];

    /// <summary>What a key with nothing scripted answers: an empty set.</summary>
    public TmdbImagesResult DefaultResult { get; set; } = new TmdbImagesResult.Images([], []);

    /// <summary>Awaited after a request is recorded and before it is
    /// answered, so a test can hold a request open while it does something
    /// else.</summary>
    public Func<TmdbSetKey, Task>? BeforeAnswer { get; set; }

    /// <summary>Every request made so far, in order.</summary>
    public IReadOnlyList<TmdbSetKey> Calls
    {
        get
        {
            lock (_lock)
                return [.. _calls];
        }
    }

    public FakeTmdbClient Serve(TmdbSetKey key, TmdbImagesResult result)
    {
        lock (_lock)
            _results[key] = result;
        return this;
    }

    public FakeTmdbClient Serve(TmdbSetKey key, IReadOnlyList<TmdbImage> posters, IReadOnlyList<TmdbImage>? backdrops = null) =>
        Serve(key, new TmdbImagesResult.Images(posters, backdrops ?? []));

    public static TmdbImage Image(string filePath, string? language = "ja", int width = 2000, int height = 3000) =>
        new(filePath, language, width, height);

    public Task<TmdbImagesResult> GetTvImagesAsync(int tvId, CancellationToken ct = default) =>
        AnswerAsync(TmdbSetKey.Tv(tvId));

    public Task<TmdbImagesResult> GetSeasonImagesAsync(int tvId, int seasonNumber, CancellationToken ct = default) =>
        AnswerAsync(TmdbSetKey.Season(tvId, seasonNumber));

    public Task<TmdbImagesResult> GetMovieImagesAsync(int movieId, CancellationToken ct = default) =>
        AnswerAsync(TmdbSetKey.Movie(movieId));

    private async Task<TmdbImagesResult> AnswerAsync(TmdbSetKey key)
    {
        lock (_lock)
            _calls.Add(key);

        if (BeforeAnswer is { } before)
            await before(key);

        lock (_lock)
            return _results.TryGetValue(key, out var result) ? result : DefaultResult;
    }
}
