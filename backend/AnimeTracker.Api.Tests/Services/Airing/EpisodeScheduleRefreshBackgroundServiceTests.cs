using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Setup;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing;

// external-id-mapping spec, "The mapping refresh and the airing-data work on
// the same tick SHALL NOT affect each other" (design.md D2): each loop
// iteration runs the weekly mapping sync first, in a scope of its own, and a
// failure there never stops the airing work. The TMDB cache purge (tmdb-artwork
// spec, "No cached TMDB set is kept for more than five months", design.md D20)
// is the second such step, with the same isolation. Driven over a real DI
// container so the scopes, and the DbContext each one owns, are the production
// ones. The hourly loop itself is the same code the service always ran; these
// tests cover what one iteration does.
public class EpisodeScheduleRefreshBackgroundServiceTests
{
    private sealed record Event(string What, Guid ContextId);

    public enum MappingStep { ReturnsFailed, Throws, NotRegistered }

    public enum PurgeStep { Throws, NotRegistered }

    // A null step is not registered at all, so resolving it fails like a missing
    // dependency would; the purge is left out of the tests that are not about it.
    private static ServiceProvider BuildProvider(
        List<Event> events, Func<AnimeIdMappingSyncOutcome>? mappingSync, Func<int>? tmdbPurge = null,
        Func<Exception>? catchUpFailure = null)
    {
        var dbName = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));

        if (mappingSync is not null)
        {
            services.AddScoped<IAnimeIdMappingSyncService>(sp =>
                new FakeMappingSync(sp.GetRequiredService<AnimeTrackerDbContext>(), events, mappingSync));
        }

        if (tmdbPurge is not null)
        {
            services.AddScoped<ITmdbCachePurgeService>(sp =>
                new FakeCachePurge(sp.GetRequiredService<AnimeTrackerDbContext>(), events, tmdbPurge));
        }

        services.AddScoped<IEpisodeScheduleRefreshService>(sp =>
            new RecordingRefreshService(sp.GetRequiredService<AnimeTrackerDbContext>(), events, catchUpFailure));
        services.AddSingleton<IBroadcastLocalTimeConverter, BroadcastLocalTimeConverter>();
        services.AddScoped<IEpisodeScheduleService, UnusedEpisodeScheduleService>();
        services.AddScoped<IAiringWatchStatusService, NoopAiringWatchStatusService>();
        return services.BuildServiceProvider();
    }

    // Setup has finished unless a test says otherwise: an install that has, and
    // whose ticks these tests are about.
    private static EpisodeScheduleRefreshBackgroundService CreateService(
        ServiceProvider provider, bool setupFinished = true, bool draining = false)
    {
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var gate = new SetupGate(scopeFactory);
        if (setupFinished)
            gate.MarkFinishedAsync().GetAwaiter().GetResult();

        return new EpisodeScheduleRefreshBackgroundService(
            scopeFactory, gate, new FakeSetupCoordinator { IsDraining = draining },
            NullLogger<EpisodeScheduleRefreshBackgroundService>.Instance);
    }

    [Fact]
    public async Task TheMappingSyncRunsFirstAndInAScopeOfItsOwn()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "airing-catchup", "airing-refresh"], events.Select(e => e.What));
        // Its own DbContext, so a failed mapping save can't leave tracked
        // entities behind for the airing work's saves; the airing work shares
        // one context, as it always did.
        Assert.NotEqual(events[0].ContextId, events[1].ContextId);
        Assert.Equal(events[1].ContextId, events[2].ContextId);
    }

    [Theory]
    [InlineData(MappingStep.ReturnsFailed)]
    [InlineData(MappingStep.Throws)]
    [InlineData(MappingStep.NotRegistered)]
    public async Task AFailingMappingSyncStillLetsTheAiringTickRun(MappingStep step)
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, step switch
        {
            MappingStep.ReturnsFailed => () => AnimeIdMappingSyncOutcome.Failed,
            MappingStep.Throws => () => throw new InvalidOperationException("the sync itself blew up"),
            _ => null, // resolving the service fails
        });

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Contains("airing-catchup", events.Select(e => e.What));
        Assert.Contains("airing-refresh", events.Select(e => e.What));
    }

    [Fact]
    public async Task ACancellationDuringTheMappingSyncStopsTheIterationBeforeTheAiringWork()
    {
        using var cts = new CancellationTokenSource();
        var events = new List<Event>();
        using var provider = BuildProvider(events, () =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(provider).RunIterationAsync(cts.Token));

        Assert.Equal(["mapping-sync"], events.Select(e => e.What));
    }

    [Fact]
    public async Task TheFirstTickAtStartRunsTheMappingSyncThenTheAiringWork()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (events.Count < 3 && !timeout.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);

            Assert.Equal(["mapping-sync", "airing-catchup", "airing-refresh"], events.Select(e => e.What));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task TheTmdbCachePurgeRunsAfterTheMappingSyncAndBeforeTheAiringWork_InAScopeOfItsOwn()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced, tmdbPurge: () => 0);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "tmdb-purge", "airing-catchup", "airing-refresh"], events.Select(e => e.What));
        // Its own DbContext, so a failed purge save can't leave tracked
        // entities behind for either neighbour's saves.
        Assert.NotEqual(events[0].ContextId, events[1].ContextId);
        Assert.NotEqual(events[1].ContextId, events[2].ContextId);
    }

    [Theory]
    [InlineData(PurgeStep.Throws)]
    [InlineData(PurgeStep.NotRegistered)]
    public async Task AFailingTmdbCachePurgeStillLetsTheAiringTickRun(PurgeStep step)
    {
        var events = new List<Event>();
        using var provider = BuildProvider(
            events, () => AnimeIdMappingSyncOutcome.Synced,
            step == PurgeStep.Throws ? () => throw new InvalidOperationException("the purge itself blew up") : null);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Contains("airing-catchup", events.Select(e => e.What));
        Assert.Contains("airing-refresh", events.Select(e => e.What));
    }

    [Fact]
    public async Task AFailingMappingSyncStillLetsTheTmdbCachePurgeRun()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(
            events, () => throw new InvalidOperationException("the sync itself blew up"), tmdbPurge: () => 0);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "tmdb-purge", "airing-catchup", "airing-refresh"], events.Select(e => e.What));
    }

    [Fact]
    public async Task ACancellationDuringTheTmdbCachePurgeStopsTheIterationBeforeTheAiringWork()
    {
        using var cts = new CancellationTokenSource();
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced, tmdbPurge: () =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateService(provider).RunIterationAsync(cts.Token));

        Assert.Equal(["mapping-sync", "tmdb-purge"], events.Select(e => e.What));
    }

    // An HttpClient timeout is a TaskCanceledException while the stopping token
    // is still live. The loop used to read any cancellation as a shutdown and
    // exit, so one AniList timeout ended the hourly airing work until the next
    // restart; it now counts as a failed tick and the loop waits for the next.
    [Fact]
    public async Task ATimeoutDuringATickDoesNotStopTheHourlyLoop()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(
            events, () => AnimeIdMappingSyncOutcome.Synced,
            catchUpFailure: () => new TaskCanceledException(
                "The request was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.", new TimeoutException()));
        var service = CreateService(provider);

        await service.StartAsync(CancellationToken.None);
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (!events.Any(e => e.What == "airing-catchup") && !timeout.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);
            Assert.Contains("airing-catchup", events.Select(e => e.What));

            // Give an exiting loop time to finish; a live one sits in its hourly delay.
            await Task.WhenAny(service.ExecuteTask!, Task.Delay(500, CancellationToken.None));
            Assert.False(service.ExecuteTask!.IsCompleted);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    // add-first-run-setup design D2, first-run-setup "Other work waits for setup": the
    // hourly airing work is one of the jobs setup holds back. The mapping sync and
    // the TMDB purge touch neither MAL nor AniList, so they keep running.
    [Fact]
    public async Task TheAiringPartIsInertUntilSetupHasFinished_WhileTheMappingSyncAndPurgeStillRun()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced, tmdbPurge: () => 0);

        await CreateService(provider, setupFinished: false).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "tmdb-purge"], events.Select(e => e.What));
        using var scope = provider.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<AnimeTrackerDbContext>().AiringRefreshStates.ToListAsync());
    }

    [Fact]
    public async Task TheAiringPartRunsOnTheNextIterationOnceSetupHasFinished_WithNoRestart()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);
        var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
        var gate = new SetupGate(scopeFactory);
        var service = new EpisodeScheduleRefreshBackgroundService(
            scopeFactory, gate, new FakeSetupCoordinator(), NullLogger<EpisodeScheduleRefreshBackgroundService>.Instance);

        await service.RunIterationAsync(CancellationToken.None);
        Assert.DoesNotContain(events, e => e.What.StartsWith("airing"));

        await gate.MarkFinishedAsync();
        await service.RunIterationAsync(CancellationToken.None);

        Assert.Contains(events, e => e.What == "airing-catchup");
        Assert.Contains(events, e => e.What == "airing-refresh");
    }

    // episode-airing-data "Every list anime without airing data is caught up": every
    // tick, not once ever, so there is no flag that can misfire against an empty list.
    [Fact]
    public async Task TheCatchUpRunsOnEveryTick_ThereIsNoOnceEverBackfill()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);
        var service = CreateService(provider);

        await service.RunIterationAsync(CancellationToken.None);
        await service.RunIterationAsync(CancellationToken.None);

        Assert.Equal(2, events.Count(e => e.What == "airing-catchup"));
    }

    [Fact]
    public async Task TheCatchUpSkipsItsTurnWhileSetupsLeftoverAiringWorkIsDraining_ButTheDailyPassStillRuns()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);

        await CreateService(provider, draining: true).RunIterationAsync(CancellationToken.None);

        Assert.DoesNotContain(events, e => e.What == "airing-catchup");
        Assert.Contains(events, e => e.What == "airing-refresh");
    }

    // --- Fakes ---

    private sealed class FakeMappingSync(AnimeTrackerDbContext db, List<Event> events, Func<AnimeIdMappingSyncOutcome> run)
        : IAnimeIdMappingSyncService
    {
        public Task<AnimeIdMappingSyncOutcome> SyncIfDueAsync(CancellationToken ct = default)
        {
            events.Add(new Event("mapping-sync", db.ContextId.InstanceId));
            return Task.FromResult(run());
        }
    }

    private sealed class FakeCachePurge(AnimeTrackerDbContext db, List<Event> events, Func<int> run) : ITmdbCachePurgeService
    {
        public Task<int> PurgeExpiredAsync(CancellationToken ct = default)
        {
            events.Add(new Event("tmdb-purge", db.ContextId.InstanceId));
            return Task.FromResult(run());
        }
    }

    private sealed class RecordingRefreshService(
        AnimeTrackerDbContext db, List<Event> events, Func<Exception>? catchUpFailure) : IEpisodeScheduleRefreshService
    {
        public Task CatchUpAsync(CancellationToken ct = default)
        {
            events.Add(new Event("airing-catchup", db.ContextId.InstanceId));
            return catchUpFailure is not null ? Task.FromException(catchUpFailure()) : Task.CompletedTask;
        }

        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());

        public Task<RefreshBatchResult> RefreshBatchAsync(IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default)
        {
            events.Add(new Event("airing-refresh", db.ContextId.InstanceId));
            return Task.FromResult(new RefreshBatchResult(Fetched: 0, NoData: 0, Failed: 0));
        }

        public Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class UnusedEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class NoopAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
