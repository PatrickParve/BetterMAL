using AnimeTracker.Api.Services.Series;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Series;

// The queue behind background series builds (search, newly found relations).
// HttpClient reports its own timeout as a TaskCanceledException while the
// stopping token is still live; the loop used to read any cancellation as a
// shutdown and exit, so one timed-out build stopped every later one until the
// next restart.
public class SeriesBuildTriggerBackgroundServiceTests
{
    [Fact]
    public async Task ATimedOutBuildDoesNotStopTheQueue()
    {
        var seriesService = new RecordingSeriesService();
        seriesService.FailFor[1] = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException());

        var services = new ServiceCollection();
        services.AddScoped<ISeriesService>(_ => seriesService);
        using var provider = services.BuildServiceProvider();

        var trigger = new SeriesBuildTrigger();
        var service = new SeriesBuildTriggerBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(), trigger, NullLogger<SeriesBuildTriggerBackgroundService>.Instance);

        await service.StartAsync(CancellationToken.None);
        try
        {
            trigger.Enqueue(1);
            trigger.Enqueue(2);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (seriesService.Attempted.Count < 2 && !timeout.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal([1, 2], seriesService.Attempted);
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    private sealed class RecordingSeriesService : ISeriesService
    {
        public Dictionary<int, Exception> FailFor { get; } = new();
        public List<int> Attempted { get; } = [];

        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default)
        {
            Attempted.Add(animeId);
            if (FailFor.TryGetValue(animeId, out var failure))
                return Task.FromException<SeriesDto>(failure);

            // A lone anime: a normal outcome the loop already swallows, and it
            // saves building a whole SeriesDto here.
            return Task.FromException<SeriesDto>(new SeriesNotFoundException(animeId));
        }

        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
