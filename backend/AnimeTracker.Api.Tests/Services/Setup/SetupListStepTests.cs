using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Setup;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Setup;

/// <summary>Runs one setup step in the background and stops it afterwards.</summary>
internal sealed class RunningStep : IAsyncDisposable
{
    private readonly CancellationTokenSource _cts = new();

    public RunningStep(Func<CancellationToken, Task> run) => Task = run(_cts.Token);

    public Task Task { get; }

    public bool IsCompleted => Task.IsCompleted;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        try
        {
            await Task;
        }
        catch (OperationCanceledException)
        {
        }

        _cts.Dispose();
    }
}

// add-first-run-setup task 8.3 (design D7; spec "Reading your list stores every entry and a
// basic anime row"): the list step, on its own, against a scripted MyAnimeList.
public class SetupListStepTests
{
    private static (SetupListStep Step, TaskCompletionSource Stored, TaskCompletionSource Read, RetryLadder Ladder) Create(SetupRunKit kit)
    {
        var ladder = new RetryLadder();
        return (
            new SetupListStep(kit.Context, kit.Trigger, kit.MalHealth, ladder),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            ladder);
    }

    private static RunningStep Start(SetupListStep step, TaskCompletionSource stored, TaskCompletionSource read) =>
        new(ct => step.RunAsync(stored, read, ct));

    [Fact]
    public async Task EveryRecognizedEntryIsStoredWithABasicRowBuiltFromTheListResponse()
    {
        using var kit = new SetupRunKit();
        kit.Mal.Edges.AddRange([SetupRunKit.Edge(1, "watching", "currently_airing"), SetupRunKit.Edge(2, "completed"), SetupRunKit.Edge(3, "plan_to_watch")]);
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        var rows = await db.AnimeMetadata.AsNoTracking().OrderBy(a => a.Id).ToListAsync();
        Assert.Equal([1, 2, 3], rows.Select(r => r.Id));
        Assert.All(rows, r =>
        {
            Assert.Equal(default, r.LastSyncedAt); // still counts as never fully fetched
            Assert.Equal(["Action"], r.Genres);
            Assert.Equal("From the list.", r.Synopsis);
            Assert.Equal("manga", r.Source);
        });
        Assert.Equal("Basic 1", rows[0].Title);
        Assert.Equal("currently_airing", rows[0].AiringStatus);

        var entries = await db.UserAnimeEntries.AsNoTracking().OrderBy(e => e.AnimeId).ToListAsync();
        Assert.Equal([WatchStatus.Watching, WatchStatus.Completed, WatchStatus.PlanToWatch], entries.Select(e => e.Status));
        Assert.All(entries, e =>
        {
            Assert.Equal(8, e.MyScore);
            Assert.Equal(3, e.EpisodesWatched);
            Assert.False(e.PendingSync);
        });
    }

    [Fact]
    public async Task ThePagesAreStoredOneAtATimeAndTheSearchIndexIsToldAfterEach()
    {
        using var kit = new SetupRunKit();
        kit.Mal.PageSize = 2;
        kit.Mal.Edges.AddRange(Enumerable.Range(1, 5).Select(id => SetupRunKit.Edge(id)));
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(3, kit.SearchIndex.Invalidations); // three pages: 2 + 2 + 1
        var snapshot = kit.State.Snapshot();
        Assert.Equal(5, snapshot.ListRead);
        Assert.True(snapshot.ListReadThisRun);
    }

    // Task 10.2 (design D17). One entry a page and three seconds of the kit's clock a page, the
    // read blocked at the ninth page: eight entries are read, the first is the starting line, and
    // seven in 21 s leave the four still to read twelve seconds.
    [Fact]
    public async Task TheReadPublishesAnEstimateOnceItsTotalIsKnown()
    {
        using var kit = new SetupRunKit();
        kit.Mal.PageSize = 1;
        kit.Mal.Count = 12;
        kit.Mal.Edges.AddRange(Enumerable.Range(1, 12).Select(id => SetupRunKit.Edge(id)));
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kit.Mal.BeforePage = async (page, ct) =>
        {
            kit.Now += TimeSpan.FromSeconds(3);
            if (page == 9)
                await hold.Task.WaitAsync(ct);
        };
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().ListRead == 8, "eight entries to be read");

        Assert.Equal(12, kit.State.Snapshot().List.EtaSeconds);

        hold.SetResult();
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task WithNoTotalTheReadHasNoEstimate()
    {
        using var kit = new SetupRunKit();
        kit.Mal.PageSize = 1;
        kit.Mal.Count = null; // MyAnimeList's list pages carry no total, and the statistics call gave none
        kit.Mal.Edges.AddRange(Enumerable.Range(1, 12).Select(id => SetupRunKit.Edge(id)));
        var hold = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        kit.Mal.BeforePage = async (page, ct) =>
        {
            kit.Now += TimeSpan.FromSeconds(3);
            if (page == 9)
                await hold.Task.WaitAsync(ct);
        };
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await SetupRunKit.WaitUntilAsync(() => kit.State.Snapshot().ListRead == 8, "eight entries to be read");

        var snapshot = kit.State.Snapshot();
        Assert.Null(snapshot.ListTotal);        // the bar takes the unknown-total presentation
        Assert.Null(snapshot.List.EtaSeconds);  // and there is nothing to subtract from

        hold.SetResult();
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task AFullyFetchedRowIsNeverReplacedByABasicOneButItsEntryIsRefreshed()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(1, WatchStatus.PlanToWatch, fetched: true, title: "Fully fetched");
        kit.Mal.Edges.Add(SetupRunKit.Edge(1, "completed", title: "Basic from the list"));
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        var row = await db.AnimeMetadata.AsNoTracking().SingleAsync();
        Assert.Equal("Fully fetched", row.Title);
        Assert.NotEqual(default, row.LastSyncedAt);
        Assert.Null(row.Genres);

        var entry = await db.UserAnimeEntries.AsNoTracking().SingleAsync();
        Assert.Equal(WatchStatus.Completed, entry.Status); // MyAnimeList's value is the truth during setup
        Assert.Equal(8, entry.MyScore);
    }

    [Fact]
    public async Task AReReadAddsWhatIsMissingAndDeletesNothing()
    {
        using var kit = new SetupRunKit();
        await kit.SeedAnimeAsync(9, title: "Not on the list any more");
        kit.Mal.Edges.Add(SetupRunKit.Edge(1));
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        Assert.Equal([1, 9], (await db.UserAnimeEntries.AsNoTracking().Select(e => e.AnimeId).ToListAsync()).Order());
        Assert.Equal(2, await db.AnimeMetadata.CountAsync());
    }

    [Fact]
    public async Task AnUnrecognizedStatusLeavesTheAnimeOutAndIsListedWithItsTitleAndStatus()
    {
        using var kit = new SetupRunKit();
        kit.Mal.Edges.AddRange([SetupRunKit.Edge(1), SetupRunKit.Edge(2, "rewatching_v2", title: "Odd one"), SetupRunKit.Edge(3)]);
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await using var db = kit.NewDb();
        Assert.Equal([1, 3], (await db.UserAnimeEntries.Select(e => e.AnimeId).ToListAsync()).Order());
        Assert.DoesNotContain(await db.AnimeMetadata.ToListAsync(), a => a.Id == 2); // no row, no entry
        var skipped = Assert.Single(kit.State.Snapshot().UnrecognizedStatuses);
        Assert.Equal(new UnrecognizedStatusSkip(2, "Odd one", "rewatching_v2"), skipped);
        Assert.Contains(kit.Log.Lines, l => l.Contains("rewatching_v2") && l.StartsWith("Warning"));
    }

    [Fact]
    public async Task TheListOrderIsRememberedForTheDetailsStep()
    {
        using var kit = new SetupRunKit();
        kit.Mal.PageSize = 2;
        kit.Mal.Edges.AddRange([SetupRunKit.Edge(30), SetupRunKit.Edge(10, "bogus"), SetupRunKit.Edge(20), SetupRunKit.Edge(5)]);
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var order = kit.State.ListOrder;
        Assert.Equal([30, 20, 5], order.OrderBy(kv => kv.Value).Select(kv => kv.Key)); // the unrecognized one has no place
    }

    [Fact]
    public async Task TheTotalComesFromTheStatisticsCallAndTheBarNeverEndsBelowWhatWasRead()
    {
        using var kit = new SetupRunKit();
        kit.Mal.Count = 2;
        kit.Mal.Edges.AddRange(Enumerable.Range(1, 3).Select(id => SetupRunKit.Edge(id))); // the list grew since it was counted
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var snapshot = kit.State.Snapshot();
        Assert.Equal(3, snapshot.ListRead);
        Assert.Equal(3, snapshot.ListTotal);
    }

    [Fact]
    public async Task AFailedStatisticsCallLeavesTheTotalUnknownAndTheReadCarriesOn()
    {
        using var kit = new SetupRunKit();
        kit.Mal.CountFails = true;
        kit.Mal.Edges.AddRange([SetupRunKit.Edge(1), SetupRunKit.Edge(2)]);
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var snapshot = kit.State.Snapshot();
        Assert.Equal(2, snapshot.ListRead);
        Assert.Null(snapshot.ListTotal);
        await using var db = kit.NewDb();
        Assert.Equal(2, await db.UserAnimeEntries.CountAsync());
    }

    [Fact]
    public async Task AnEmptyListFinishesTheStepAtOnce()
    {
        using var kit = new SetupRunKit();
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(kit.State.Snapshot().ListReadThisRun);
        Assert.Equal(SetupStepPhase.Done, kit.State.Snapshot().List.Phase);
        await running.Task.WaitAsync(TimeSpan.FromSeconds(10)); // the step itself ended
    }

    [Fact]
    public async Task ARefusedLoginWaitsForReconnectAndLetsTheOtherStepsStart()
    {
        using var kit = new SetupRunKit();
        kit.Mal.ListFailure = new MalAuthorizationRequiredException();
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await stored.Task.WaitAsync(TimeSpan.FromSeconds(10)); // the steps that need no login may start

        Assert.False(read.Task.IsCompleted);
        var snapshot = kit.State.Snapshot();
        Assert.True(snapshot.WaitingForReconnect);
        Assert.Equal(SetupStepPhase.Paused, snapshot.List.Phase);
        Assert.False(snapshot.ListReadThisRun);
        Assert.False(kit.MalHealth.IsDown); // a refused login is not a service failure

        // The user reconnects: the OAuth callback stores the login and signals.
        kit.Mal.ListFailure = null;
        kit.Mal.Edges.Add(SetupRunKit.Edge(1));
        kit.Trigger.Signal();

        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(kit.State.Snapshot().WaitingForReconnect);
        Assert.True(kit.State.Snapshot().ListReadThisRun);
    }

    [Fact]
    public async Task AFailedReadWaitsItsTurnAndRetryNowMakesItDue()
    {
        using var kit = new SetupRunKit();
        kit.Mal.Edges.Add(SetupRunKit.Edge(1));
        kit.Mal.ListFailure = Failures.ServerError();
        var (step, stored, read, ladder) = Create(kit);

        await using var running = Start(step, stored, read);
        await SetupRunKit.WaitUntilAsync(() => kit.Mal.ListReads == 1, "the first read");
        await SetupRunKit.Settle();
        Assert.Equal(1, kit.Mal.ListReads); // waiting a minute, not retrying at once
        Assert.False(read.Task.IsCompleted);

        kit.Mal.ListFailure = null;
        ladder.MakeAllDueNow(kit.Now); // Retry now
        kit.Wake.Pulse();

        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(2, kit.Mal.ListReads);
    }

    [Fact]
    public async Task AMalThatIsDownHoldsTheReadUntilItsOwnLadderIsDue()
    {
        using var kit = new SetupRunKit();
        kit.Mal.Edges.Add(SetupRunKit.Edge(1));
        for (var i = 0; i < ServiceHealth.DownAfterFailures; i++)
            kit.MalHealth.RecordTemporaryFailure();
        var (step, stored, read, _) = Create(kit);

        await using var running = Start(step, stored, read);
        await SetupRunKit.Settle();
        Assert.Equal(0, kit.Mal.ListReads);
        Assert.Equal(SetupStepPhase.Paused, kit.State.Snapshot().List.Phase);

        kit.MalHealth.MakeDueNow(); // Retry now
        kit.Wake.Pulse();

        await read.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.False(kit.MalHealth.IsDown); // the read that got through reset it
    }
}
