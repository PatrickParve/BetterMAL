using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Search;

public class AnimeSearchServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(AnimeTrackerDbContext db, int id, string title, int? popularityRank = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = title, PopularityRank = popularityRank });

    private static void AddSeries(AnimeTrackerDbContext db, int seriesId, int rootAnimeId, string rootTitle, int? rootPopularityRank, int extraMembers = 0)
    {
        AddAnime(db, rootAnimeId, rootTitle, rootPopularityRank);
        db.Series.Add(new SeriesModel { Id = seriesId, RootAnimeId = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = rootAnimeId, SeriesId = seriesId, IsMainLine = true, Order = 0 });

        for (var i = 0; i < extraMembers; i++)
        {
            var extraId = rootAnimeId * 1000 + i + 1;
            AddAnime(db, extraId, $"{rootTitle} Extra {i}");
            db.SeriesMembers.Add(new SeriesMember { AnimeId = extraId, SeriesId = seriesId, IsMainLine = false, Order = i + 1 });
        }
    }

    private static AnimeSearchService CreateService(
        AnimeTrackerDbContext db, List<AnimeTitleProjection>? localIndex = null,
        List<MalAnimeListEdge>? malResults = null, ISeriesBuildTrigger? trigger = null,
        List<AnimeSearchFallbackProjection>? fallbackIndex = null, bool malFails = false) =>
        new(
            new FakeAnimeMetadataRepository(localIndex ?? [], fallbackIndex ?? []),
            new FakeMalClient(malResults ?? [], malFails),
            db,
            new SeriesSearchLookup(db),
            trigger ?? new FakeSeriesBuildTrigger(),
            NullLogger<AnimeSearchService>.Instance);

    private sealed class FakeAnimeMetadataRepository(
        List<AnimeTitleProjection> searchIndex, List<AnimeSearchFallbackProjection> fallbackIndex) : IAnimeMetadataRepository
    {
        public Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default) => Task.FromResult(searchIndex);
        public Task<List<AnimeSearchFallbackProjection>> GetSearchFallbackIndexAsync(CancellationToken ct = default) =>
            Task.FromResult(fallbackIndex);
    }

    // malFails simulates the live MAL search unreachable/erroring — the case
    // AnimeSearchService.SearchMalAsync itself already catches and turns into
    // (Edges: [], Failed: true), so this mirrors that boundary rather than
    // faking the Failed flag directly.
    private sealed class FakeMalClient(List<MalAnimeListEdge> searchResults, bool fails = false) : IMalClient
    {
        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            fails
                ? throw new HttpRequestException("Simulated MAL search failure")
                : Task.FromResult(new MalPagedResponse<MalAnimeListEdge> { Data = searchResults });

        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private static MalAnimeListEdge MalEdge(int id, string title, int index) =>
        new() { Node = new MalAnimeNode { Id = id, Title = title, Popularity = 100 + index } };

    // --- 8.2 Type-ahead composition ---

    [Fact]
    public async Task SearchAsync_SeriesLeadTheDropdownCappedAtTwoOutOfFiveTotalRows()
    {
        using var db = CreateDb();
        AddSeries(db, seriesId: 1, rootAnimeId: 201, rootTitle: "Gundam Series One", rootPopularityRank: 1);
        AddSeries(db, seriesId: 2, rootAnimeId: 202, rootTitle: "Gundam Series Two", rootPopularityRank: 50);
        AddSeries(db, seriesId: 3, rootAnimeId: 203, rootTitle: "Gundam Series Three", rootPopularityRank: 100);
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection>
        {
            new(1, "Gundam A", null, null, 10),
            new(2, "Gundam B", null, null, 20),
            new(3, "Gundam C", null, null, 30),
            new(4, "Gundam D", null, null, 40),
            new(5, "Gundam E", null, null, 60),
        };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("gundam", limit: 5);

        Assert.Equal(5, results.Count);
        Assert.Equal(["series", "series", "anime", "anime", "anime"], results.Select(r => r.Kind));
        // The two strongest (most popular) matching series lead, not the third.
        Assert.Equal([201, 202], results.Where(r => r.Kind == "series").Select(r => r.RootAnimeId));
    }

    [Fact]
    public async Task SearchAsync_NoStoredSeriesLeavesResultsAllAnime()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no Series rows at all -> the cheap empty-index path

        var localIndex = new List<AnimeTitleProjection>
        {
            new(1, "Toradora!", null, null, 1),
            new(2, "Toradora SS", null, null, 2),
        };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("toradora", limit: 5);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal("anime", r.Kind));
        Assert.Equal([1, 2], results.Select(r => r.Id)); // unaffected: most-popular prefix match first
    }

    // --- 8.3 Results-page composition ---

    [Fact]
    public async Task SearchPageAsync_SeriesLeadUnderRelevanceCappedAtThree()
    {
        using var db = CreateDb();
        AddSeries(db, seriesId: 1, rootAnimeId: 301, rootTitle: "Fate Series One", rootPopularityRank: 1);
        AddSeries(db, seriesId: 2, rootAnimeId: 302, rootTitle: "Fate Series Two", rootPopularityRank: 2);
        AddSeries(db, seriesId: 3, rootAnimeId: 303, rootTitle: "Fate Series Three", rootPopularityRank: 3, extraMembers: 2);
        AddSeries(db, seriesId: 4, rootAnimeId: 304, rootTitle: "Fate Series Four", rootPopularityRank: 4);
        await db.SaveChangesAsync();

        var malResults = Enumerable.Range(0, 4).Select(i => MalEdge(1000 + i, $"Fate Anime {i}", i)).ToList();
        var service = CreateService(db, malResults: malResults);

        var page = await service.SearchPageAsync("fate", "relevance", offset: 0, limit: 50);

        Assert.Equal(3, page.Series.Count); // capped, so series 4 is excluded
        Assert.Equal(4, page.Items.Count); // anime candidates unaffected by series
        var withExtras = page.Series.Single(s => s.RootAnimeId == 303);
        Assert.Equal(3, withExtras.EntryCount); // root + 2 extras
    }

    [Theory]
    [InlineData("popularity")]
    [InlineData("malScore")]
    [InlineData("alphabetical")]
    [InlineData("myScore")]
    public async Task SearchPageAsync_SeriesOmittedUnderNonRelevanceSorts(string sortKey)
    {
        using var db = CreateDb();
        AddSeries(db, seriesId: 1, rootAnimeId: 301, rootTitle: "Fate Series One", rootPopularityRank: 1);
        await db.SaveChangesAsync();

        var malResults = Enumerable.Range(0, 4).Select(i => MalEdge(1000 + i, $"Fate Anime {i}", i)).ToList();
        var service = CreateService(db, malResults: malResults);

        var page = await service.SearchPageAsync("fate", sortKey, offset: 0, limit: 50);

        Assert.Empty(page.Series);
        Assert.Equal(4, page.Items.Count); // anime results still present, only series are omitted
    }

    [Fact]
    public async Task SearchPageAsync_TotalCountCountsAnimeOnlyRegardlessOfSeries()
    {
        var malResults = Enumerable.Range(0, 4).Select(i => MalEdge(1000 + i, $"Fate Anime {i}", i)).ToList();

        using var withSeriesDb = CreateDb();
        AddSeries(withSeriesDb, seriesId: 1, rootAnimeId: 301, rootTitle: "Fate Series One", rootPopularityRank: 1);
        await withSeriesDb.SaveChangesAsync();
        var withSeries = await CreateService(withSeriesDb, malResults: malResults).SearchPageAsync("fate", "relevance", 0, 50);

        using var withoutSeriesDb = CreateDb();
        await withoutSeriesDb.SaveChangesAsync();
        var withoutSeries = await CreateService(withoutSeriesDb, malResults: malResults).SearchPageAsync("fate", "relevance", 0, 50);

        Assert.NotEmpty(withSeries.Series);
        Assert.Empty(withoutSeries.Series);
        Assert.Equal(4, withSeries.TotalCount);
        Assert.Equal(withoutSeries.TotalCount, withSeries.TotalCount);
    }

    // --- Search results-page fallback (design.md D7/tasks.md 11.2) ---

    [Fact]
    public async Task SearchPageAsync_FailingMalSearchReturnsLocalMatchesWithFlagTrue()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection>
        {
            new(1, "Naruto", null, null, 1, "tv", 220, 8.0),
        };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("naruto", "relevance", offset: 0, limit: 50);

        Assert.True(page.MalSearchFailed);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Items[0].AnimeId);
    }

    [Fact]
    public async Task SearchPageAsync_SucceedingMalSearchIsUnchangedWithFlagFalse()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var malResults = new List<MalAnimeListEdge> { MalEdge(1, "Naruto", 0) };
        var service = CreateService(db, malResults: malResults, malFails: false);

        var page = await service.SearchPageAsync("naruto", "relevance", offset: 0, limit: 50);

        Assert.False(page.MalSearchFailed);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Items[0].AnimeId);
    }

    [Fact]
    public async Task SearchPageAsync_FailingSearchWithNoLocalMatchReturnsEmptyWithFlagTrue()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection> { new(1, "Bleach", null, null, 1, "tv", 366, 7.9) };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("naruto", "relevance", offset: 0, limit: 50);

        Assert.True(page.MalSearchFailed);
        Assert.Empty(page.Items);
    }

    [Fact]
    public async Task SearchPageAsync_QuotedQueryUnderFallbackMatchesExactly()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection>
        {
            new(1, "Naruto", null, null, 1, "tv", 220, 8.0),
            new(2, "Naruto Shippuden", null, null, 2, "tv", 500, 8.2),
        };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("\"Naruto\"", "relevance", offset: 0, limit: 50);

        Assert.Single(page.Items);
        Assert.Equal(1, page.Items[0].AnimeId);
    }

    [Fact]
    public async Task SearchPageAsync_LocalRelevanceRanksExactThenPrefixThenSubstringEachByPopularity()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection>
        {
            new(1, "Fate", null, null, 50, "tv", 24, 7.5), // exact
            new(2, "Fate Zero", null, null, 10, "tv", 25, 8.5), // prefix, more popular
            new(3, "Fate Apocrypha", null, null, 20, "tv", 25, 7.0), // prefix, less popular
            new(4, "Heaven's Fate", null, null, 5, "tv", 12, 6.5), // substring only, most popular overall
            new(5, "Strange Fate Tales", null, null, 100, "tv", 12, 6.0), // substring only
        };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("Fate", "relevance", offset: 0, limit: 50);

        // Band membership (exact > prefix > substring) dominates popularity —
        // id 4 is the most popular of all five but still ranks after the
        // exact and prefix bands, not ahead of them.
        Assert.Equal([1, 2, 3, 4, 5], page.Items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task SearchPageAsync_FallbackNonRelevanceSortsOrderByMalScore()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection>
        {
            new(1, "Fate Zero", null, null, 1, "tv", 25, 7.0),
            new(2, "Fate Apocrypha", null, null, 2, "tv", 25, 9.0),
            new(3, "Fate Stay Night", null, null, 3, "tv", 24, 8.0),
        };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("Fate", "malScore", offset: 0, limit: 50);

        Assert.Equal([2, 3, 1], page.Items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task SearchPageAsync_SeriesStillRideAlongUnderRelevanceInFallback()
    {
        using var db = CreateDb();
        AddSeries(db, seriesId: 1, rootAnimeId: 301, rootTitle: "Fate Series One", rootPopularityRank: 1);
        await db.SaveChangesAsync();

        var fallback = new List<AnimeSearchFallbackProjection> { new(500, "Fate Anime", null, null, 1, "tv", 24, 7.5) };
        var service = CreateService(db, fallbackIndex: fallback, malFails: true);

        var page = await service.SearchPageAsync("fate", "relevance", offset: 0, limit: 50);

        Assert.True(page.MalSearchFailed);
        Assert.Single(page.Series);
        Assert.Equal(301, page.Series[0].RootAnimeId);
    }

    // --- 8.4 Background series build trigger ---

    [Fact]
    public async Task SearchAsync_EnqueuesTopMatchWhenItHasNoStoredSeries()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(500, "Gundam Unicorn", null, null, 1) };
        var trigger = new FakeSeriesBuildTrigger();
        var service = CreateService(db, localIndex, trigger: trigger);

        var results = await service.SearchAsync("gundam", limit: 5);

        Assert.Equal([500], trigger.Enqueued);
        Assert.Single(results); // scheduling never alters the returned results
    }

    [Fact]
    public async Task SearchAsync_SkipsEnqueueWhenTopMatchAlreadyHasStoredSeries()
    {
        using var db = CreateDb();
        AddSeries(db, seriesId: 1, rootAnimeId: 500, rootTitle: "Gundam Unicorn", rootPopularityRank: 1);
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(500, "Gundam Unicorn", null, null, 1) };
        var trigger = new FakeSeriesBuildTrigger();
        var service = CreateService(db, localIndex, trigger: trigger);

        await service.SearchAsync("gundam", limit: 5);

        Assert.Empty(trigger.Enqueued);
    }

    [Fact]
    public async Task SearchAsync_SkipsEnqueueForQueriesShorterThanThreeCharacters()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(500, "Gundam Unicorn", null, null, 1) };
        var trigger = new FakeSeriesBuildTrigger();
        var service = CreateService(db, localIndex, trigger: trigger);

        await service.SearchAsync("gu", limit: 5);

        Assert.Empty(trigger.Enqueued);
    }

    [Fact]
    public async Task SearchAsync_DedupesRepeatedEnqueuesAndNeverThrows()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(500, "Gundam Unicorn", null, null, 1) };
        var trigger = new SeriesBuildTrigger(); // the real dedupe implementation, not the fake
        var service = CreateService(db, localIndex, trigger: trigger);

        for (var i = 0; i < 5; i++)
            await service.SearchAsync("gundam", limit: 5); // repeated "keystrokes" of the same query

        using var firstWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        Assert.Equal(500, await trigger.WaitAsync(firstWait.Token));

        using var secondWait = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => trigger.WaitAsync(secondWait.Token));
    }

    // --- Normalized matching (design.md D1-D3, tasks.md 1.7) ---

    [Fact]
    public async Task SearchAsync_SpacingIsIgnored()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(1, "Fullmetal Alchemist", null, null, 1) };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("full metal", limit: 5);

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }

    [Fact]
    public async Task SearchAsync_PunctuationIsIgnoredForSpacedAndUnspacedQueries()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(1, "Re:ZERO -Starting Life in Another World-", null, null, 1) };
        var service = CreateService(db, localIndex);

        var reZero = await service.SearchAsync("re zero", limit: 5);
        var rezero = await service.SearchAsync("rezero", limit: 5);

        Assert.Single(reZero);
        Assert.Single(rezero);
    }

    [Fact]
    public async Task SearchAsync_AccentsMatchTheirUnaccentedSpellingBothWays()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(1, "Kimi ni Todoke: Yūki no Ippo", null, null, 1) };
        var service = CreateService(db, localIndex);

        var unaccentedQuery = await service.SearchAsync("Yuki no Ippo", limit: 5);

        Assert.Single(unaccentedQuery);

        var accentedIndex = new List<AnimeTitleProjection> { new(1, "Yuki no Ippo", null, null, 1) };
        var accentedService = CreateService(db, accentedIndex);
        var accentedQuery = await accentedService.SearchAsync("Yūki no Ippo", limit: 5);

        Assert.Single(accentedQuery);
    }

    [Fact]
    public async Task SearchAsync_QuotedQueryMatchesUnderNormalizationButNotASubstringTitle()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection>
        {
            new(1, "Fullmetal Alchemist", null, null, 1),
            new(2, "Fullmetal Alchemist: Brotherhood", null, null, 2),
        };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("\"fullmetal alchemist\"", limit: 5);

        Assert.Single(results);
        Assert.Equal(1, results[0].Id);
    }

    [Fact]
    public async Task SearchAsync_PunctuationOnlyQueryReturnsNoResults()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection> { new(1, "Fullmetal Alchemist", null, null, 1) };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("!!!", limit: 5);

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_PrefixMatchesStillLeadContainsMatchesUnderNormalization()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();

        var localIndex = new List<AnimeTitleProjection>
        {
            new(1, "Kaguya-sama: Love is War", null, null, 1), // prefix once de-punctuated
            new(2, "Petition for Kaguya-sama's Happiness", null, null, 2), // contains only
        };
        var service = CreateService(db, localIndex);

        var results = await service.SearchAsync("kaguya sama", limit: 5);

        Assert.Equal([1, 2], results.Select(r => r.Id));
    }
}
