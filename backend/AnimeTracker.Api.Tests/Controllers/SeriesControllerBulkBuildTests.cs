using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Controllers;

// SeriesController.TriggerBulkBuild (background-jobs "A job is started
// once"/design.md D2): the response reports the run as in flight before the
// background service has woken up and resolved targets, so a single press is
// enough for the Settings page to start polling and disable its button, and a
// second press while it's already running starts nothing new.
public class SeriesControllerBulkBuildTests
{
    private static SeriesController CreateController(ISeriesBulkBuildTrigger trigger, SeriesBulkBuildProgress tracker)
    {
        using var db = new AnimeTrackerDbContext(
            new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var listService = new SeriesListService(new SeriesRankingLookup(db), new UnusedEpisodeScheduleService(), new UnusedAnimeRankingService());
        return new SeriesController(
            new UnusedSeriesService(), listService, trigger, tracker, new UnusedArtworkSelectionService(),
            new UnusedPictureRefreshService(), new FakeTmdbArtworkService(), new UnusedAnimeMetadataRepository());
    }

    [Fact]
    public void TwoPressesGiveRunningBothTimes()
    {
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();
        var controller = CreateController(trigger, tracker);

        Assert.Equal(JobPhase.NotStarted, tracker.Snapshot.Phase);

        var first = Assert.IsType<AcceptedResult>(controller.TriggerBulkBuild());
        Assert.Equal(JobPhase.Running, tracker.Snapshot.Phase);
        Assert.Equal("Running", Assert.IsType<JobDto>(first.Value).Phase);

        var second = Assert.IsType<AcceptedResult>(controller.TriggerBulkBuild());
        Assert.Equal(JobPhase.Running, tracker.Snapshot.Phase);
        Assert.Equal("Running", Assert.IsType<JobDto>(second.Value).Phase);
    }

    [Fact]
    public async Task ExactlyOneSignalIsObservableAcrossTwoPresses()
    {
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgress();
        var controller = CreateController(trigger, tracker);

        controller.TriggerBulkBuild();
        controller.TriggerBulkBuild(); // TryBegin() is false the second time, so Signal() isn't called again

        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
            await trigger.WaitAsync(cts.Token); // completes at once: exactly one signal was queued

        using var secondCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAsync<OperationCanceledException>(() => trigger.WaitAsync(secondCts.Token)); // nothing left to wait for
    }

    private sealed class UnusedSeriesService : ISeriesService
    {
        public Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedArtworkSelectionService : IArtworkSelectionService
    {
        public Task<string?> SetAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> ResetAnimePictureAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> AdoptAnimePictureAsync(int animeId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CheckAnimePictureAsync(int animeId, string pictureUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> AdoptSeriesTitleAsync(int seriesId, string? title, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> AdoptSeriesPictureAsync(int seriesId, string? pictureUrl, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CheckSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedPictureRefreshService : IPictureRefreshService
    {
        public Task<bool> RefreshOneAsync(int animeId, bool evenIfFetched = false, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedAnimeMetadataRepository : IAnimeMetadataRepository
    {
        public Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<AnimeSearchFallbackProjection>> GetSearchFallbackIndexAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(Models.AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(Models.AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(Models.AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<Models.AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    // This test doesn't touch ranking figures — a stub returning the empty
    // ranking is enough (design.md D7/tasks.md 2.7).
    private sealed class UnusedAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Empty);
        public Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
