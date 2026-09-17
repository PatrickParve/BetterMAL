using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Search;

// AnimeSearchIndexCache (design.md D5/D7): same rows as the repository, one
// query reused across reads, invalidation-not-a-clock, and the two races a
// singleton rebuilding through a scope factory has to close — a cold cache
// racing itself, and a write landing mid-rebuild.
public class AnimeSearchIndexCacheTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task GetAsync_ReturnsExactlyWhatTheRepositoryReturnsForTheSameData()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "Attack on Titan", EnglishTitle = "Attack on Titan",
            PictureUrl = "https://mal/1.jpg", PopularityRank = 1,
        });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "No Nullable Fields At All" });
        await db.SaveChangesAsync();

        var expected = await new AnimeMetadataRepository(db).GetSearchIndexAsync();
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(new CountingAnimeMetadataRepository(db)));

        var actual = await cache.GetAsync();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetAsync_TwoReadsWithNoInvalidateRunTheUnderlyingQueryOnce()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db);
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        await cache.GetAsync();
        await cache.GetAsync();

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task GetAsync_InvalidateMakesTheNextReadRebuildAndIncludeTheWrite()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db);
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        var first = await cache.GetAsync();
        Assert.Single(first);

        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Anime 2" });
        await db.SaveChangesAsync();
        cache.Invalidate();

        var second = await cache.GetAsync();
        Assert.Equal(2, second.Count);
        Assert.Equal(2, repository.CallCount);

        // Nothing changed since, so a third read must not query again.
        var third = await cache.GetAsync();
        Assert.Equal(2, third.Count);
        Assert.Equal(2, repository.CallCount);
    }

    [Fact]
    public async Task GetAsync_NeverExpiresOnItsOwnRegardlessOfTimePassing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db);
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        await cache.GetAsync();
        // Nothing but Invalidate() should ever expire this cache — an
        // await is enough to prove the point without a real sleep, which
        // would make this test slow for no reason.
        await Task.Yield();
        await cache.GetAsync();

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task GetAsync_ConcurrentColdReadsIssueOneQueryAndAllSeeTheCompleteList()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 3; id++)
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db) { Gate = new TaskCompletionSource() };
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        // Each call runs synchronously up to its first real await, so by the
        // time this loop returns, the first has entered the repository call
        // (and is blocked on the gate) and the rest are queued behind the
        // rebuild lock — none of them have queried yet.
        var pending = Enumerable.Range(0, 5).Select(_ => cache.GetAsync()).ToList();
        repository.Gate.SetResult();
        var results = await Task.WhenAll(pending);

        Assert.Equal(1, repository.CallCount);
        Assert.All(results, r => Assert.Equal(3, r.Count));
    }

    [Fact]
    public async Task GetAsync_AWriteThatInvalidatesMidRebuildIsPickedUpByTheNextReadNotTheStaleOne()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db) { Gate = new TaskCompletionSource() };
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        var inFlight = cache.GetAsync(); // blocked mid-rebuild on the gate, having captured generation 0

        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Anime 2" });
        await db.SaveChangesAsync();
        cache.Invalidate(); // bumps to generation 1 while the rebuild above is still in flight

        repository.Gate.SetResult();
        await inFlight; // stale-but-published rows tagged generation 0 — not asserted on

        repository.Gate = null; // let the rebuild this triggers complete immediately
        var next = await cache.GetAsync();

        Assert.Equal(2, next.Count);
        Assert.Equal(2, repository.CallCount); // the in-flight rebuild's query, plus this one — never a third
    }

    // Delegates to the real repository over the given context so "same rows"
    // holds by construction, while counting calls and optionally blocking on
    // a gate mid-query to simulate a rebuild in flight.
    private sealed class CountingAnimeMetadataRepository(AnimeTrackerDbContext db) : IAnimeMetadataRepository
    {
        private readonly AnimeMetadataRepository _inner = new(db);
        public int CallCount { get; private set; }
        public TaskCompletionSource? Gate { get; set; }

        public async Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default)
        {
            CallCount++;
            if (Gate is { } gate)
                await gate.Task;
            return await _inner.GetSearchIndexAsync(ct);
        }

        public Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AnimeSearchFallbackProjection>> GetSearchFallbackIndexAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeScopeFactory(IAnimeMetadataRepository repository) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(repository);
    }

    private sealed class FakeScope(IAnimeMetadataRepository repository) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(repository);
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider(IAnimeMetadataRepository repository) : IServiceProvider
    {
        public object? GetService(Type serviceType) => serviceType == typeof(IAnimeMetadataRepository) ? repository : null;
    }
}
