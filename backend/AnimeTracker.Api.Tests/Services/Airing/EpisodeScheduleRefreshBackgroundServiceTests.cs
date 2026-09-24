using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.IdMapping;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Tmdb;
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
        List<Event> events, Func<AnimeIdMappingSyncOutcome>? mappingSync, Func<int>? tmdbPurge = null)
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
            new RecordingRefreshService(sp.GetRequiredService<AnimeTrackerDbContext>(), events));
        services.AddSingleton<IBroadcastLocalTimeConverter, BroadcastLocalTimeConverter>();
        services.AddScoped<IEpisodeScheduleService, UnusedEpisodeScheduleService>();
        services.AddScoped<IAiringWatchStatusService, NoopAiringWatchStatusService>();
        return services.BuildServiceProvider();
    }

    private static EpisodeScheduleRefreshBackgroundService CreateService(ServiceProvider provider) =>
        new(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<EpisodeScheduleRefreshBackgroundService>.Instance);

    [Fact]
    public async Task TheMappingSyncRunsFirstAndInAScopeOfItsOwn()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(events, () => AnimeIdMappingSyncOutcome.Synced);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "airing-backfill", "airing-refresh"], events.Select(e => e.What));
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

        Assert.Contains("airing-backfill", events.Select(e => e.What));
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

            Assert.Equal(["mapping-sync", "airing-backfill", "airing-refresh"], events.Select(e => e.What));
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

        Assert.Equal(["mapping-sync", "tmdb-purge", "airing-backfill", "airing-refresh"], events.Select(e => e.What));
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

        Assert.Contains("airing-backfill", events.Select(e => e.What));
        Assert.Contains("airing-refresh", events.Select(e => e.What));
    }

    [Fact]
    public async Task AFailingMappingSyncStillLetsTheTmdbCachePurgeRun()
    {
        var events = new List<Event>();
        using var provider = BuildProvider(
            events, () => throw new InvalidOperationException("the sync itself blew up"), tmdbPurge: () => 0);

        await CreateService(provider).RunIterationAsync(CancellationToken.None);

        Assert.Equal(["mapping-sync", "tmdb-purge", "airing-backfill", "airing-refresh"], events.Select(e => e.What));
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

    private sealed class RecordingRefreshService(AnimeTrackerDbContext db, List<Event> events) : IEpisodeScheduleRefreshService
    {
        public Task BackfillAsync(CancellationToken ct = default)
        {
            events.Add(new Event("airing-backfill", db.ContextId.InstanceId));
            return Task.CompletedTask;
        }

        public Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());

        public Task<RefreshManyResult> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null)
        {
            events.Add(new Event("airing-refresh", db.ContextId.InstanceId));
            return Task.FromResult(new RefreshManyResult(NoData: 0, Failed: 0));
        }

        public Task RefreshOneAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default) => throw new NotImplementedException();
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
