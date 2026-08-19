using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// RecapController (tasks.md 5.6): validates mode/season the same way
// SeasonController does (400, never reaching the service), and never turns
// an empty-but-valid period into an error.
public class RecapControllerTests
{
    private static RecapDto EmptyRecap(RecapPeriod period, string filter) =>
        new(period.Mode, period.StartYear, period.EndYear, period.Season, filter,
            WatchedCount: 0, AiredCount: 0,
            Stats: new RecapStatsDto(null, 0, 0, 0, 0, 0, 0, 0, []),
            Items: [], SeasonRanking: [], YearRanking: [], SeasonTimeRanking: [], YearTimeRanking: []);

    [Fact]
    public async Task Get_UnknownModeIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get("decade", null, null, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_UnknownSeasonNameIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.Season, null, null, 2022, "monsoon", RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_UnknownFilterIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.Yearly, null, null, 2022, null, "someday", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_MissingRequiredParametersForTheModeIsRejected()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.MultiYear, null, 2020, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_ValidYearlyRequestReturnsTheDto()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.Yearly, null, null, 2022, null, RecapTimeFilter.Aired, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RecapDto>(ok.Value);
        Assert.Equal(2022, dto.StartYear);
        var request = Assert.Single(service.Requests);
        Assert.Equal(RecapTimeFilter.Aired, request.Filter);
    }

    [Fact]
    public async Task Get_EmptyPeriodReturnsAWellFormedEmptyResponseRatherThanAnError()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.Yearly, null, null, 1999, null, RecapTimeFilter.Watched, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RecapDto>(ok.Value);
        Assert.Equal(0, dto.WatchedCount);
        Assert.Equal(0, dto.AiredCount);
        Assert.Empty(dto.Items);
        Assert.Null(dto.Stats.MeanScore);
    }

    // tasks.md 2.9: the new time-ranking fields, exercised through the real
    // RecapService (not the recording stub) so eligibility gating is proven
    // end to end rather than just at the builder level.
    [Fact]
    public async Task Get_AiredMultiYearRecapPopulatesTheTimeRankings()
    {
        var repository = new FakeUserAnimeEntryRepository([WatchedEntry(1, new DateOnly(2021, 4, 15), 12)]);
        var controller = new RecapController(new RecapService(repository), new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.MultiYear, 2020, 2022, null, null, RecapTimeFilter.Aired, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RecapDto>(ok.Value);
        Assert.NotEmpty(dto.SeasonTimeRanking);
        Assert.NotEmpty(dto.YearTimeRanking);
    }

    [Fact]
    public async Task Get_SeasonRecapLeavesTheTimeRankingsEmpty()
    {
        var repository = new FakeUserAnimeEntryRepository([WatchedEntry(1, new DateOnly(2021, 4, 15), 12)]);
        var controller = new RecapController(new RecapService(repository), new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.Season, null, null, 2021, "spring", RecapTimeFilter.Aired, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RecapDto>(ok.Value);
        Assert.Empty(dto.SeasonTimeRanking);
        Assert.Empty(dto.YearTimeRanking);
    }

    [Fact]
    public async Task Get_WatchedFilterLeavesTheTimeRankingsEmpty()
    {
        var repository = new FakeUserAnimeEntryRepository([WatchedEntry(1, new DateOnly(2021, 4, 15), 12)]);
        var controller = new RecapController(new RecapService(repository), new RecordingAvailabilityService());

        var result = await controller.Get(RecapMode.MultiYear, 2020, 2022, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RecapDto>(ok.Value);
        Assert.Empty(dto.SeasonTimeRanking);
        Assert.Empty(dto.YearTimeRanking);
    }

    private static UserAnimeEntry WatchedEntry(int animeId, DateOnly airedFrom, int episodesWatched) => new()
    {
        AnimeId = animeId,
        Anime = new AnimeMetadata
        {
            Id = animeId,
            Title = $"Anime {animeId}",
            AiredFrom = airedFrom,
            AverageEpisodeDurationSeconds = 1400,
        },
        EpisodesWatched = episodesWatched,
        Status = WatchStatus.Completed,
    };

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            Task.FromResult(entries.FirstOrDefault(e => e.AnimeId == animeId));

        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);

        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            Task.FromResult((0, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task GetAvailability_ReturnsTheServiceResult()
    {
        var availabilityService = new RecordingAvailabilityService();
        var controller = new RecapController(new RecordingRecapService(), availabilityService);

        var result = await controller.GetAvailability(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<RecapAvailabilityDto>(ok.Value);
        Assert.Equal(1, availabilityService.CallCount);
    }

    private sealed class RecordingRecapService : IRecapService
    {
        public List<(RecapPeriod Period, string Filter)> Requests { get; } = [];

        public Task<RecapDto> GetRecapAsync(RecapPeriod period, string filter, CancellationToken ct = default)
        {
            Requests.Add((period, filter));
            return Task.FromResult(EmptyRecap(period, filter));
        }
    }

    private sealed class RecordingAvailabilityService : IRecapAvailabilityService
    {
        public int CallCount { get; private set; }

        public Task<RecapAvailabilityDto> GetAvailabilityAsync(CancellationToken ct = default)
        {
            CallCount++;
            return Task.FromResult(new RecapAvailabilityDto([], []));
        }
    }
}
