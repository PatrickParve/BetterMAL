using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.5 (design D9; spec "Building series covers every list anime"): the
// series step on its own, against a series service that stores what a real build would.
public class SetupSeriesStepTests
{
    private static (SetupSeriesStep Step, RetryLadder Ladder) Create(SetupRunKit kit)
    {
        var ladder = new RetryLadder();
        return (new SetupSeriesStep(kit.Context, kit.MalHealth, ladder), ladder);
    }

    private static RunningStep Start(SetupSeriesStep step) => new(step.RunAsync);

    private static async Task SeedFetchedAsync(SetupRunKit kit, params int[] ids)
    {
        foreach (var id in ids)
            await kit.SeedAnimeAsync(id, fetched: true);
    }

    [Fact]
    public async Task AFranchiseIsBuiltOnceAndEveryListAnimeItCoversCountsAsSettled()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2, 3, 4);
        kit.Series.Franchises[1] = [1, 2, 3]; // the first season's build stores all three list anime
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([1, 4], kit.Series.BuildCalls); // 2 and 3 were covered by then, so they were not built
        await using var db = kit.NewDb();
        var status = await kit.NewStatusService(db, new FakeSetupCoordinator { Snapshot = kit.State.Snapshot() }).GetAsync();
        Assert.Equal(4, status.Steps.Series.Done); // three settled by one build, one by its own
        Assert.Equal(4, status.Steps.Series.Total);
    }

    [Fact]
    public async Task ALoneAnimeIsRememberedAsSettledAndNotBuiltAgain()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2);
        kit.Series.Lone.Add(2);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([1, 2], kit.Series.BuildCalls); // one build each, and the loop ended
        Assert.Contains(2, kit.State.Snapshot().NoSeriesAnimeIds);
        await using var db = kit.NewDb();
        Assert.Empty(db.SeriesMembers.Where(m => m.AnimeId == 2));
        var status = await kit.NewStatusService(db, new FakeSetupCoordinator { Snapshot = kit.State.Snapshot() }).GetAsync();
        Assert.Equal(2, status.Steps.Series.Done);
    }

    [Fact]
    public async Task APartialSeedGoesOnTheLadderAndIsBuiltAgainWhenItsWaitEnds()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2);
        kit.State.SetListOrder([1, 2]);
        kit.Series.Script(1, SetupBuildOutcome.Partial, SetupBuildOutcome.Settled);
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.Series.BuildCalls.Contains(2), "anime 2's build");
        await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(1), "the partial build to be recorded");

        Assert.Equal([1, 2], kit.Series.BuildCalls); // carried on with 2; 1 is waiting its minute
        Assert.False(running.IsCompleted);
        Assert.Equal(1, kit.State.Snapshot().Series.WaitingRetry?.Count);

        ladder.MakeAllDueNow(kit.Now);
        kit.Wake.Pulse();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([1, 2, 1], kit.Series.BuildCalls);
        Assert.False(ladder.HasFailed(1));
    }

    [Fact]
    public async Task AFailedBuildIsRetriedAndTheStepCarriesOnWithTheRest()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2, 3);
        kit.Series.Fail(2, Failures.Timeout());
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(2) && kit.Series.BuildCalls.Contains(3), "3 to be built past 2's failure");

        Assert.False(running.IsCompleted);
        ladder.MakeAllDueNow(kit.Now);
        kit.Wake.Pulse();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([1, 2, 3, 2], kit.Series.BuildCalls);
    }

    [Fact]
    public async Task AnimeSkippedForGoodAndAnimeAlreadyCoveredAreNotBuilt()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2);
        await kit.SeedAnimeAsync(3, notOnMal: true); // MyAnimeList has no such anime
        await kit.SeedAnimeAsync(4, fetched: true);
        await kit.SeedSeriesAsync(40, 4);             // up to date and not partial already
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([1, 2], kit.Series.BuildCalls);
    }

    [Fact]
    public async Task AnAnimeInAPartialOrOutOfDateSeriesIsBuiltAgain()
    {
        using var kit = new SetupRunKit();
        await SeedFetchedAsync(kit, 1, 2);
        await using (var db = kit.NewDb())
        {
            db.Series.Add(new Models.Series { Id = 10, BuiltAt = DateTimeOffset.UtcNow, IsPartial = true });
            db.Series.Add(new Models.Series { Id = 20, BuiltAt = SeriesGraphBuilder.ClassificationRevisedAt.AddDays(-1) });
            db.SeriesMembers.Add(new SeriesMember { SeriesId = 10, AnimeId = 1, IsPrimary = true });
            db.SeriesMembers.Add(new SeriesMember { SeriesId = 20, AnimeId = 2, IsPrimary = true });
            await db.SaveChangesAsync();
        }
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([1, 2], kit.Series.BuildCalls.Order());
    }

    [Fact]
    public async Task TheOrderIsTheDetailsOrder()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, fetched: true);
        await kit.SeedAnimeAsync(2, WatchStatus.Watching, fetched: true);
        await kit.SeedAnimeAsync(3, fetched: true, airing: "currently_airing");
        kit.State.SetListOrder([1, 3, 2]);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([2, 3, 1], kit.Series.BuildCalls);
    }
}
