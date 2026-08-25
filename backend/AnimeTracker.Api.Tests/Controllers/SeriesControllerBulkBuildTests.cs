using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Artwork;
using AnimeTracker.Api.Services.Series;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Controllers;

// SeriesController.TriggerBulkBuild (design.md decision 5/task 5.2): the
// response reports the run as in flight before the background service has
// woken up and resolved targets, so a single press is enough for the
// Settings page to start polling and disable its button.
public class SeriesControllerBulkBuildTests
{
    [Fact]
    public void TriggerReportsRunningBeforeTheBackgroundServiceHasStarted()
    {
        var trigger = new SeriesBulkBuildTrigger();
        var tracker = new SeriesBulkBuildProgressTracker();
        using var db = new AnimeTrackerDbContext(
            new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var listService = new SeriesListService(new SeriesRankingLookup(db), new UnusedEpisodeScheduleService());
        var controller = new SeriesController(
            new UnusedSeriesService(), listService, trigger, tracker, new UnusedArtworkSelectionService(), new UnusedPictureRefreshService());

        Assert.Equal(SeriesBulkBuildPhase.NotStarted, tracker.Snapshot.Phase);

        var result = Assert.IsType<AcceptedResult>(controller.TriggerBulkBuild());

        Assert.Equal(SeriesBulkBuildPhase.Running, tracker.Snapshot.Phase);
        dynamic dto = result.Value!;
        Assert.Equal("Running", (string)dto.phase);
    }

    private sealed class UnusedSeriesService : ISeriesService
    {
        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task SetFavouriteOrderAsync(int seriesId, List<int> animeIds, CancellationToken ct = default) =>
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
        public Task<string> SetSeriesTitleAsync(int seriesId, string title, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> ResetSeriesTitleAsync(int seriesId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> SetSeriesPictureAsync(int seriesId, string pictureUrl, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<string?> ResetSeriesPictureAsync(int seriesId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class UnusedPictureRefreshService : IPictureRefreshService
    {
        public Task<bool> RefreshOneAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int> RefreshSeriesMainLineAsync(int seriesId, int budget, CancellationToken ct = default) =>
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
}
