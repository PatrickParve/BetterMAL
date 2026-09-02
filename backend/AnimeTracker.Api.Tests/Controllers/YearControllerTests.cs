using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Season;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// YearController (design D1, tasks.md 1.7): mirrors SeasonController's own
// parameter handling exactly — hideHentai is the only query parameter left
// once sort/includeMyList/type/offset/limit moved client-side (design D3).
public class YearControllerTests
{
    private static YearPageDto EmptyPage(int year) =>
        new(year, [], TotalCount: 0, LastFetchedAt: null, HasListing: false);

    [Fact]
    public async Task GetPage_PassesHideHentaiThroughToTheService()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, hideHentai: true, ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.Equal(2020, request.Year);
        Assert.True(request.HideHentai);
    }

    [Fact]
    public async Task GetPage_DefaultsHideHentaiToFalse()
    {
        var service = new RecordingSeasonBrowseService();
        var controller = new YearController(service);

        await controller.GetPage(2020, ct: CancellationToken.None);

        var request = Assert.Single(service.PageRequests);
        Assert.False(request.HideHentai);
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

    private sealed record PageRequest(int Year, bool HideHentai);

    private sealed class RecordingSeasonBrowseService : ISeasonBrowseService
    {
        public List<PageRequest> PageRequests { get; } = [];
        public List<int> RefreshRequests { get; } = [];

        public Task<YearPageDto> GetYearPageAsync(int year, bool hideHentai, CancellationToken ct = default)
        {
            PageRequests.Add(new PageRequest(year, hideHentai));
            return Task.FromResult(EmptyPage(year));
        }

        public Task<YearRefreshResultDto> RefreshYearAsync(int year, CancellationToken ct = default)
        {
            RefreshRequests.Add(year);
            return Task.FromResult(new YearRefreshResultDto(SeasonRefreshOutcome.Skipped));
        }

        public Task<SeasonPageDto> GetPageAsync(int year, string season, bool hideHentai, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<SeasonBoundsDto> GetBoundsAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
