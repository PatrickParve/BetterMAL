using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Controllers;

// MetadataRefreshController.RefreshOne (metadata-refresh "On-demand
// single-anime refresh", anime-detail "On-demand refresh action", design.md
// D15): one MAL refresh, then an AniList refresh and a forced TMDB refetch,
// each failure-isolated from the other and from the action's own success.
public class MetadataRefreshControllerTests
{
    private static MetadataRefreshController CreateController(
        FakeMetadataRefreshService metadata, FakeAiringRefreshService airing, FakeTmdbArtworkService tmdb) =>
        new(metadata, airing, tmdb, NullLogger<MetadataRefreshController>.Instance);

    [Fact]
    public async Task RefreshOneAlsoRefetchesTheAnimesTmdbSetsWhateverTheirAge()
    {
        var metadata = new FakeMetadataRefreshService();
        var airing = new FakeAiringRefreshService();
        var tmdb = new FakeTmdbArtworkService();

        var result = await CreateController(metadata, airing, tmdb).RefreshOne(7, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([7], metadata.Refreshed);
        Assert.Equal([7], airing.Refreshed);
        Assert.Equal([(7, true)], tmdb.AnimeRefreshes); // force: true, "Refresh data" asks for fresh images
    }

    [Fact]
    public async Task RefreshOneStillReturns204WhenTheTmdbStepThrows()
    {
        var tmdb = new FakeTmdbArtworkService { RefreshFailure = new HttpRequestException("TMDB is down") };

        var result = await CreateController(new FakeMetadataRefreshService(), new FakeAiringRefreshService(), tmdb)
            .RefreshOne(7, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Single(tmdb.AnimeRefreshes); // it was tried; its failure was logged, not surfaced
    }

    [Fact]
    public async Task RefreshOneStillRefetchesTmdbWhenTheAiringStepThrows()
    {
        var airing = new FakeAiringRefreshService { RefreshFailure = new HttpRequestException("AniList is down") };
        var tmdb = new FakeTmdbArtworkService();

        var result = await CreateController(new FakeMetadataRefreshService(), airing, tmdb).RefreshOne(7, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.Equal([(7, true)], tmdb.AnimeRefreshes); // the two steps after the MyAnimeList one don't depend on each other
    }

    [Fact]
    public async Task AnUnknownAnimeIs404AndNeitherLaterStepRuns()
    {
        var metadata = new FakeMetadataRefreshService { NotFound = true };
        var airing = new FakeAiringRefreshService();
        var tmdb = new FakeTmdbArtworkService();

        var result = await CreateController(metadata, airing, tmdb).RefreshOne(7, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(airing.Refreshed);
        Assert.Empty(tmdb.AnimeRefreshes);
    }

    private sealed class FakeMetadataRefreshService : IMetadataRefreshService
    {
        public List<int> Refreshed { get; } = [];
        public bool NotFound { get; set; }

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Refreshed.Add(animeId);
            return NotFound ? Task.FromException(new AnimeMetadataNotFoundException(animeId)) : Task.CompletedTask;
        }
    }

    private sealed class FakeAiringRefreshService : IEpisodeScheduleRefreshService
    {
        public List<int> Refreshed { get; } = [];
        public Exception? RefreshFailure { get; set; }

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Refreshed.Add(animeId);
            return RefreshFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }

        public Task<RefreshManyResult> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null) =>
            throw new NotImplementedException();
        public Task BackfillAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }
}
