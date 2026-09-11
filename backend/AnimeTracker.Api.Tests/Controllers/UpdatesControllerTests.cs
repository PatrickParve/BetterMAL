using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Updates;
using Microsoft.AspNetCore.Mvc;

namespace AnimeTracker.Api.Tests.Controllers;

// UpdatesController.MarkSeen (store-seen-updates-on-server design.md D4): a
// thin body-validating wrapper over IAnimeUpdateService.MarkSeenAsync, in the
// style of ListBackupControllerTests's recording fake.
public class UpdatesControllerTests
{
    private sealed class RecordingAnimeUpdateService : IAnimeUpdateService
    {
        public IReadOnlyCollection<long>? ReceivedIds { get; private set; }
        public bool Called { get; private set; }

        public Task<List<AnimeUpdateDto>> GetRecentAsync(CancellationToken ct = default) => Task.FromResult(new List<AnimeUpdateDto>());
        public Task<List<AnimeUpdateDto>> GetRecentAsync(DateTimeOffset since, CancellationToken ct = default) => Task.FromResult(new List<AnimeUpdateDto>());
        public Task<List<AnimeUpdateDto>> GetHistoryAsync(CancellationToken ct = default) => Task.FromResult(new List<AnimeUpdateDto>());

        public Task MarkSeenAsync(IReadOnlyCollection<long> ids, CancellationToken ct = default)
        {
            Called = true;
            ReceivedIds = ids;
            return Task.CompletedTask;
        }
    }

    private static UpdatesController CreateController(RecordingAnimeUpdateService service) => new(service);

    [Fact]
    public async Task ABatchReturnsNoContentAndForwardsTheIds()
    {
        var service = new RecordingAnimeUpdateService();
        var controller = CreateController(service);

        var result = await controller.MarkSeen(new MarkUpdatesSeenRequest([1, 2, 3]), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(service.Called);
        Assert.Equal([1L, 2L, 3L], service.ReceivedIds);
    }

    [Fact]
    public async Task NullIdsReturns400()
    {
        var service = new RecordingAnimeUpdateService();
        var controller = CreateController(service);

        var result = await controller.MarkSeen(new MarkUpdatesSeenRequest(null), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(service.Called);
    }

    [Fact]
    public async Task OverTheMaxBatchReturns400WithoutCallingTheService()
    {
        var service = new RecordingAnimeUpdateService();
        var controller = CreateController(service);
        var ids = Enumerable.Range(1, 501).Select(i => (long)i).ToList();

        var result = await controller.MarkSeen(new MarkUpdatesSeenRequest(ids), CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.False(service.Called);
    }

    [Fact]
    public async Task AnEmptyListReturns204()
    {
        var service = new RecordingAnimeUpdateService();
        var controller = CreateController(service);

        var result = await controller.MarkSeen(new MarkUpdatesSeenRequest([]), CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        Assert.True(service.Called);
        Assert.Empty(service.ReceivedIds!);
    }
}
