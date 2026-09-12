using AnimeTracker.Api.Services.Jobs;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Jobs;

// BackgroundJobRunner runs the four in-request actions off the request's own
// lifetime (design.md D4): it starts a job at most once, and guarantees an
// ending — an exception fails the job with a plain reason, and work that
// returns without failing completes it.
public class BackgroundJobRunnerTests
{
    private static BackgroundJobRunner CreateRunner() => new(new FakeServiceScopeFactory());

    [Fact]
    public async Task WorkThatThrowsHttpRequestExceptionEndsFailedWithTheCouldntBeReachedReason()
    {
        var runner = CreateRunner();
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var tracker = new JobProgressTracker();

            var started = runner.TryStart(tracker, (_, _, _) => throw new HttpRequestException("boom"));

            Assert.True(started);
            await WaitForAsync(tracker, JobPhase.Failed);
            Assert.Equal("MyAnimeList couldn't be reached.", tracker.Snapshot.Error);
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task WorkThatReturnsWithoutEndingTheTrackerEndsComplete()
    {
        var runner = CreateRunner();
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var tracker = new JobProgressTracker();

            var started = runner.TryStart(tracker, (_, _, _) => Task.CompletedTask);

            Assert.True(started);
            await WaitForAsync(tracker, JobPhase.Complete);
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TryStartWhileRunningReturnsFalseAndNeverInvokesTheWork()
    {
        var runner = CreateRunner();
        await runner.StartAsync(CancellationToken.None);
        try
        {
            var tracker = new JobProgressTracker();
            var gate = new TaskCompletionSource();
            Assert.True(runner.TryStart(tracker, async (_, _, _) => await gate.Task));

            var invoked = false;
            var started = runner.TryStart(tracker, (_, _, _) =>
            {
                invoked = true;
                return Task.CompletedTask;
            });

            Assert.False(started);
            Assert.False(invoked);

            gate.SetResult();
            await WaitForAsync(tracker, JobPhase.Complete);
        }
        finally
        {
            await runner.StopAsync(CancellationToken.None);
        }
    }

    // Bounded wait so a regression fails loudly rather than hanging the run.
    private static async Task WaitForAsync(JobProgressTracker tracker, JobPhase phase)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (tracker.Snapshot.Phase != phase && !cts.IsCancellationRequested)
            await Task.Delay(10, CancellationToken.None);

        Assert.Equal(phase, tracker.Snapshot.Phase);
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope();
    }

    private sealed class FakeServiceScope : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider();
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}
