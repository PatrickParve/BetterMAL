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

    // metadata-refresh / episode-airing-data "AniList request pacing": a refresh the
    // user starts by hand looks an anime AniList was recorded as not knowing up
    // again, whatever the age of that record.
    [Fact]
    public async Task RefreshOneAsksForAFreshLookupOfAnAnimeAniListDoesNotKnow()
    {
        var airing = new FakeAiringRefreshService();

        await CreateController(new FakeMetadataRefreshService(), airing, new FakeTmdbArtworkService()).RefreshOne(7, CancellationToken.None);

        Assert.Equal([true], airing.RelookupAbsentRequests);
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
        public List<bool> RelookupAbsentRequests { get; } = [];
        public Exception? RefreshFailure { get; set; }

        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default)
        {
            Refreshed.Add(animeId);
            RelookupAbsentRequests.Add(relookupAbsent);
            return RefreshFailure is { } failure ? Task.FromException(failure) : Task.CompletedTask;
        }

        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
