using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;
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
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get("decade", null, null, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_UnknownSeasonNameIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Season, null, null, 2022, "monsoon", RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_UnknownFilterIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Yearly, null, null, 2022, null, "someday", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_MissingRequiredParametersForTheModeIsRejected()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.MultiYear, null, 2020, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    // RecapRange (design D6, tasks.md 1.6): the recap's own winter-1917-to-
    // current-season range, refused the same way an unknown mode or season
    // name is — before the service is ever called.
    [Fact]
    public async Task Get_YearlyBeforeTheArchiveIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Yearly, null, null, 1916, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_YearlyPastTheCurrentYearIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Yearly, null, null, DateTime.UtcNow.Year + 2, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_YearlyBeyondTheCalendarIsRejectedRatherThanFailing()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Yearly, null, null, 99999, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_SeasonBeforeTheArchiveIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Season, null, null, 1916, "fall", RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_SeasonPastTheCurrentOneIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Season, null, null, DateTime.UtcNow.Year + 2, "winter", RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_MultiYearWithAnEndBeyondTheCalendarIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.MultiYear, 1, 9999, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_MultiYearWithANegativeEndIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.MultiYear, -5, 2020, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.Requests);
    }

    [Fact]
    public async Task Get_YearlyAtTheArchiveFloorIsAccepted()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Yearly, null, null, 1917, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task Get_SeasonAtTheArchiveFloorIsAccepted()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.Season, null, null, 1917, "winter", RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Single(service.Requests);
    }

    [Fact]
    public async Task Get_MultiYearReversedRangeReachesTheServiceInOrder()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

        var result = await controller.Get(RecapMode.MultiYear, 2024, 2020, null, null, RecapTimeFilter.Watched, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        var request = Assert.Single(service.Requests);
        Assert.Equal(2020, request.Period.StartYear);
        Assert.Equal(2024, request.Period.EndYear);
    }

    [Fact]
    public async Task Get_ValidYearlyRequestReturnsTheDto()
    {
        var service = new RecordingRecapService();
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

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
        var controller = new RecapController(service, new RecordingAvailabilityService(), new FakeBroadcastLocalTimeConverter());

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
        var controller = new RecapController(
            new RecapService(repository, new FakeActivityLogRepository(), new FakeTopAnimeSelectionRepository(), new FakeBroadcastLocalTimeConverter()),
            new RecordingAvailabilityService(),
            new FakeBroadcastLocalTimeConverter());

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
        var controller = new RecapController(
            new RecapService(repository, new FakeActivityLogRepository(), new FakeTopAnimeSelectionRepository(), new FakeBroadcastLocalTimeConverter()),
            new RecordingAvailabilityService(),
            new FakeBroadcastLocalTimeConverter());

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
        var controller = new RecapController(
            new RecapService(repository, new FakeActivityLogRepository(), new FakeTopAnimeSelectionRepository(), new FakeBroadcastLocalTimeConverter()),
            new RecordingAvailabilityService(),
            new FakeBroadcastLocalTimeConverter());

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

        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            Task.FromResult((0, 0, (DateTimeOffset?)null));
    }

    [Fact]
    public async Task GetAvailability_ReturnsTheServiceResult()
    {
        var availabilityService = new RecordingAvailabilityService();
        var controller = new RecapController(new RecordingRecapService(), availabilityService, new FakeBroadcastLocalTimeConverter());

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

    private sealed class FakeActivityLogRepository : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> editedIds, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
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
