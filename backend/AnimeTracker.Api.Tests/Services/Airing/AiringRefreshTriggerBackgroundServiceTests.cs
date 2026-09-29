using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Tests.Services.Setup;
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
            provider.GetRequiredService<IServiceScopeFactory>(), trigger, TestSetupGates.Finished(), NullLogger<AiringRefreshTriggerBackgroundService>.Instance);

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

    // add-first-run-setup design D2: setup's airing step fetches every list anime,
    // so an add-time fetch queued meanwhile waits, and runs once setup has finished.
    [Fact]
    public async Task NothingIsFetchedUntilSetupHasFinished_ThenTheQueuedFetchRuns()
    {
        var refreshService = new RecordingRefreshService();
        var services = new ServiceCollection();
        services.AddScoped<IEpisodeScheduleRefreshService>(_ => refreshService);
        using var provider = services.BuildServiceProvider();

        var trigger = new AiringRefreshTrigger();
        var gate = TestSetupGates.Unfinished();
        var service = new AiringRefreshTriggerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), trigger, gate, NullLogger<AiringRefreshTriggerBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            trigger.Enqueue(1);
            await TestSetupGates.LetItRunAsync();
            Assert.Empty(refreshService.Attempted);

            await gate.MarkFinishedAsync();
            await TestSetupGates.WaitForAsync(() => refreshService.Refreshed.Count == 1);
            Assert.Equal([1], refreshService.Refreshed);
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

        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default)
        {
            Attempted.Add(animeId);
            if (FailFor.TryGetValue(animeId, out var failure))
                return Task.FromException(failure);

            Refreshed.Add(animeId);
            return Task.CompletedTask;
        }

        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
