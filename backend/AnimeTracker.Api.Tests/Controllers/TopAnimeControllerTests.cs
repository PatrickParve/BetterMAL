using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Library;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// TopAnimeController.Get: an unsupported `type` is rejected with 400 rather
// than silently falling back to All (design.md D4, task 2.4) — a silent
// fallback would render the All list under a highlighted, wrong button.
public class TopAnimeControllerTests
{
    [Fact]
    public async Task Get_UnsupportedTypeIsRejectedWithoutReachingTheService()
    {
        var service = new RecordingTopAnimeService();
        var controller = new TopAnimeController(service);

        var result = await controller.Get("ona", CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(service.RequestedTypes); // never called through to the service
    }

    [Fact]
    public async Task Get_OmittedTypeDefaultsToAll()
    {
        var service = new RecordingTopAnimeService();
        var controller = new TopAnimeController(service);

        var result = await controller.Get(null, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["all"], service.RequestedTypes);
    }

    [Fact]
    public async Task Get_SupportedTypeIsPassedThrough()
    {
        var service = new RecordingTopAnimeService();
        var controller = new TopAnimeController(service);

        var result = await controller.Get("movie", CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(["movie"], service.RequestedTypes);
    }

    private sealed class RecordingTopAnimeService : ITopAnimeService
    {
        public List<string> RequestedTypes { get; } = [];

        public Task<List<TopAnimeItemDto>> GetRankingAsync(string rankingType, CancellationToken ct = default)
        {
            RequestedTypes.Add(rankingType);
            return Task.FromResult(new List<TopAnimeItemDto>());
        }
    }
}
