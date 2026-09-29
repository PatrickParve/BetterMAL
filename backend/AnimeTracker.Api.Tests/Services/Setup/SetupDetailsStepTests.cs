using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Setup;

// add-first-run-setup task 8.4 (design D8; spec "Fetching anime details fills in every list
// anime"): the details step on its own, against a scripted MyAnimeList and a database the test
// seeds.
public class SetupDetailsStepTests
{
    private static (SetupDetailsStep Step, RetryLadder Ladder) Create(SetupRunKit kit)
    {
        var ladder = new RetryLadder();
        return (new SetupDetailsStep(kit.Context, kit.MalHealth, ladder), ladder);
    }

    private static RunningStep Start(SetupDetailsStep step) => new(step.RunAsync);

    private static async Task<List<int>> FetchedAsync(SetupRunKit kit)
    {
        await using var db = kit.NewDb();
        return await db.AnimeMetadata.AsNoTracking().Where(a => a.LastSyncedAt != default).Select(a => a.Id).OrderBy(id => id).ToListAsync();
    }

    [Fact]
    public async Task TheOrderIsWatchingThenAiringThenTheListsOwnOrder()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1);                                                 // other
        await kit.SeedAnimeAsync(2, airing: "currently_airing", status: WatchStatus.PlanToWatch);
        await kit.SeedAnimeAsync(3, WatchStatus.Watching);
        await kit.SeedAnimeAsync(4);                                                 // other
        await kit.SeedAnimeAsync(5, WatchStatus.Rewatching);
        await kit.SeedAnimeAsync(6, airing: "currently_airing");
        kit.State.SetListOrder([4, 1, 6, 2, 5, 3]); // the read returned them in this order
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // Watching and rewatching (in list order: 5 then 3), then airing (6 then 2), then the rest (4 then 1).
        Assert.Equal([5, 3, 6, 2, 4, 1], kit.Mal.DetailsCalls);
    }

    [Fact]
    public async Task EachAnimeIsSavedWholeWithItsRelationsAndItsPictureSet()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, title: "Basic 1");
        kit.Mal.Details[1] = new MalAnimeNode
        {
            Id = 1,
            Title = "Full 1",
            Status = "finished_airing",
            NumEpisodes = 24,
            Background = "Background text.",
            RelatedAnime =
            [
                new MalRelatedAnimeEdge { RelationType = "sequel", Node = new MalAnimeNode { Id = 2, Title = "Sequel" } },
                new MalRelatedAnimeEdge { RelationType = "side_story", Node = new MalAnimeNode { Id = 3, Title = "Side" } },
            ],
            Pictures = [new MalMainPicture { Large = "https://img/1.jpg" }, new MalMainPicture { Large = "https://img/2.jpg" }],
        };
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        var anime = await db.AnimeMetadata.Include(a => a.RelatedAnime).AsNoTracking().SingleAsync();
        Assert.Equal("Full 1", anime.Title);
        Assert.NotEqual(default, anime.LastSyncedAt);
        Assert.Equal(24, anime.TotalEpisodes);
        Assert.Equal("Background text.", anime.Background);
        Assert.Equal(["sequel", "side_story"], anime.RelatedAnime.OrderBy(r => r.SortOrder).Select(r => r.RelationType));
        Assert.Equal(["https://img/1.jpg", "https://img/2.jpg"], anime.PictureUrls);
        Assert.NotNull(anime.PicturesSyncedAt);
        Assert.Equal(1, kit.SearchIndex.Invalidations);
        Assert.False(ladder.HasFailed(1));
    }

    [Fact]
    public async Task NoUpdateDiscoveryOrSeriesBuildIsMadeWhileFetchingDetails()
    {
        using var kit = new SetupRunKit();
        foreach (var id in new[] { 1, 2, 3 })
        {
            await kit.SeedAnimeAsync(id);
            kit.Mal.Details[id] = new MalAnimeNode
            {
                Id = id,
                Title = $"Full {id}",
                Status = "currently_airing",
                NumEpisodes = 12,
                RelatedAnime = [new MalRelatedAnimeEdge { RelationType = "sequel", Node = new MalAnimeNode { Id = id + 100, Title = "Sequel" } }],
            };
        }

        var (step, _) = Create(kit);
        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
        // The kit registers no series-build trigger, so a step that enqueued one would have thrown
        // for each anime and left it unfetched.
        Assert.Equal([1, 2, 3], await FetchedAsync(kit));
        Assert.Equal(3, await db.AnimeRelatedAnime.CountAsync());
    }

    // Every fetch takes three seconds of the kit's clock, and the fetch of a listed id blocks until
    // the test lets it go, so a test looks at what the step has published at a chosen moment.
    private static Dictionary<int, TaskCompletionSource> ThreeSecondFetches(SetupRunKit kit, params int[] blockedIds)
    {
        var gates = blockedIds.ToDictionary(id => id, _ => new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        kit.Mal.BeforeDetails = async (id, ct) =>
        {
            kit.Now += TimeSpan.FromSeconds(3);
            if (gates.TryGetValue(id, out var gate))
                await gate.Task.WaitAsync(ct);
        };
        return gates;
    }

    // Task 10.2 (design D17): the estimate is the step's recent pace applied to what is left.
    [Fact]
    public async Task TheStepPublishesAnEstimateFromItsRecentPace_AndNoneOnceItIsDone()
    {
        using var kit = new SetupRunKit();
        foreach (var id in Enumerable.Range(1, 12))
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder(Enumerable.Range(1, 12).ToList());
        var gates = ThreeSecondFetches(kit, blockedIds: 9);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Count == 9, "the ninth fetch to start");

        // Eight anime are done, one every three seconds: the first is the starting line, so seven in
        // 21 s is a third of an anime a second, and the four still to do take twelve seconds.
        var snapshot = kit.State.Snapshot().Details;
        Assert.Equal(SetupStepPhase.Running, snapshot.Phase);
        Assert.Equal(12, snapshot.EtaSeconds);

        gates[9].SetResult();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(SetupStepPhase.Done, kit.State.Snapshot().Details.Phase);
        Assert.Null(kit.State.Snapshot().Details.EtaSeconds);
    }

    [Fact]
    public async Task NoEstimateIsPublishedBeforeFiveCompletions()
    {
        using var kit = new SetupRunKit();
        foreach (var id in Enumerable.Range(1, 12))
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder(Enumerable.Range(1, 12).ToList());
        var gates = ThreeSecondFetches(kit, blockedIds: 5);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Count == 5, "the fifth fetch to start");

        Assert.Null(kit.State.Snapshot().Details.EtaSeconds); // four done, over nine seconds: neither bar is met

        gates[5].SetResult();
    }

    [Fact]
    public async Task APausedQueueHasNoEstimate_AndAfterItResumesTheWaitIsNotAveragedIntoThePace()
    {
        using var kit = new SetupRunKit();
        foreach (var id in Enumerable.Range(1, 12))
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder(Enumerable.Range(1, 12).ToList());
        foreach (var id in new[] { 7, 8, 9 })
            kit.Mal.FailDetails(id, Failures.ServerError()); // three in a row: MyAnimeList appears down
        var gates = ThreeSecondFetches(kit, blockedIds: 10);
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().Details.Phase == SetupStepPhase.Paused, "the queue to pause");

        // Six anime were done at a steady pace before it stopped, and that pace says nothing about
        // how long the outage lasts.
        Assert.Null(kit.State.Snapshot().Details.EtaSeconds);

        // An hour later MyAnimeList is back. If the hour were in the pace, the estimate would be
        // absurd for the next thirty anime.
        kit.Now += TimeSpan.FromHours(1);
        kit.MalHealth.MakeDueNow();
        ladder.MakeAllDueNow(kit.Now);
        kit.Wake.Pulse();
        // Nine calls so far (six done, three failed); 7, 8 and 9 are tried again, then the tenth anime is
        // asked for, and its fetch is the one held.
        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Count == 13, "the queue to reach the tenth anime");

        // 7, 8 and 9 finished since the restart: three completions, under the five it needs.
        Assert.Null(kit.State.Snapshot().Details.EtaSeconds);

        gates[10].SetResult();
    }

    [Fact]
    public async Task A404IsPermanentTheStepNeverRetriesItAndItDoesNotHoldTheStepBack()
    {
        using var kit = new SetupRunKit();
        foreach (var id in new[] { 1, 2, 3 })
            await kit.SeedAnimeAsync(id);
        kit.Mal.FailDetails(2, Failures.NotFound());
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10)); // finished: nothing is waiting

        Assert.Equal([1, 2, 3], kit.Mal.DetailsCalls); // asked once each
        await using var db = kit.NewDb();
        var skipped = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 2);
        Assert.NotNull(skipped.LastRefreshFailedAt);
        Assert.Equal(default, skipped.LastSyncedAt);
        Assert.False(ladder.HasFailed(2));
        Assert.Equal([1, 3], await FetchedAsync(kit));

        // It counts as done for the step (design D17) and is what Settings lists.
        var status = await kit.NewStatusService(db, new FakeSetupCoordinator { Snapshot = kit.State.Snapshot() }).GetAsync();
        Assert.Equal(3, status.Steps.Details.Done);
        Assert.Equal(new SetupSkippedDto(2, "Anime 2", SetupSkipReason.NotOnMal, null), Assert.Single(status.Skipped));
    }

    [Fact]
    public async Task ATimeoutIsRetriedKeepsItsPlaceAndIsTakenNextWhenItIsDue()
    {
        using var kit = new SetupRunKit();
        foreach (var id in new[] { 1, 2, 3, 4 })
            await kit.SeedAnimeAsync(id);
        kit.State.SetListOrder([1, 2, 3, 4]);
        kit.Mal.FailDetails(2, Failures.Timeout());
        var (step, ladder) = Create(kit);

        // Anime 3 is held mid-fetch, so 2's retry falls due while 4 is still ahead of it in the queue.
        var holdThree = new TaskCompletionSource();
        kit.Mal.BeforeDetails = (id, ct) => id == 3 && kit.Mal.DetailsCalls.Count(c => c == 3) == 1 ? holdThree.Task.WaitAsync(ct) : Task.CompletedTask;

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => kit.Mal.DetailsCalls.Contains(3), "anime 3 in flight");

        // 1 was fetched, 2 timed out and waits, 3 is in flight, 4 is next in the order.
        Assert.Equal([1, 2, 3], kit.Mal.DetailsCalls);
        Assert.True(ladder.IsWaiting(2, kit.Now));
        Assert.Equal(1, kit.State.Snapshot().Details.WaitingRetry?.Count);

        ladder.MakeAllDueNow(kit.Now); // 2's minute is up
        holdThree.SetResult();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        // 2 comes back before 4, though it failed after 1 and before 3: its own place, not the end.
        Assert.Equal([1, 2, 3, 2, 4], kit.Mal.DetailsCalls);
        Assert.Equal([1, 2, 3, 4], await FetchedAsync(kit));
        Assert.False(ladder.HasFailed(2));
    }

    [Fact]
    public async Task ATimeoutIsATemporaryFailureOfThatAnimeNeverTheAppShuttingDown()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1);
        await kit.SeedAnimeAsync(2);
        kit.Mal.FailDetails(1, Failures.Timeout());
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(1), "the timeout to be recorded");
        await SetupRunKit.WaitUntilAsync(async () => (await FetchedAsync(kit)).Contains(2), "anime 2 to carry on");

        Assert.False(running.IsCompleted); // still going: one anime is waiting for its retry
        Assert.Equal(SetupStepPhase.Running, kit.State.Snapshot().Details.Phase);
    }

    [Fact]
    public async Task AnUnexpectedExceptionIsRetriedNotDropped()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1);
        await kit.SeedAnimeAsync(2);
        kit.Mal.FailDetails(1, new InvalidOperationException("something nobody planned for"));
        var (step, ladder) = Create(kit);

        await using var running = Start(step);
        await SetupRunKit.WaitUntilAsync(() => ladder.HasFailed(1), "the failure to be recorded");

        Assert.False(running.IsCompleted); // holding on to anime 1, since dropping it would hold Home forever
        ladder.MakeAllDueNow(kit.Now);
        kit.Wake.Pulse();
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal([1, 2], await FetchedAsync(kit));
    }

    [Fact]
    public async Task ARestartCarriesOnWithTheAnimeNotYetSavedAndFetchesNoneTwice()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, fetched: true);
        await kit.SeedAnimeAsync(2, fetched: true);
        await kit.SeedAnimeAsync(3);
        await kit.SeedAnimeAsync(4);
        var (step, _) = Create(kit);

        await using var running = Start(step);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal([3, 4], kit.Mal.DetailsCalls);
    }
}
