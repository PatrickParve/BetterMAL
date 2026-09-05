using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;

namespace AnimeTracker.Api.Tests.Services.Mal;

// design.md D7, tasks.md 2.5 — the 60-candidate live search cache shared by
// the type-ahead's merged stage and the results page.
public class MalSearchCacheTests
{
    private static MalAnimeListEdge Edge(int id, string title) =>
        new() { Node = new MalAnimeNode { Id = id, Title = title } };

    [Fact]
    public void GetWithinTtlReturnsTheHeldEntry()
    {
        var cache = new MalSearchCache(TimeSpan.FromMinutes(1));
        var edges = new List<MalAnimeListEdge> { Edge(1, "Naruto") };

        cache.Set("naruto", edges);

        Assert.Same(edges, cache.Get("naruto"));
    }

    [Fact]
    public async Task AnExpiredEntryIsNotReturned()
    {
        var cache = new MalSearchCache(TimeSpan.FromMilliseconds(10));
        cache.Set("naruto", [Edge(1, "Naruto")]);

        await Task.Delay(50);

        Assert.Null(cache.Get("naruto"));
    }

    [Fact]
    public void ADistinctQueryIsADistinctEntry()
    {
        var cache = new MalSearchCache(TimeSpan.FromMinutes(1));
        cache.Set("naruto", [Edge(1, "Naruto")]);
        cache.Set("bleach", [Edge(2, "Bleach")]);

        Assert.Equal(1, cache.Get("naruto")!.Single().Node.Id);
        Assert.Equal(2, cache.Get("bleach")!.Single().Node.Id);
    }

    [Fact]
    public void TheCapEvictsTheOldestEntry()
    {
        var cache = new MalSearchCache(TimeSpan.FromMinutes(1));
        for (var i = 0; i < 50; i++)
            cache.Set($"query{i}", [Edge(i, $"Anime {i}")]);

        // "query0" was the first written and is now the oldest by expiry.
        cache.Set("query50", [Edge(50, "Anime 50")]);

        Assert.Null(cache.Get("query0"));
        Assert.NotNull(cache.Get("query50"));
        Assert.NotNull(cache.Get("query1"));
    }
}
