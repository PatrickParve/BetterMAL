using AnimeTracker.Api.Controllers;
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
            Stats: new RecapStatsDto(null, 0, 0, 0, 0, 0, []),
            Items: [], SeasonRanking: [], YearRanking: []);

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
