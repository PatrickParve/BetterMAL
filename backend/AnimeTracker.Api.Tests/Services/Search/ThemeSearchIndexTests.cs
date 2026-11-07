using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Search;

// improve-search-page design.md D6, task 3.3: ThemeSearchIndex follows the pattern
// AnimeSearchIndexCacheTests holds AnimeSearchIndexCache to: one build for concurrent readers,
// invalidation rather than a clock, and a write that lands mid-rebuild leaving the copy stale.
public class ThemeSearchIndexTests
{
    private static AnimeTrackerDbContext CreateDb() => ThemeSearchTestKit.CreateDb();

    private static async Task<CountingIndex> IndexOverAsync(AnimeTrackerDbContext db, params AnimeTheme[] rows)
    {
        db.AnimeThemes.AddRange(rows);
        await db.SaveChangesAsync();
        return new CountingIndex(new ThemeSearchTestKit.DbScopeFactory(db));
    }

    [Fact]
    public async Task EveryCatalogueRowIsHeld_WithItsThreeTextsNormalized()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db,
            StoredThemes.Row(1, 10, songTitle: "Blue-Bird", artists: ["Ikimonogakari", "Kenji Ōtsuki"], animeName: "Naruto: Shippuuden"),
            StoredThemes.Row(2, 20, songTitle: null, artists: [], animeName: null));

        var entries = await index.GetAsync();

        Assert.Equal(2, entries.Count);
        var first = entries.Single(e => e.Row.AnimeId == 1);
        Assert.Equal("bluebird", first.SongTitleKey);
        Assert.Equal(["ikimonogakari", "kenjiotsuki"], first.ArtistKeys);
        Assert.Equal("narutoshippuuden", first.AnimeNameKey);
        Assert.Equal("Blue-Bird", first.Row.SongTitle); // the row itself is untouched

        var bare = entries.Single(e => e.Row.AnimeId == 2);
        Assert.Null(bare.SongTitleKey);
        Assert.Empty(bare.ArtistKeys);
        Assert.Null(bare.AnimeNameKey);
    }

    [Fact]
    public async Task TwoReadsWithNoInvalidateLoadOnce()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10));

        var first = await index.GetAsync();
        var second = await index.GetAsync();

        Assert.Equal(1, index.LoadCount);
        Assert.Same(first, second);
    }

    [Fact]
    public async Task NeverExpiresOnItsOwnRegardlessOfTimePassing()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10));

        await index.GetAsync();
        await Task.Yield();
        await index.GetAsync();

        Assert.Equal(1, index.LoadCount);
    }

    [Fact]
    public async Task ConcurrentColdReadsLoadOnceAndAllSeeTheCompleteCatalogue()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10), StoredThemes.Row(2, 20), StoredThemes.Row(3, 30));
        index.Gate = new TaskCompletionSource();

        // Each call runs up to its first real await synchronously: the first is inside the load,
        // held on the gate, and the rest are queued behind the rebuild lock.
        var pending = Enumerable.Range(0, 5).Select(_ => index.GetAsync()).ToList();
        index.Gate.SetResult();
        var results = await Task.WhenAll(pending);

        Assert.Equal(1, index.LoadCount);
        Assert.All(results, r => Assert.Equal(3, r.Count));
    }

    [Fact]
    public async Task InvalidateMakesTheNextReadRebuildAndIncludeTheWrite()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10));
        Assert.Single(await index.GetAsync());

        db.AnimeThemes.Add(StoredThemes.Row(2, 20));
        await db.SaveChangesAsync();
        Assert.Single(await index.GetAsync()); // nothing says it is stale yet
        index.Invalidate();

        Assert.Equal(2, (await index.GetAsync()).Count);
        Assert.Equal(2, index.LoadCount);

        // Nothing changed since, so a third read must not load again.
        Assert.Equal(2, (await index.GetAsync()).Count);
        Assert.Equal(2, index.LoadCount);
    }

    [Fact]
    public async Task AWriteThatCommitsMidRebuildLeavesThePublishedCopyStale()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10));
        index.Gate = new TaskCompletionSource();

        var inFlight = index.GetAsync(); // held mid-rebuild, having captured generation 0

        db.AnimeThemes.Add(StoredThemes.Row(2, 20));
        await db.SaveChangesAsync();
        index.Invalidate(); // the refresh committed while the rebuild above was still reading

        index.Gate.SetResult();
        await inFlight; // published, but tagged with the generation that is already behind

        index.Gate = null;
        var next = await index.GetAsync();

        Assert.Equal(2, next.Count); // the next read does not trust the stale copy
        Assert.Equal(2, index.LoadCount); // the in-flight load, and this one: never a third
    }

    [Fact]
    public async Task InvalidateWaitsForNothing()
    {
        using var db = CreateDb();
        var index = await IndexOverAsync(db, StoredThemes.Row(1, 10));
        index.Gate = new TaskCompletionSource();
        var inFlight = index.GetAsync();

        index.Invalidate(); // returns while the rebuild is still held: the refresh never waits on a search

        Assert.False(inFlight.IsCompleted);
        index.Gate.SetResult();
        await inFlight;
    }

    // Counts the loads and can hold one open, but reads through the real code underneath.
    private sealed class CountingIndex(IServiceScopeFactory scopeFactory) : ThemeSearchIndex(scopeFactory)
    {
        public int LoadCount { get; private set; }
        public TaskCompletionSource? Gate { get; set; }

        protected override async Task<List<AnimeTheme>> LoadRowsAsync(CancellationToken ct)
        {
            LoadCount++;
            if (Gate is { } gate)
                await gate.Task;
            return await base.LoadRowsAsync(ct);
        }
    }
}
