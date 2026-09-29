using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Library;

// TopAnimeService per-ranking-list isolation (design.md D3, tasks 2.2/2.3):
// each selectable list has its own daily fetch clock, its own cached rows,
// and its own single-flight guard, so viewing/refreshing one list never
// affects another.
public class TopAnimeServiceTests
{
    // A shared name gives two contexts the same in-memory store, which is how
    // two requests each get their own scoped DbContext over one database.
    private static AnimeTrackerDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    private static TopAnimeService CreateService(
        AnimeTrackerDbContext db, FakeMalClient malClient, Dictionary<int, int>? airedSoFar = null,
        IAnimeSearchIndex? searchIndex = null, RefreshGate? refreshGate = null) =>
        new(
            db,
            malClient,
            new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), new SeriesBuildTrigger()),
            new TopAnimeRepository(db),
            new FakeEpisodeScheduleService(airedSoFar),
            new FakeBroadcastLocalTimeConverter(),
            refreshGate ?? new RefreshGate(),
            searchIndex ?? new FakeAnimeSearchIndex(),
            NullLogger<TopAnimeService>.Instance);

    private static MalAnimeListEdge Edge(int id, int rank, int? numEpisodes = null) =>
        new() { Node = new MalAnimeNode { Id = id, Title = $"Anime {id}", NumEpisodes = numEpisodes }, Ranking = new MalRankingInfo { Rank = rank } };

    [Fact]
    public async Task RefreshAsync_FetchingOneListDoesNotMarkAnotherAsFetchedToday()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);

        // Movie has never been fetched, so refreshing it live-fetches too — it
        // was not marked fresh by All's fetch.
        await service.RefreshAsync(TopAnimeRankingType.Movie);

        Assert.Equal(["all", "movie"], malClient.RankingCalls);
    }

    // simplify-settings-and-first-fetch-states design D1: a never-fetched list
    // costs one MAL request however many callers ask at once. The second
    // caller meets the first at the RefreshGate and re-checks freshness once
    // it is in, so it is answered Skipped.
    [Fact]
    public async Task RefreshAsync_TwoOverlappingCallsOnANeverFetchedListFetchOnceAndReportFetchedThenSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var firstDb = CreateDb(dbName);
        using var secondDb = CreateDb(dbName);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        }, release: release);
        var gate = new RefreshGate();
        var firstService = CreateService(firstDb, malClient, refreshGate: gate);
        var secondService = CreateService(secondDb, malClient, refreshGate: gate);

        var firstCall = firstService.RefreshAsync(TopAnimeRankingType.All);
        await malClient.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); // the first holds the gate, blocked inside MAL

        var secondCall = secondService.RefreshAsync(TopAnimeRankingType.All);
        await Task.Delay(100);
        Assert.False(secondCall.IsCompleted); // waiting on the gate, not fetching

        release.SetResult();
        var first = await firstCall.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await secondCall.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(TopAnimeRefreshOutcome.Fetched, first.Outcome);
        Assert.Equal(TopAnimeRefreshOutcome.Skipped, second.Outcome);
        Assert.Single(malClient.RankingCalls);
    }

    [Fact]
    public async Task RefreshAsync_RefreshingOneListLeavesAnotherListsCachedRowsIntact()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1), Edge(2, 2)],
            ["movie"] = [Edge(3, 1)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);
        var allBeforeMovie = await service.GetRankingAsync(TopAnimeRankingType.All);
        Assert.Equal([1, 2], allBeforeMovie.Select(i => i.AnimeId));

        await service.RefreshAsync(TopAnimeRankingType.Movie);

        // All's rows are untouched by Movie's fetch — same two anime, same ranks.
        var allRows = await db.TopAnimeRankingEntries.Where(r => r.RankingType == "all").ToListAsync();
        Assert.Equal(2, allRows.Count);
        Assert.Equal([1, 2], allRows.OrderBy(r => r.Rank).Select(r => r.AnimeId));

        var movieRows = await db.TopAnimeRankingEntries.Where(r => r.RankingType == "movie").ToListAsync();
        Assert.Single(movieRows);
        Assert.Equal(3, movieRows[0].AnimeId);
    }

    [Fact]
    public async Task RefreshAsync_SameDayRevisitMakesNoMalCall()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);
        Assert.Single(malClient.RankingCalls);

        await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Single(malClient.RankingCalls); // still just the one fetch
    }

    [Fact]
    public async Task RefreshAsync_NewDayRevisitFetchesAgain()
    {
        using var db = CreateDb();
        db.TopAnimeFetchLogs.Add(new TopAnimeFetchLog
        {
            RankingType = "all",
            LastFetchedAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Single(malClient.RankingCalls);
    }

    [Fact]
    public async Task RefreshAsync_FailedFetchLeavesTheListUnmarkedAndStillServesCache()
    {
        using var db = CreateDb();
        // Seed yesterday's successfully-cached row directly, bypassing the
        // (failing) MAL client, so there's something to serve from cache.
        var yesterday = DateTimeOffset.UtcNow.AddDays(-1);
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        db.TopAnimeRankingEntries.Add(new TopAnimeRankingEntry { RankingType = "all", AnimeId = 1, Rank = 1 });
        db.TopAnimeFetchLogs.Add(new TopAnimeFetchLog { RankingType = "all", LastFetchedAt = yesterday });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(failing: ["all"]);
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);

        // Cache is still served despite the failed live fetch.
        var result = await service.GetRankingAsync(TopAnimeRankingType.All);
        Assert.Single(result);
        Assert.Equal(1, result[0].AnimeId);

        // The failure did not mark the list as fetched for today — the log
        // row is unchanged from yesterday's seed.
        var log = await db.TopAnimeFetchLogs.SingleAsync(f => f.RankingType == "all");
        Assert.Equal(yesterday, log.LastFetchedAt);
    }

    // The core of this change: reading a list is a pure cache read, however
    // stale that cache is — only RefreshAsync ever calls MAL.
    [Fact]
    public async Task GetRankingAsync_MakesNoMalCallEvenWhenTheCacheIsStaleByDays()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        db.TopAnimeRankingEntries.Add(new TopAnimeRankingEntry { RankingType = "all", AnimeId = 1, Rank = 1 });
        db.TopAnimeFetchLogs.Add(new TopAnimeFetchLog { RankingType = "all", LastFetchedAt = DateTimeOffset.UtcNow.AddDays(-30) });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        var result = await service.GetRankingAsync(TopAnimeRankingType.All);

        Assert.Single(result);
        Assert.Empty(malClient.RankingCalls);
    }

    [Fact]
    public async Task RefreshAsync_ReportsFetchedOnAFirstFetch()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Equal(TopAnimeRefreshOutcome.Fetched, result.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_ReportsSkippedOnASameDaySecondCall()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);
        var result = await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Equal(TopAnimeRefreshOutcome.Skipped, result.Outcome);
    }

    [Fact]
    public async Task RefreshAsync_ReportsFailedWhenTheMalClientThrows()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(failing: ["all"]);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Equal(TopAnimeRefreshOutcome.Failed, result.Outcome);
    }

    // gate-editing-on-aired-episodes task 5.8: TopAnimeItemDto carries the
    // same aired-episode facts as the dashboard's currently-watching cards.
    [Fact]
    public async Task GetRankingAsync_RowsCarryAiringStatusAndEpisodesAired()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "currently_airing" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient, airedSoFar: new Dictionary<int, int> { [1] = 7 });

        await service.RefreshAsync(TopAnimeRankingType.All);
        var result = await service.GetRankingAsync(TopAnimeRankingType.All);

        var row = Assert.Single(result);
        Assert.Equal("currently_airing", row.AiringStatus);
        Assert.Equal(7, row.EpisodesAired);
    }

    // anime-updates spec ("Nothing is recorded for an anime outside my list
    // and its direct relations"; scope-updates-to-my-list tasks 6.7): a
    // ranking refresh's lean write still detects the episode count it
    // reveals, but the gate records nothing for the anime that makes up the
    // bulk of a ranking list — one with no list entry and no link to one.
    [Fact]
    public async Task RefreshAsync_FullyFetchedStrangerRecordsNoUpdate()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1, numEpisodes: 12)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task RefreshAsync_FullyFetchedLinkedAnimeStillRecordsItsEpisodeCountReveal()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1, numEpisodes: 12)],
        });
        var service = CreateService(db, malClient);

        await service.RefreshAsync(TopAnimeRankingType.All);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
    }

    // --- cache-type-ahead-search-index tasks.md 5.2: a refresh invalidates the search index ---

    [Fact]
    public async Task RefreshAsync_CachingANotYetSeenAnimeInvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, malClient, searchIndex: searchIndex);

        await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task RefreshAsync_LeanlyRewritingAnExistingRowInvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var searchIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, malClient, searchIndex: searchIndex);

        await service.RefreshAsync(TopAnimeRankingType.All);

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    private sealed class FakeEpisodeScheduleService(Dictionary<int, int>? airedSoFar = null) : IEpisodeScheduleService
    {
        private readonly Dictionary<int, int> _airedSoFar = airedSoFar ?? [];

        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(anime.Where(a => _airedSoFar.ContainsKey(a.Id)).ToDictionary(a => a.Id, a => _airedSoFar[a.Id]));
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(animeIds.Where(_airedSoFar.ContainsKey).ToDictionary(id => id, id => _airedSoFar[id]));
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }

    // With a release source, GetRankingAsync answers only once it is set —
    // Entered marks the moment a caller is inside MAL, so a test can start a
    // second caller while the first is held there.
    private sealed class FakeMalClient(
        Dictionary<string, List<MalAnimeListEdge>>? rankings = null,
        HashSet<string>? failing = null,
        TaskCompletionSource? release = null) : IMalClient
    {
        private readonly Dictionary<string, List<MalAnimeListEdge>> _rankings = rankings ?? [];
        private readonly HashSet<string> _failing = failing ?? [];

        public List<string> RankingCalls { get; } = [];
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default)
        {
            RankingCalls.Add(rankingType);
            Entered.TrySetResult();
            if (_failing.Contains(rankingType))
                throw new InvalidOperationException($"Simulated MAL failure for ranking type '{rankingType}'.");

            var edges = _rankings.GetValueOrDefault(rankingType, []);
            var response = new MalPagedResponse<MalAnimeListEdge> { Data = edges };
            return release is null ? Task.FromResult(response) : AnswerOnceReleased();

            async Task<MalPagedResponse<MalAnimeListEdge>> AnswerOnceReleased()
            {
                await release.Task;
                return response;
            }
        }

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
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
}
