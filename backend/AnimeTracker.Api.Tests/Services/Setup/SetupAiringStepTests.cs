using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.6 (design D10, D12; spec "Airing dates are fetched for every list
// anime, most useful first" and "A service that is down pauses its own queue"): the airing step on
// its own, against a refresh service that marks each anime it is given as fetched.
public class SetupAiringStepTests
{
    private static (SetupAiringStep Step, RetryLadder Ladder) Create(SetupRunKit kit)
    {
        var ladder = new RetryLadder();
        return (new SetupAiringStep(kit.Context, kit.AniListHealth, ladder), ladder);
    }

    private static RunningStep Start(SetupAiringStep step) => new(step.RunAsync);

    // The kit's clock is 2026-09-29, in summer 2026: fall is next season, spring is last.
    [Fact]
    public async Task TheAiringOrderFollowsTheTiers()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1);                                                                        // tier 4
        await kit.SeedAnimeAsync(2, airing: "currently_airing", status: WatchStatus.PlanToWatch);           // tier 1
        await kit.SeedAnimeAsync(3, airing: "not_yet_aired", airedFrom: new DateOnly(2026, 10, 5));         // tier 2
        await kit.SeedAnimeAsync(4, airing: "finished_airing", airedFrom: new DateOnly(2026, 5, 1));        // tier 3
        await kit.SeedAnimeAsync(5, airing: "currently_airing", status: WatchStatus.Watching);              // tier 1, watched
        kit.State.SetListOrder([1, 2, 3, 4, 5]);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([5, 2, 3, 4, 1], kit.Airing.AskedFor);
    }

    [Fact]
    public async Task TheNextTwentyFiveDueAnimeAreOneBatch()
    {
        using var kit = new SetupRunKit();
        foreach (var id in Enumerable.Range(1, 60))
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder(Enumerable.Range(1, 60).ToList());
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([25, 25, 10], kit.Airing.Batches.Select(b => b.Count));
        Assert.Equal(Enumerable.Range(1, 60), kit.Airing.AskedFor);
    }

    // Task 10.2 (design D17): a batch counts its anime, not one event. Twenty-five anime a batch and
    // thirty seconds of the kit's clock a batch, held at the third batch: 50 are done, 25 in the 30 s
    // since the first batch, so the 50 still to do take a minute.
    [Fact]
    public async Task TheEstimateCountsTheAnimeABatchFinishedNotTheBatches()
    {
        using var kit = new SetupRunKit();
        foreach (var id in Enumerable.Range(1, 100))
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder(Enumerable.Range(1, 100).ToList());
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kit.Airing.BeforeBatch = async (ids, ct) =>
        {
            kit.Now += TimeSpan.FromSeconds(30);
            if (ids[0] == 51)
                await hold.Task.WaitAsync(ct);
        };
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.Airing.Batches.Count == 3, "the third batch to start");

        Assert.Equal(60, kit.State.Snapshot().Airing.EtaSeconds);

        hold.SetResult();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Null(kit.State.Snapshot().Airing.EtaSeconds);
    }

    [Fact]
    public async Task OnlyAnimeWithNoAiringMarkAreFetched()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, marked: true);
        await kit.SeedAnimeAsync(2);
        await kit.SeedAnimeAsync(3, marked: true);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([2], kit.Airing.AskedFor);
    }

    [Fact]
    public async Task AnAnimeThatFailsGoesOnTheLadderAndTheRestCarryOn()
    {
        using var kit = new SetupRunKit();
        foreach (var id in new[] { 1, 2, 3 })
            await kit.SeedAnimeAsync(id);
        kit.Airing.Failing.Add(2);
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(2), "the failure to be recorded");

        await using (var db = kit.NewDb())
            Assert.Equal([1, 3], (await db.AnimeAiringSyncs.Select(s => s.AnimeId).ToListAsync()).Order());
        Assert.False(running.IsCompleted);
        Assert.Equal(1, kit.State.Snapshot().Airing.WaitingRetry?.Count);

        kit.Airing.Failing.Clear();
        ladder.MakeAllDueNow(kit.Now);
        kit.Wake.Pulse();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([1, 2, 3], kit.Airing.Batches[0].Concat(kit.Airing.Batches[1]).Order().Distinct());
        Assert.False(ladder.HasFailed(2));
    }

    [Fact]
    public async Task ADownServicePausesItAndOneAttemptWithOneAnimeResumesIt()
    {
        using var kit = new SetupRunKit();
        foreach (var id in new[] { 1, 2, 3 })
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder([1, 2, 3]);
        kit.Airing.FailWholeBatches = ServiceHealth.DownAfterFailures; // three failed requests in a row
        var (step, ladder) = Create(kit);

        void RetryNow()
        {
            ladder.MakeAllDueNow(kit.Now);
            kit.AniListHealth.MakeDueNow();
            kit.Wake.Pulse();
        }

        await using var running = Start(step);
        for (var strike = 1; strike <= ServiceHealth.DownAfterFailures; strike++)
        {
            var batches = strike;
            await SetupRunKit.WaitUntilAsync(() => kit.Airing.Batches.Count == batches, $"batch {batches}");
            await SetupRunKit.WaitUntilAsync(() => kit.AniListHealth.ConsecutiveFailures == batches, $"strike {batches}");
            if (strike < ServiceHealth.DownAfterFailures)
            {
                await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(3), "the batch's anime to wait");
                RetryNow();
            }
        }

        // Three failed requests: the service appears down and the queue takes nothing.
        await SetupRunKit.WaitUntilAsync(() => kit.AniListHealth.IsDown, "AniList to appear down");
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Airing.Phase == SetupStepPhase.Paused, "the queue to pause");
        await SetupRunKit.Settle();
        Assert.Equal(3, kit.Airing.Batches.Count);

        // Retry now: its ladder is due, and it makes one attempt with the next anime in order.
        RetryNow();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([[1, 2, 3], [1, 2, 3], [1, 2, 3], [1], [2, 3]], kit.Airing.Batches);
        Assert.False(kit.AniListHealth.IsDown); // the attempt that got through reset it
    }
}
