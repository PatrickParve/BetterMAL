using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// YearController (tasks.md 3.1-3.3): mirrors SeasonController's own
// parameter handling exactly — the same sort/includeMyList/hideHentai/type/
// offset/limit query parameters, the same comma-split of type, and the same
// limit clamp — plus the refresh outcome's camelCase wire format.
public class YearControllerTests
{
    private static YearPageDto EmptyPage(int year, int offset, int limit) =>
        new(year, [], offset, limit, TotalCount: 0, LastFetchedAt: null, HasListing: false);

    [Fact]
    public async Task GetPage_PassesParametersThroughToTheService()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, sort: "malScore", includeMyList: false, hideHentai: true, type: null, offset: 12, limit: 30, ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.Equal(2020, request.Year);
        Assert.Equal("malScore", request.SortKey);
        Assert.False(request.IncludeMyList);
        Assert.True(request.HideHentai);
        Assert.Null(request.Types);
        Assert.Equal(12, request.Offset);
        Assert.Equal(30, request.Limit);
    }

    [Fact]
    public async Task GetPage_SplitsTheTypeParameterOnCommas()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, type: "tv,movie,unknown", ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.Equal(["tv", "movie", "unknown"], request.Types);
    }

    [Fact]
    public async Task GetPage_BlankTypeParameterIsTreatedAsNoFilter()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, type: "  ", ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.Null(request.Types);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(500, 100)]
    [InlineData(24, 24)]
    public async Task GetPage_ClampsTheLimitToBetweenOneAndOneHundred(int requested, int expected)
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, limit: requested, ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.Equal(expected, request.Limit);
    }

    [Fact]
    public async Task GetPage_ReturnsTheServiceResult()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        var result = await controller.GetPage(2020, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<YearPageDto>(ok.Value);
        Assert.Equal(2020, dto.Year);
    }

    [Fact]
    public async Task Refresh_PassesTheYearThroughToTheService()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        var result = await controller.Refresh(2020, CancellationToken.None);

        Assert.Equal(2020, Assert.Single(service.RefreshRequests));
        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<YearRefreshResultDto>(ok.Value);
    }

    [Theory]
    [InlineData(SeasonRefreshOutcome.Fetched, "fetched")]
    [InlineData(SeasonRefreshOutcome.NotListed, "notListed")]
    [InlineData(SeasonRefreshOutcome.Skipped, "skipped")]
    [InlineData(SeasonRefreshOutcome.Failed, "failed")]
    public void YearRefreshResultDto_OutcomeSerializesInCamelCase(SeasonRefreshOutcome outcome, string expectedWireValue)
    {
        // Mirrors the JSON options ASP.NET Core's controller pipeline applies
        // (Program.cs) — camelCase property names, plus the enum's own
        // JsonStringEnumMemberName conversion (SeasonBrowseDto.cs), which the
        // year endpoint reuses unchanged.
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        options.Converters.Add(new JsonStringEnumConverter());

        var json = JsonSerializer.Serialize(new YearRefreshResultDto(outcome), options);

        Assert.Equal($$"""{"outcome":"{{expectedWireValue}}"}""", json);
    }

    private sealed record PageRequest(int Year, string SortKey, bool IncludeMyList, bool HideHentai, IReadOnlyCollection<string>? Types, int Offset, int Limit);

    private sealed class RecordingSeasonBrowseService : ISeasonBrowseService
    {
        public List<PageRequest> PageRequests { get; } = [];
        public List<int> RefreshRequests { get; } = [];

        public Task<YearPageDto> GetYearPageAsync(int year, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default)
        {
            PageRequests.Add(new PageRequest(year, sortKey, includeMyList, hideHentai, types, offset, limit));
            return Task.FromResult(EmptyPage(year, offset, limit));
        }

        public Task<YearRefreshResultDto> RefreshYearAsync(int year, CancellationToken ct = default)
        {
            RefreshRequests.Add(year);
            return Task.FromResult(new YearRefreshResultDto(SeasonRefreshOutcome.Skipped));
        }

        public Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeasonBoundsDto> GetBoundsAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
