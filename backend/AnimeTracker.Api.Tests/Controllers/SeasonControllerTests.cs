using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// SeasonController (design D7, tasks.md 8.3): the season-name check runs
// before the range check, so a refused request never reaches
// GetPageAsync/RefreshAsync/GetRequestRangeAsync as appropriate — this fake
// records each call so a test can tell "refused" and "passed through" apart
// by what actually ran, not just by the HTTP status.
public class SeasonControllerTests
{
    private static SeasonPageDto EmptyPage(int year, string season) =>
        new(year, season, [], TotalCount: 0, LastFetchedAt: null, HasListing: false);

    [Theory]
    [InlineData(9999, "winter")]
    [InlineData(1800, "winter")]
    [InlineData(1916, "fall")]
    [InlineData(2027, "spring")]
    public async Task GetPage_RefusesASeasonOutsideTheRange(int year, string season)
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.GetPage(year, season, ct: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.PageRequests);
    }

    [Theory]
    [InlineData(9999, "winter")]
    [InlineData(1800, "winter")]
    [InlineData(1916, "fall")]
    [InlineData(2027, "spring")]
    public async Task Refresh_RefusesASeasonOutsideTheRange(int year, string season)
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.Refresh(year, season, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.RefreshRequests);
    }

    [Theory]
    [InlineData(2027, "winter")]
    [InlineData(1917, "winter")]
    [InlineData(1988, "summer")]
    public async Task GetPage_AcceptsASeasonInsideTheRangeAndPassesItThrough(int year, string season)
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.GetPage(year, season, ct: CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal((year, season), Assert.Single(service.PageRequests));
    }

    [Theory]
    [InlineData(2027, "winter")]
    [InlineData(1917, "winter")]
    [InlineData(1988, "summer")]
    public async Task Refresh_AcceptsASeasonInsideTheRangeAndPassesItThrough(int year, string season)
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.Refresh(year, season, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal((year, season), Assert.Single(service.RefreshRequests));
    }

    [Fact]
    public async Task GetPage_UnknownSeasonNameIsRefusedBeforeTheRangeIsEvenAsked()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.GetPage(9999, "autumn", ct: CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.RangeCallCount);
    }

    [Fact]
    public async Task Refresh_UnknownSeasonNameIsRefusedBeforeTheRangeIsEvenAsked()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new SeasonController(service);

        var result = await controller.Refresh(9999, "autumn", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(0, service.RangeCallCount);
    }

    private sealed class RecordingSeasonBrowseService : ISeasonBrowseService
    {
        public List<(int Year, string Season)> PageRequests { get; } = [];
        public List<(int Year, string Season)> RefreshRequests { get; } = [];
        public int RangeCallCount { get; private set; }

        public Task<SeasonRequestRange> GetRequestRangeAsync(CancellationToken ct = default)
        {
            RangeCallCount++;
            return Task.FromResult(new SeasonRequestRange(1917, 2027, "winter"));
        }

        public Task<SeasonPageDto> GetPageAsync(int year, string season, bool hideHentai, CancellationToken ct = default)
        {
            PageRequests.Add((year, season));
            return Task.FromResult(EmptyPage(year, season));
        }

        public Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default)
        {
            RefreshRequests.Add((year, season));
            return Task.FromResult(new SeasonRefreshResultDto(SeasonRefreshOutcome.Skipped));
        }

        public Task<SeasonBoundsDto> GetBoundsAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<YearPageDto> GetYearPageAsync(int year, bool hideHentai, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<YearRefreshResultDto> RefreshYearAsync(int year, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
