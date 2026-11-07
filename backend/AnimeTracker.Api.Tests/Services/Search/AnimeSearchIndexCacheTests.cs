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

    // --- The by-id view (improve-search-page design.md D1, task 1.1) ---

    [Fact]
    public async Task GetByIdAsync_HoldsTheSameRowsAsGetAsync()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "Attack on Titan", EnglishTitle = "Attack on Titan",
            PictureUrl = "https://mal/1.jpg", PopularityRank = 1,
        });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "No Nullable Fields At All" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 30, Title = "Thirty" });
        await db.SaveChangesAsync();

        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(new CountingAnimeMetadataRepository(db)));

        var rows = await cache.GetAsync();
        var byId = await cache.GetByIdAsync();

        Assert.Equal(3, byId.Count);
        Assert.Equal(rows.Count, byId.Count);
        // The very same instances, not copies that merely compare equal: one entry carries both.
        Assert.All(rows, row => Assert.Same(row, byId[row.Id]));
        Assert.Equal("https://mal/1.jpg", byId[1].PictureUrl);
    }

    [Fact]
    public async Task GetByIdAsync_IsBuiltOncePerGenerationNotOncePerCall()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(new CountingAnimeMetadataRepository(db)));

        var first = await cache.GetByIdAsync();
        var second = await cache.GetByIdAsync();
        Assert.Same(first, second); // the cached view, not a dictionary rebuilt from the list on every search

        cache.Invalidate();
        var third = await cache.GetByIdAsync();
        Assert.NotSame(first, third); // a new generation brings a new view
    }

    [Fact]
    public async Task TheCachedViewIsWhatTheInterfaceHandsOut()
    {
        // The search services reach the index through IAnimeSearchIndex, where GetByIdAsync has a default
        // implementation for fakes. The cache's own method has to be the one that answers there, or every
        // results-page search would rebuild the dictionary from the list instead of reading the cached one.
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();
        IAnimeSearchIndex index = new AnimeSearchIndexCache(new FakeScopeFactory(new CountingAnimeMetadataRepository(db)));

        var first = await index.GetByIdAsync();
        var second = await index.GetByIdAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task GetByIdAsync_AnIdTheAppHasNotSavedIsNotInIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(new CountingAnimeMetadataRepository(db)));

        var byId = await cache.GetByIdAsync();

        Assert.True(byId.ContainsKey(1));
        Assert.False(byId.ContainsKey(2));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TheRowsAndTheByIdViewShareOneQueryWhicheverIsReadFirst(bool rowsFirst)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db);
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        if (rowsFirst)
        {
            await cache.GetAsync();
            await cache.GetByIdAsync();
        }
        else
        {
            await cache.GetByIdAsync();
            await cache.GetAsync();
        }

        Assert.Equal(1, repository.CallCount);
    }

    [Fact]
    public async Task Invalidate_MakesTheListAndTheByIdViewRebuildTogether()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db);
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        Assert.Single(await cache.GetAsync());
        Assert.Single(await cache.GetByIdAsync());

        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Anime 2" });
        await db.SaveChangesAsync();
        cache.Invalidate();

        // The by-id read goes first, so it is the one that triggers the rebuild: the
        // list read after it must be the same new generation, not a stale one.
        var byId = await cache.GetByIdAsync();
        var rows = await cache.GetAsync();

        Assert.Equal(2, byId.Count);
        Assert.Equal(2, rows.Count);
        Assert.All(rows, row => Assert.Same(row, byId[row.Id]));
        Assert.Equal(2, repository.CallCount); // the first build, and one rebuild — not one per view
    }

    [Fact]
    public async Task GetByIdAsync_ConcurrentColdReadsOfBothViewsIssueOneQuery()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 3; id++)
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db) { Gate = new TaskCompletionSource() };
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        var rows = new List<Task<List<AnimeTitleProjection>>>();
        var byIds = new List<Task<IReadOnlyDictionary<int, AnimeTitleProjection>>>();
        for (var i = 0; i < 4; i++)
        {
            rows.Add(cache.GetAsync());
            byIds.Add(cache.GetByIdAsync());
        }

        repository.Gate.SetResult();
        await Task.WhenAll(rows);
        await Task.WhenAll(byIds);

        Assert.Equal(1, repository.CallCount);
        Assert.All(rows, task => Assert.Equal(3, task.Result.Count));
        Assert.All(byIds, task => Assert.Equal(3, task.Result.Count));
    }

    [Fact]
    public async Task GetByIdAsync_AWriteThatInvalidatesMidRebuildIsPickedUpByTheNextReadNotTheStaleOne()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var repository = new CountingAnimeMetadataRepository(db) { Gate = new TaskCompletionSource() };
        var cache = new AnimeSearchIndexCache(new FakeScopeFactory(repository));

        var inFlight = cache.GetByIdAsync(); // blocked mid-rebuild on the gate, having captured generation 0

        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Anime 2" });
        await db.SaveChangesAsync();
        cache.Invalidate(); // bumps to generation 1 while the rebuild above is still in flight

        repository.Gate.SetResult();
        await inFlight; // stale-but-published, tagged generation 0 — not asserted on

        repository.Gate = null;
        var next = await cache.GetByIdAsync();

        Assert.Equal(2, next.Count);
        Assert.Equal(2, repository.CallCount);
    }

    [Fact]
    public async Task TheDefaultViewOverAnImplementationThatOnlySuppliesRowsHoldsItsRows()
    {
        var fake = new FakeAnimeSearchIndex([new(1, "A", null, null, 1), new(2, "B", "B English", "b.jpg", null)]);
        IAnimeSearchIndex index = fake;

        var byId = await index.GetByIdAsync();

        Assert.Equal([1, 2], byId.Keys.Order());
        Assert.Equal("B English", byId[2].EnglishTitle);
        Assert.Equal("b.jpg", byId[2].PictureUrl);

        // Built from GetAsync on every call, so a fake whose rows change is followed.
        fake.Rows = [new(3, "C", null, null, null)];
        Assert.Equal([3], (await index.GetByIdAsync()).Keys);
    }

    [Fact]
    public void IndexById_GivenTheSameIdTwiceKeepsTheLaterRowInsteadOfThrowing()
    {
        var byId = IAnimeSearchIndex.IndexById([new(7, "First", null, null, null), new(7, "Second", null, null, null)]);

        Assert.Equal("Second", Assert.Single(byId).Value.Title);
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
