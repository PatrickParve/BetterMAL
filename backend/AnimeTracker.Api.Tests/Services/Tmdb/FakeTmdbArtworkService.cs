using AnimeTracker.Api.Services.Tmdb;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// An ITmdbArtworkService for the tests that only care which of its members a
// caller reaches, not what TMDB holds: the controllers' constructor
// dependencies, and "did this action refetch, and did it survive the fetch
// failing". RefreshAnimeAsync records its arguments and can be made to throw.
// Every other member throws, so a caller that reaches for one fails the test
// loudly rather than reading an invented answer. Tests that need TMDB's real
// answers use TmdbArtworkService over FakeTmdbClient instead.
internal sealed class FakeTmdbArtworkService : ITmdbArtworkService
{
    /// <summary>Every <c>RefreshAnimeAsync</c> call so far, in order.</summary>
    public List<(int AnimeId, bool Force)> AnimeRefreshes { get; } = [];

    /// <summary>Thrown by <c>RefreshAnimeAsync</c> after it records the call.</summary>
    public Exception? RefreshFailure { get; set; }

    public Task RefreshAnimeAsync(int animeId, bool force, CancellationToken ct = default)
    {
        AnimeRefreshes.Add((animeId, force));
        return RefreshFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
    }

    public Task<AnimeTmdbPicturesDto?> GetAnimePicturesAsync(int animeId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<SeriesTmdbPicturesDto> GetSeriesPicturesAsync(int seriesId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<bool> IsAnimeFetchDueAsync(int animeId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<string>> GetAnimeOptionUrlsAsync(int animeId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<string>> GetSeriesOptionUrlsAsync(int seriesId, CancellationToken ct = default) =>
        throw new NotImplementedException();

    public Task<int> RefreshSeriesAsync(int seriesId, int budget, bool force, CancellationToken ct = default) =>
        throw new NotImplementedException();
}
