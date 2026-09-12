using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Jobs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// The settings page's manual "refresh all airing data" run (design.md D3): a
// refresh that throws leaves the tracker Failed with a plain AniList reason
// rather than only logging, which is what AiringFullRefreshProgressTracker
// used to do before it lost its own Failed phase.
public class AiringFullRefreshBackgroundServiceTests
{
    [Fact]
    public async Task ARefreshThatThrowsLeavesTheTrackerFailedWithTheAniListReason()
    {
        var trigger = new AiringFullRefreshTrigger();
        var progress = new AiringFullRefreshProgress();
        var refreshService = new ThrowingEpisodeScheduleRefreshService();
        var service = new AiringFullRefreshBackgroundService(
            new FakeServiceScopeFactory(new FakeServiceProvider(refreshService)),
            trigger,
            progress,
            NullLogger<AiringFullRefreshBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            progress.TryBegin(); // mirrors AiringController.TriggerFullRefresh (design.md D2): the tracker is the start gate
            trigger.Signal();

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (progress.Snapshot.Phase != JobPhase.Failed && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(JobPhase.Failed, progress.Snapshot.Phase); // fail loudly on a test timeout, not a hang
            Assert.Equal("AniList couldn't be reached.", progress.Snapshot.Error);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class ThrowingEpisodeScheduleRefreshService : IEpisodeScheduleRefreshService
    {
        public Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default) =>
            throw new HttpRequestException("AniList is unreachable");

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<int> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null) =>
            throw new NotImplementedException();
        public Task BackfillAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeServiceProvider(IEpisodeScheduleRefreshService refreshService) : IServiceProvider
    {
        public object? GetService(Type serviceType) =>
            serviceType == typeof(IEpisodeScheduleRefreshService) ? refreshService : null;
    }

    private sealed class FakeServiceScopeFactory(IServiceProvider provider) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope(provider);
    }

    private sealed class FakeServiceScope(IServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() { }
    }
}
