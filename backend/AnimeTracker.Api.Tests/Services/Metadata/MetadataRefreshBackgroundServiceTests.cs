using System.Net;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Metadata;

// MetadataRefreshBackgroundService (metadata-refresh-call-accounting tasks
// 5.1-5.3): RunPassAsync drives resolution then the staleness batch over real
// AnnouncementResolutionService/MetadataRefreshService instances sharing one
// scriptable fake IMalClient, so these tests exercise the actual outage/
// not-found/skip wiring end to end rather than re-mocking it.
public class MetadataRefreshBackgroundServiceTests
{
    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static MalAnimeNode DetailNode(int id) => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = "tv",
        Status = "finished_airing",
    };

    // Five candidates on the same (StaleTtl) tier with ascending LastSyncedAt,
    // so RefreshTiers.LastAttemptAt orders them 1, 2, 3, 4, 5 — a fixed order
    // the tests below depend on. Mirrors MetadataRefreshServiceTests'
    // SeedFiveDueAnime.
    private static void SeedFiveDueAnime(AnimeTrackerDbContext db, DateTimeOffset now)
    {
        for (var id = 1; id <= 5; id++)
        {
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", LastSyncedAt = now - TimeSpan.FromDays(65 - id) });
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id });
        }
    }

    // A pending discovery naming animeId, owned by a distinct anime row (the
    // "owner" id doesn't need to be a due candidate itself).
    private static void SeedDiscovery(AnimeTrackerDbContext db, int ownerAnimeId, int relatedAnimeId, DateTimeOffset discoveredAt)
    {
        db.AnimeMetadata.Add(new AnimeMetadata { Id = ownerAnimeId, Title = $"Owner {ownerAnimeId}" });
        db.RelationDiscoveries.Add(new RelationDiscovery
        {
            AnimeId = ownerAnimeId,
            RelatedAnimeId = relatedAnimeId,
            RelationType = "sequel",
            DiscoveredAt = discoveredAt,
        });
    }

    [Fact]
    public async Task MalUnreachable_EachPassMakesExactlyOneAttemptAndTheSkipRotates()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            SeedDiscovery(db, ownerAnimeId: 999, relatedAnimeId: 100, discoveredAt: now.AddDays(-1));
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        // Every id fails with no response — an outage, and worse, one none of
        // this job's own state can distinguish from any other.
        var noResponse = new HttpRequestException("no response");
        foreach (var id in new[] { 100, 1, 2, 3, 4, 5 })
            malClient.Failures[id] = noResponse;

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        await service.RunPassAsync(today, CancellationToken.None);
        Assert.Equal(1, service.CallsThisWindow);
        Assert.Equal([100], malClient.Calls);

        await service.RunPassAsync(today, CancellationToken.None);
        Assert.Equal(2, service.CallsThisWindow);
        Assert.Equal([100, 1], malClient.Calls);

        await service.RunPassAsync(today, CancellationToken.None);
        Assert.Equal(3, service.CallsThisWindow);
        Assert.Equal([100, 1, 100], malClient.Calls);
    }

    [Fact]
    public async Task OneAnimeThatAlwaysFails_TakesTurnsWithTheRestOfTheBatch()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        malClient.Failures[1] = new HttpRequestException("failed", null, HttpStatusCode.InternalServerError);
        for (var id = 2; id <= 5; id++)
            malClient.Responses[id] = DetailNode(id);

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        await service.RunPassAsync(today, CancellationToken.None); // pass 1: only anime 1
        Assert.Equal([1], malClient.Calls);

        await service.RunPassAsync(today, CancellationToken.None); // pass 2: skips 1, refreshes 2-5
        Assert.Equal([1, 2, 3, 4, 5], malClient.Calls);

        await service.RunPassAsync(today, CancellationToken.None); // pass 3: 2-5 are fresh, so only 1 is due
        Assert.Equal([1, 2, 3, 4, 5, 1], malClient.Calls);
        Assert.Equal(6, service.CallsThisWindow);
    }

    [Fact]
    public async Task OneOffFailure_RefreshesNormallyOnceMalRecoversAndLeavesNoSkipBehind()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        malClient.Failures[1] = new HttpRequestException("failed", null, HttpStatusCode.InternalServerError);
        for (var id = 2; id <= 5; id++)
            malClient.Responses[id] = DetailNode(id);

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        await service.RunPassAsync(today, CancellationToken.None); // pass 1: 1 fails, ends the pass
        await service.RunPassAsync(today, CancellationToken.None); // pass 2: skips 1, refreshes 2-5

        // MAL recovers for anime 1 before the next pass.
        malClient.Failures.Remove(1);
        malClient.Responses[1] = DetailNode(1);

        await service.RunPassAsync(today, CancellationToken.None); // pass 3: 1 is refreshed normally
        Assert.Equal([1, 2, 3, 4, 5, 1], malClient.Calls);
        using (var db = new AnimeTrackerDbContext(options))
        {
            var stored = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
            Assert.NotEqual(now - TimeSpan.FromDays(64), stored.LastSyncedAt);
        }

        var callsBeforePass4 = malClient.Calls.Count;
        await service.RunPassAsync(today, CancellationToken.None); // pass 4: nothing due, no skip was left behind
        Assert.Equal(callsBeforePass4, malClient.Calls.Count);
    }

    [Fact]
    public async Task OutageOnTheSecondOfFiveDueAnimeStopsThePass()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        malClient.Responses[1] = DetailNode(1);
        malClient.Failures[2] = new HttpRequestException("failed", null, HttpStatusCode.ServiceUnavailable);

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);

        await service.RunPassAsync(DateOnly.FromDateTime(now.UtcDateTime), CancellationToken.None);

        Assert.Equal(2, service.CallsThisWindow);
        Assert.Equal([1, 2], malClient.Calls);
    }

    [Fact]
    public async Task OutageOnTheAnnouncementFetchSkipsTheRefreshBatchEntirely()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = now - TimeSpan.FromDays(60) });
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1 });
            SeedDiscovery(db, ownerAnimeId: 999, relatedAnimeId: 100, discoveredAt: now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        malClient.Failures[100] = new HttpRequestException("failed", null, HttpStatusCode.ServiceUnavailable);
        malClient.Responses[1] = DetailNode(1); // would succeed if the batch ever ran

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);

        await service.RunPassAsync(DateOnly.FromDateTime(now.UtcDateTime), CancellationToken.None);

        Assert.Equal(1, service.CallsThisWindow);
        Assert.Equal([100], malClient.Calls);
    }

    [Fact]
    public async Task NotFoundAmongFiveDueAnimeWaitsOutItsTierOnTheNextPass()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        malClient.Failures[3] = new HttpRequestException("not found", null, HttpStatusCode.NotFound);
        foreach (var id in new[] { 1, 2, 4, 5 })
            malClient.Responses[id] = DetailNode(id); // no relations: nothing for a second pass to resolve

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        await service.RunPassAsync(today, CancellationToken.None);
        Assert.Equal(5, service.CallsThisWindow);
        Assert.Equal([1, 2, 3, 4, 5], malClient.Calls);

        await service.RunPassAsync(today, CancellationToken.None); // the four fresh anime aren't due; 3 waits out its tier
        Assert.Equal(5, service.CallsThisWindow);
        Assert.Equal([1, 2, 3, 4, 5], malClient.Calls);
    }

    [Fact]
    public async Task TheCapReachedByFailuresStopsTheJobForTheDayAndResetsOnTheNextDay()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            // More due anime than one batch (BatchSize is 20): every one of
            // them fails with a 400 (Other), which never marks it, so it
            // stays due and the batch spends its full allowance every pass.
            for (var id = 1; id <= 21; id++)
            {
                db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", LastSyncedAt = now - TimeSpan.FromDays(60) });
                db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id });
            }
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        for (var id = 1; id <= 21; id++)
            malClient.Failures[id] = new HttpRequestException("bad request", null, HttpStatusCode.BadRequest);

        var service = new MetadataRefreshBackgroundService(new FakeServiceScopeFactory(options, malClient), NullLogger<MetadataRefreshBackgroundService>.Instance);
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        while (service.CallsThisWindow < MetadataRefreshBackgroundService.NightlyCap)
            await service.RunPassAsync(today, CancellationToken.None);

        Assert.Equal(MetadataRefreshBackgroundService.NightlyCap, service.CallsThisWindow);
        var callsAtCap = malClient.Calls.Count;

        await service.RunPassAsync(today, CancellationToken.None); // same day: no quota left
        Assert.Equal(callsAtCap, malClient.Calls.Count);
        Assert.Equal(MetadataRefreshBackgroundService.NightlyCap, service.CallsThisWindow);

        await service.RunPassAsync(today.AddDays(1), CancellationToken.None); // new UTC day: window resets
        Assert.True(malClient.Calls.Count > callsAtCap);
        Assert.Equal(20, service.CallsThisWindow);
    }

    // --- cache-type-ahead-search-index tasks.md 5.4: invalidation through the background service ---

    [Fact]
    public async Task RunPassAsync_APassThatRefreshesDueAnimeInvalidatesTheSearchIndex()
    {
        var options = CreateOptions();
        var now = DateTimeOffset.UtcNow;
        using (var db = new AnimeTrackerDbContext(options))
        {
            SeedFiveDueAnime(db, now);
            await db.SaveChangesAsync();
        }

        var malClient = new FakeMalClient();
        foreach (var id in new[] { 1, 2, 3, 4, 5 })
            malClient.Responses[id] = DetailNode(id);

        var searchIndex = new FakeAnimeSearchIndex();
        var service = new MetadataRefreshBackgroundService(
            new FakeServiceScopeFactory(options, malClient, searchIndex), NullLogger<MetadataRefreshBackgroundService>.Instance);

        await service.RunPassAsync(DateOnly.FromDateTime(now.UtcDateTime), CancellationToken.None);

        Assert.True(searchIndex.InvalidateCallCount > 0);
    }

    [Fact]
    public async Task RunPassAsync_APassWithNothingDueDoesNotInvalidateTheSearchIndex()
    {
        var options = CreateOptions();
        using (var db = new AnimeTrackerDbContext(options))
        {
            await db.SaveChangesAsync(); // nothing seeded: no candidates, no discoveries
        }

        var malClient = new FakeMalClient();
        var searchIndex = new FakeAnimeSearchIndex();
        var service = new MetadataRefreshBackgroundService(
            new FakeServiceScopeFactory(options, malClient, searchIndex), NullLogger<MetadataRefreshBackgroundService>.Instance);

        await service.RunPassAsync(DateOnly.FromDateTime(DateTimeOffset.UtcNow.UtcDateTime), CancellationToken.None);

        Assert.Equal(0, searchIndex.InvalidateCallCount);
    }

    private sealed class FakeMalClient : IMalClient
    {
        public List<int> Calls { get; } = [];
        public Dictionary<int, MalAnimeNode> Responses { get; } = new();
        public Dictionary<int, Exception> Failures { get; } = new();

        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default)
        {
            Calls.Add(animeId);
            if (Failures.TryGetValue(animeId, out var failure))
                throw failure;
            return Task.FromResult(Responses[animeId]);
        }

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    // Each scope opens its own AnimeTrackerDbContext over the same named
    // in-memory database (mirroring real per-scope resolution), wiring real
    // AnnouncementResolutionService and MetadataRefreshService instances over
    // the one shared FakeMalClient — the same instances production DI would
    // hand the background service, so these tests exercise the actual
    // outage/not-found/skip control flow rather than re-mocking it.
    private sealed class FakeServiceScopeFactory(
        DbContextOptions<AnimeTrackerDbContext> options, FakeMalClient malClient, IAnimeSearchIndex? searchIndex = null) : IServiceScopeFactory
    {
        // One instance shared across every scope this factory creates, the
        // same way the real singleton cache outlives each pass's own scope.
        private readonly IAnimeSearchIndex _searchIndex = searchIndex ?? new FakeAnimeSearchIndex();

        public IServiceScope CreateScope() => new FakeServiceScope(options, malClient, _searchIndex);
    }

    private sealed class FakeServiceScope : IServiceScope
    {
        private readonly AnimeTrackerDbContext _db;
        public IServiceProvider ServiceProvider { get; }

        public FakeServiceScope(DbContextOptions<AnimeTrackerDbContext> options, FakeMalClient malClient, IAnimeSearchIndex searchIndex)
        {
            _db = new AnimeTrackerDbContext(options);
            var changeDetector = new AnimeMetadataChangeDetector(
                _db, new AnimeUpdateRecorder(_db, new AnimeUpdateRelevance(_db, new RelationResolver(_db))), new SeriesBuildTrigger());
            var metadataRefresh = new MetadataRefreshService(_db, malClient, changeDetector, searchIndex, NullLogger<MetadataRefreshService>.Instance);
            var resolution = new AnnouncementResolutionService(
                _db, metadataRefresh, new AnimeUpdateRecorder(_db, new AnimeUpdateRelevance(_db, new RelationResolver(_db))), NullLogger<AnnouncementResolutionService>.Instance);
            ServiceProvider = new FakeServiceProvider(metadataRefresh, resolution);
        }

        public void Dispose() => _db.Dispose();
    }

    private sealed class FakeServiceProvider(IMetadataRefreshService metadataRefresh, IAnnouncementResolutionService resolution) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IMetadataRefreshService)) return metadataRefresh;
            if (serviceType == typeof(IAnnouncementResolutionService)) return resolution;
            return null;
        }
    }
}
