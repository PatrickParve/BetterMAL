using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Setup;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// The queue behind a new list entry's data fetch (episode-airing-data "Event-driven
// refresh triggers", metadata-refresh "Full detail fetch reserved for ..."): every
// anime queued since the last wake is checked against my list; MyAnimeList's full
// detail is fetched first, per anime, for a row that never had one; AniList's episode
// dates follow, once for the whole set. HttpClient reports its own timeout as a
// TaskCanceledException while the stopping token is still live; the loop used to read
// any cancellation as a shutdown and exit, so one AniList timeout stopped every later
// add-time fetch until the next restart.
public class NewEntryRefreshBackgroundServiceTests
{
    [Fact]
    public async Task ANeverFetchedRowGetsTheMyAnimeListFetchThenAniList_InThatOrder()
    {
        await using var harness = Harness.Create(unfetched: [1]);

        await harness.RunAsync(1);

        Assert.Equal(["mal:1", "anilist:1"], harness.Calls);
    }

    [Fact]
    public async Task AnAlreadyFetchedRowGetsAniListOnly()
    {
        await using var harness = Harness.Create(fetched: [1]);

        await harness.RunAsync(1);

        Assert.Equal(["anilist:1"], harness.Calls);
    }

    [Fact]
    public async Task ARemovedEntryGetsNeitherFetch()
    {
        // Anime 1's row never had a full fetch, but its entry is gone: nothing is
        // fetched for it from either service, and anime 2 goes alone.
        await using var harness = Harness.Create(fetched: [2], removed: [1]);

        await harness.RunAsync(1, 2);

        Assert.Equal(["anilist:2"], harness.Calls);
    }

    [Fact]
    public async Task AMyAnimeListFailureStillRunsAniList()
    {
        await using var harness = Harness.Create(unfetched: [1]);
        harness.MalFailures[1] = new HttpRequestException("MyAnimeList is down.");

        await harness.RunAsync(1);

        Assert.Equal(["mal:1", "anilist:1"], harness.Calls);
    }

    [Fact]
    public async Task ThreeIdsQueuedTogetherMakeOneBatchedAniListCall()
    {
        await using var harness = Harness.Create(fetched: [1, 2, 3]);

        await harness.RunAsync(1, 2, 3);

        Assert.Equal(["anilist-batch:1,2,3"], harness.Calls);
    }

    [Fact]
    public async Task EveryNeverFetchedRowOfABurstIsFetchedFromMyAnimeListBeforeTheBatch()
    {
        await using var harness = Harness.Create(unfetched: [1, 3], fetched: [2]);

        await harness.RunAsync(1, 2, 3);

        Assert.Equal(["mal:1", "mal:3", "anilist-batch:1,2,3"], harness.Calls);
    }

    [Fact]
    public async Task AnIdQueuedTwiceIsFetchedOnce()
    {
        await using var harness = Harness.Create(fetched: [1]);

        await harness.RunAsync(1, 1);

        Assert.Equal(["anilist:1"], harness.Calls);
    }

    [Fact]
    public async Task AFetchThatLandedWhileWaitingForTheGateIsNotRepeated()
    {
        await using var harness = Harness.Create(unfetched: [1]);

        // The detail page's visit fetch holds the anime's gate; once it has
        // stamped the row and let go, the queued fetch finds nothing left to do.
        var gate = harness.Provider.GetRequiredService<RefreshGate>();
        var held = await gate.LockAsync("anime:1", CancellationToken.None);
        harness.Trigger.Enqueue(1);
        await harness.StartAsync();
        try
        {
            await TestSetupGates.LetItRunAsync();
            Assert.Empty(harness.Calls);

            await harness.StampFetchedAsync(1);
            held.Dispose();
            await TestSetupGates.WaitForAsync(() => harness.Calls.Count == 1);

            Assert.Equal(["anilist:1"], harness.Calls);
        }
        finally
        {
            await harness.StopAsync();
        }
    }

    [Fact]
    public async Task ATimedOutRefreshDoesNotStopTheQueue()
    {
        await using var harness = Harness.Create(fetched: [1, 2]);
        harness.AniListFailures[1] = new TaskCanceledException(
            "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException());

        await harness.StartAsync();
        try
        {
            harness.Trigger.Enqueue(1);
            await TestSetupGates.WaitForAsync(() => harness.Calls.Contains("anilist:1"));
            harness.Trigger.Enqueue(2);
            await TestSetupGates.WaitForAsync(() => harness.Calls.Contains("anilist:2"));

            Assert.Equal(["anilist:1", "anilist:2"], harness.Calls);
            Assert.False(harness.Service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await harness.StopAsync();
        }
    }

    // add-first-run-setup design D2: setup's airing step fetches every list anime,
    // so an add-time fetch queued meanwhile waits, and runs once setup has finished.
    [Fact]
    public async Task NothingIsFetchedUntilSetupHasFinished_ThenTheQueuedFetchRuns()
    {
        await using var harness = Harness.Create(unfetched: [1], gate: TestSetupGates.Unfinished());

        await harness.StartAsync();
        try
        {
            harness.Trigger.Enqueue(1);
            await TestSetupGates.LetItRunAsync();
            Assert.Empty(harness.Calls);

            await harness.Gate.MarkFinishedAsync();
            await TestSetupGates.WaitForAsync(() => harness.Calls.Count == 2);
            Assert.Equal(["mal:1", "anilist:1"], harness.Calls);
        }
        finally
        {
            await harness.StopAsync();
        }
    }

    // One database, one call log and one worker over the real new-entry queue, with
    // the two services it calls replaced by recorders.
    private sealed class Harness : IAsyncDisposable
    {
        private Harness(SetupGate gate, int[] unfetched, int[] fetched, int[] removed)
        {
            Gate = gate;
            var dbName = Guid.NewGuid().ToString();
            var services = new ServiceCollection();
            services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddScoped<IUserAnimeEntryRepository, UserAnimeEntryRepository>();
            services.AddSingleton<RefreshGate>();
            services.AddScoped<IMetadataRefreshService>(sp =>
                new RecordingMetadataRefreshService(sp.GetRequiredService<AnimeTrackerDbContext>(), Calls, MalFailures));
            services.AddScoped<IEpisodeScheduleRefreshService>(_ => new RecordingAiringRefreshService(Calls, AniListFailures));
            Provider = services.BuildServiceProvider();

            Seed(unfetched, fetched, removed);
            Service = new NewEntryRefreshBackgroundService(
                Provider.GetRequiredService<IServiceScopeFactory>(), Trigger, gate, NullLogger<NewEntryRefreshBackgroundService>.Instance);
        }

        public ServiceProvider Provider { get; }
        public SetupGate Gate { get; }
        public NewEntryRefreshTrigger Trigger { get; } = new();
        public NewEntryRefreshBackgroundService Service { get; }
        public List<string> Calls { get; } = [];
        public Dictionary<int, Exception> MalFailures { get; } = [];
        public Dictionary<int, Exception> AniListFailures { get; } = [];

        /// <summary>Anime in <paramref name="unfetched"/> are in my list with a row that never
        /// had a full fetch; <paramref name="fetched"/> are in my list and already fetched;
        /// <paramref name="removed"/> have an unfetched row and no list entry.</summary>
        public static Harness Create(
            int[]? unfetched = null, int[]? fetched = null, int[]? removed = null, SetupGate? gate = null) =>
            new(gate ?? TestSetupGates.Finished(), unfetched ?? [], fetched ?? [], removed ?? []);

        private void Seed(int[] unfetched, int[] fetched, int[] removed)
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            var now = DateTimeOffset.UtcNow;
            foreach (var id in unfetched.Concat(removed))
                db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}" });
            foreach (var id in fetched)
                db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", LastSyncedAt = now });
            foreach (var id in unfetched.Concat(fetched))
                db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id, Status = WatchStatus.PlanToWatch });
            db.SaveChanges();
        }

        public Task StartAsync() => Service.StartAsync(CancellationToken.None);

        public Task StopAsync() => Service.StopAsync(CancellationToken.None);

        // Queues the ids while the worker is stopped, so its first wake drains them
        // all, then waits for the work to finish.
        public async Task RunAsync(params int[] animeIds)
        {
            foreach (var id in animeIds)
                Trigger.Enqueue(id);

            await StartAsync();
            try
            {
                // The worker is done with a burst once the AniList step has been
                // reached (it is always the last call), or the burst has nothing to fetch.
                await TestSetupGates.WaitForAsync(() => Calls.Any(c => c.StartsWith("anilist")));
                await TestSetupGates.LetItRunAsync();
            }
            finally
            {
                await StopAsync();
            }
        }

        public async Task StampFetchedAsync(int animeId)
        {
            using var scope = Provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>();
            (await db.AnimeMetadata.FirstAsync(a => a.Id == animeId)).LastSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await Provider.DisposeAsync();
    }

    private sealed class RecordingMetadataRefreshService(
        AnimeTrackerDbContext db, List<string> calls, Dictionary<int, Exception> failures) : IMetadataRefreshService
    {
        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            calls.Add($"mal:{animeId}");
            if (failures.TryGetValue(animeId, out var failure))
                throw failure;

            (await db.AnimeMetadata.FirstAsync(a => a.Id == animeId, ct)).LastSyncedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
    }

    private sealed class RecordingAiringRefreshService(List<string> calls, Dictionary<int, Exception> failures) : IEpisodeScheduleRefreshService
    {
        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default)
        {
            calls.Add($"anilist:{animeId}");
            return failures.TryGetValue(animeId, out var failure) ? Task.FromException(failure) : Task.CompletedTask;
        }

        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default)
        {
            calls.Add($"anilist-batch:{string.Join(",", animeIds)}");
            return Task.FromResult(new RefreshBatchResult(animeIds.Count, 0, 0));
        }

        public Task CatchUpAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
