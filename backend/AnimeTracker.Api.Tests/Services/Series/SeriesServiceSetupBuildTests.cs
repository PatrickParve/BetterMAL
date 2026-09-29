using System.Collections.Concurrent;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Metadata;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// ISeriesService.BuildForSetupAsync (first-run-setup design.md D9, series-page
// "A setup build has no budget"): the series step's own entry point. Same
// gate, same "does it need a build" check and same fall-back to what is
// stored as a page visit, but with no fetch or probe budget, and no
// projection.
public class SeriesServiceSetupBuildTests
{
    private static DbContextOptions<AnimeTrackerDbContext> NewStore() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static AnimeMetadata Anime(int id, DateOnly airedFrom, string mediaType = "tv") => new()
    {
        Id = id,
        Title = $"Anime {id}",
        MediaType = mediaType,
        AiredFrom = airedFrom,
    };

    private static DateOnly Aired(int index) => new DateOnly(2000, 1, 1).AddDays(index * 30);

    private static void Relate(AnimeMetadata from, int toId, string relationType) =>
        from.RelatedAnime.Add(new AnimeRelatedAnime
        {
            AnimeId = from.Id,
            RelatedAnimeId = toId,
            RelationType = relationType,
            Title = $"Anime {toId}",
        });

    // A strict sequel chain 1 -> 2 -> ... -> length. Anime 1 is what a caller
    // seeds; whoever the caller stores in the database is cached, the rest is
    // what the fake "MyAnimeList" serves.
    private static Func<int, AnimeMetadata?> Chain(int length) => id =>
    {
        if (id < 1 || id > length)
            return null;
        var anime = Anime(id, Aired(id));
        if (id < length)
            Relate(anime, id + 1, "sequel");
        return anime;
    };

    // The second context in the concurrency test is another scope's DbContext
    // on the same store, as in the app.
    private static SeriesService CreateService(
        AnimeTrackerDbContext db, IMetadataRefreshService refresh, RefreshGate? gate = null) =>
        new(
            db,
            new SeriesGraphBuilder(db, refresh, new RelationResolver(db), NullLogger<SeriesGraphBuilder>.Instance),
            gate ?? new RefreshGate(),
            new ProjectionMustNotRun(),
            new ProjectionMustNotRun(),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""),
            TestIdMappings.Resolver(db));

    private static async Task SeedAsync(DbContextOptions<AnimeTrackerDbContext> store, params AnimeMetadata[] anime)
    {
        await using var db = new AnimeTrackerDbContext(store);
        db.AnimeMetadata.AddRange(anime);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnUncachedTwentyMemberFranchiseWithTenFarOtherEndsIsBuiltInOneCall()
    {
        var store = NewStore();
        var seed = Chain(20)(1)!;
        for (var farEnd = 101; farEnd <= 110; farEnd++)
            Relate(seed, farEnd, "other");
        await SeedAsync(store, seed);

        // Members 2-20 are uncached and so are the ten `other` far ends.
        var refresh = new CatalogueRefreshService(store, id => id is >= 101 and <= 110
            ? Anime(id, Aired(id), mediaType: "cm")
            : Chain(20)(id));
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Settled, outcome);
        // 19 members fetched and 10 far ends probed, in this one call: a visit
        // build would have stopped at 8 and 4 and stored the series partial.
        Assert.Equal(29, refresh.Calls.Count);
        var series = await db.Series.AsNoTracking().SingleAsync();
        Assert.False(series.IsPartial);
        Assert.False(series.IsTruncated);
        Assert.Equal(20, await db.SeriesMembers.CountAsync(m => m.SeriesId == series.Id));
    }

    [Fact]
    public async Task TheMemberCapStillTruncatesAnUnboundedBuild()
    {
        var store = NewStore();
        const int chainLength = SeriesGraphBuilder.MemberCap + 50;
        // All cached, so the cap is what stops the traversal and nothing is fetched.
        await SeedAsync(store, Enumerable.Range(1, chainLength).Select(id => Chain(chainLength)(id)!).ToArray());
        var refresh = new CatalogueRefreshService(store, _ => null);
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        // A truncated series is a finished build, not a failed one.
        Assert.Equal(SetupBuildOutcome.Settled, outcome);
        var series = await db.Series.AsNoTracking().SingleAsync();
        Assert.True(series.IsTruncated);
        Assert.False(series.IsPartial);
        Assert.Equal(SeriesGraphBuilder.MemberCap, await db.SeriesMembers.CountAsync(m => m.SeriesId == series.Id));
        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task AFailedMemberFetchGivesPartial()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(4)(1)!, Chain(4)(2)!);
        // 3 is where the traversal needs a fetch, and MAL is timing out.
        var refresh = new CatalogueRefreshService(store, Chain(4)) { FailWith = { [3] = new TimeoutException("MAL timed out") } };
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Partial, outcome);
        Assert.True((await db.Series.AsNoTracking().SingleAsync()).IsPartial);
    }

    // The seed's only neighbour is what failed, so the traversal finds one
    // anime and stores no series. That must not read as "alone": a lone anime
    // is settled for good, a failed fetch is tried again.
    [Fact]
    public async Task ASeedThatLooksAloneOnlyBecauseItsNeighbourFailedGivesPartialNotNoSeries()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(2)(1)!);
        var refresh = new CatalogueRefreshService(store, Chain(2)) { FailWith = { [2] = new HttpRequestException("MAL is down") } };
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Partial, outcome);
        Assert.Empty(await db.Series.ToListAsync());
    }

    [Fact]
    public async Task AFailedBuildIsSettledByTheNextCallOnceMalAnswers()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(3)(1)!, Chain(3)(2)!);
        var refresh = new CatalogueRefreshService(store, Chain(3)) { FailWith = { [3] = new TimeoutException("MAL timed out") } };
        await using var db = new AnimeTrackerDbContext(store);
        var service = CreateService(db, refresh);
        Assert.Equal(SetupBuildOutcome.Partial, await service.BuildForSetupAsync(1));

        refresh.FailWith.Clear();
        var retried = await service.BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Settled, retried);
        Assert.False((await db.Series.AsNoTracking().SingleAsync()).IsPartial);
    }

    [Fact]
    public async Task A404MemberDoesNotMakeItPartial()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(4)(1)!, Chain(4)(2)!);
        // MyAnimeList has no anime 3: the catalogue answers 404 for it, so the
        // series is built from what exists and isn't held partial on its account.
        var refresh = new CatalogueRefreshService(store, id => id == 3 ? null : Chain(4)(id));
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Settled, outcome);
        Assert.Equal([3], refresh.Calls);
        Assert.False((await db.Series.AsNoTracking().SingleAsync()).IsPartial);
    }

    [Fact]
    public async Task ALoneAnimeGivesNoSeries()
    {
        var store = NewStore();
        await SeedAsync(store, Anime(1, Aired(1)));
        var refresh = new CatalogueRefreshService(store, _ => null);
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.NoSeries, outcome);
        Assert.Empty(await db.Series.ToListAsync());
        // Not asserted: whether the seed is fetched. The builder reads a row
        // with no relation rows as lean and re-fetches it once (here MAL has
        // no such anime, so the cached row is used), lone or not.
    }

    [Fact]
    public async Task AnUpToDateSeriesIsNotRebuilt()
    {
        var store = NewStore();
        var builtAt = SeriesGraphBuilder.ClassificationRevisedAt + TimeSpan.FromDays(1);
        await SeedAsync(store, Chain(2)(1)!, Chain(2)(2)!);
        await using (var seedDb = new AnimeTrackerDbContext(store))
        {
            seedDb.Series.Add(new SeriesModel { Id = 1, BuiltAt = builtAt });
            seedDb.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0, IsPrimary = true });
            seedDb.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = true, Order = 1, IsPrimary = true });
            await seedDb.SaveChangesAsync();
        }
        var refresh = new CatalogueRefreshService(store, _ => null);
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, refresh).BuildForSetupAsync(2);

        Assert.Equal(SetupBuildOutcome.Settled, outcome);
        Assert.Equal(builtAt, (await db.Series.AsNoTracking().SingleAsync()).BuiltAt); // unchanged: no rebuild
        Assert.Empty(refresh.Calls);
    }

    [Fact]
    public async Task APartialStoredSeriesIsRebuilt()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(3)(1)!, Chain(3)(2)!, Chain(3)(3)!);
        await using (var seedDb = new AnimeTrackerDbContext(store))
        {
            seedDb.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow, IsPartial = true });
            seedDb.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0, IsPrimary = true });
            await seedDb.SaveChangesAsync();
        }
        await using var db = new AnimeTrackerDbContext(store);

        var outcome = await CreateService(db, new CatalogueRefreshService(store, _ => null)).BuildForSetupAsync(1);

        Assert.Equal(SetupBuildOutcome.Settled, outcome);
        var series = await db.Series.AsNoTracking().SingleAsync();
        Assert.False(series.IsPartial);
        Assert.Equal(3, await db.SeriesMembers.CountAsync(m => m.SeriesId == series.Id));
    }

    // Two scopes, one gate (the app's RefreshGate is a singleton): the second
    // call waits on the first's key and then finds its finished series.
    [Fact]
    public async Task TwoConcurrentCallsBuildOnce()
    {
        var store = NewStore();
        await SeedAsync(store, Chain(3)(1)!);
        var firstFetchStarted = new TaskCompletionSource();
        var releaseFirstFetch = new TaskCompletionSource();
        var refresh = new CatalogueRefreshService(store, Chain(3))
        {
            // Parks the first build inside its first fetch, holding the gate.
            BeforeFetch = async _ =>
            {
                firstFetchStarted.TrySetResult();
                await releaseFirstFetch.Task;
            },
        };
        var gate = new RefreshGate();
        await using var firstDb = new AnimeTrackerDbContext(store);
        await using var secondDb = new AnimeTrackerDbContext(store);

        var first = CreateService(firstDb, refresh, gate).BuildForSetupAsync(1);
        await firstFetchStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var second = CreateService(secondDb, refresh, gate).BuildForSetupAsync(1);

        // Were the second call not held by the gate it would start its own
        // build and fetch anime 2 as well, within a few milliseconds.
        await Task.Delay(200);
        Assert.False(second.IsCompleted);
        Assert.Equal([2], refresh.Calls);

        releaseFirstFetch.SetResult();
        Assert.Equal(SetupBuildOutcome.Settled, await first.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal(SetupBuildOutcome.Settled, await second.WaitAsync(TimeSpan.FromSeconds(5)));
        Assert.Equal([2, 3], refresh.Calls.Order()); // each fetched once, by the first build only
        Assert.Single(await secondDb.Series.AsNoTracking().ToListAsync());
    }

    // Stands in for a live MAL fetch: serves `catalogue`, an anime it doesn't
    // have is a 404, and an id in FailWith fails the way a timeout or an
    // outage would. Writes through its own context, as the real service does
    // with the scope's.
    private sealed class CatalogueRefreshService(
        DbContextOptions<AnimeTrackerDbContext> store, Func<int, AnimeMetadata?> catalogue) : IMetadataRefreshService
    {
        private readonly ConcurrentQueue<int> _calls = new();
        public IReadOnlyCollection<int> Calls => _calls;
        public Dictionary<int, Exception> FailWith { get; } = new();
        public Func<int, Task>? BeforeFetch { get; init; }

        public Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task RefreshOneAsync(int animeId, CancellationToken ct = default)
        {
            _calls.Enqueue(animeId);
            if (BeforeFetch is not null)
                await BeforeFetch(animeId);
            if (FailWith.TryGetValue(animeId, out var failure))
                throw failure;

            var anime = catalogue(animeId) ?? throw new AnimeMetadataNotFoundException(animeId);
            await using var db = new AnimeTrackerDbContext(store);
            if (!await db.AnimeMetadata.AnyAsync(a => a.Id == animeId, ct))
            {
                db.AnimeMetadata.Add(anime);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // BuildForSetupAsync skips the projection, and the projection is what
    // reads the ranking and the episode schedule, so a call to either fails
    // the test.
    private sealed class ProjectionMustNotRun : IEpisodeScheduleService, IAnimeRankingService
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

        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
