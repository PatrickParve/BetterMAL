using AnimeTracker.Api.Services.Airing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// The queue behind an anime's add-time airing fetch. HttpClient reports its own
// timeout as a TaskCanceledException while the stopping token is still live; the
// loop used to read any cancellation as a shutdown and exit, so one AniList
// timeout stopped every later add-time fetch until the next restart.
public class AiringRefreshTriggerBackgroundServiceTests
{
    [Fact]
    public async Task ATimedOutRefreshDoesNotStopTheQueue()
    {
        var refreshService = new RecordingRefreshService();
        refreshService.FailFor[1] = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException());

        var services = new ServiceCollection();
        services.AddScoped<IEpisodeScheduleRefreshService>(_ => refreshService);
        using var provider = services.BuildServiceProvider();

        var trigger = new AiringRefreshTrigger();
        var service = new AiringRefreshTriggerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), trigger, NullLogger<AiringRefreshTriggerBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            trigger.Enqueue(1);
            trigger.Enqueue(2);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!refreshService.Refreshed.Contains(2) && !timeout.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal([1, 2], refreshService.Attempted);
            Assert.Equal([2], refreshService.Refreshed);
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class RecordingRefreshService : IEpisodeScheduleRefreshService
    {
        public Dictionary<int, Exception> FailFor { get; } = new();
        public List<int> Attempted { get; } = [];
        public List<int> Refreshed { get; } = [];

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            Attempted.Add(animeId);
            if (FailFor.TryGetValue(animeId, out var failure))
                return Task.FromException(failure);

            Refreshed.Add(animeId);
            return Task.CompletedTask;
        }

        public Task<RefreshManyResult> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null) =>
            throw new NotImplementedException();
        public Task BackfillAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }
}
