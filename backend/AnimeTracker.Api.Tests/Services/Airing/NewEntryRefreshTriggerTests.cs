using AnimeTracker.Api.Services.Airing;

namespace AnimeTracker.Api.Tests.Services.Airing;

// The queue's drain: one wake returns every id queued so far, each once, in the
// order it was first queued.
public class NewEntryRefreshTriggerTests
{
    [Fact]
    public async Task WaitingReturnsEveryQueuedIdAtOnceDistinctAndInQueueOrder()
    {
        var trigger = new NewEntryRefreshTrigger();
        foreach (var id in new[] { 5, 3, 5, 9, 3 })
            trigger.Enqueue(id);

        var drained = await trigger.WaitAsync(CancellationToken.None);

        Assert.Equal([5, 3, 9], drained);
    }

    [Fact]
    public async Task ADrainedQueueStartsEmptyAgain()
    {
        var trigger = new NewEntryRefreshTrigger();
        trigger.Enqueue(1);
        await trigger.WaitAsync(CancellationToken.None);
        trigger.Enqueue(2);

        var drained = await trigger.WaitAsync(CancellationToken.None);

        Assert.Equal([2], drained);
    }

    [Fact]
    public async Task WaitingBlocksUntilSomethingIsQueued()
    {
        var trigger = new NewEntryRefreshTrigger();

        var waiting = trigger.WaitAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.False(waiting.IsCompleted);

        trigger.Enqueue(7);

        Assert.Equal([7], await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task WaitingEndsWhenTheTokenIsCancelled()
    {
        var trigger = new NewEntryRefreshTrigger();
        using var cts = new CancellationTokenSource();

        var waiting = trigger.WaitAsync(cts.Token);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
    }
}
