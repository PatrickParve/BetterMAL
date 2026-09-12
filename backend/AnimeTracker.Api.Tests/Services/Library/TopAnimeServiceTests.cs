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
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Library;

// TopAnimeService per-ranking-list isolation (design.md D3, tasks 2.2/2.3):
// each selectable list has its own daily fetch clock, its own cached rows,
// and its own single-flight guard, so viewing/refreshing one list never
// affects another.
public class TopAnimeServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TopAnimeService CreateService(AnimeTrackerDbContext db, FakeMalClient malClient, Dictionary<int, int>? airedSoFar = null) =>
        new(
            db,
            malClient,
            new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), new SeriesBuildTrigger()),
            new TopAnimeRepository(db),
            new FakeEpisodeScheduleService(airedSoFar),
            new FakeBroadcastLocalTimeConverter(),
            new RefreshGate(),
            NullLogger<TopAnimeService>.Instance);

    private static MalAnimeListEdge Edge(int id, int rank, int? numEpisodes = null) =>
        new() { Node = new MalAnimeNode { Id = id, Title = $"Anime {id}", NumEpisodes = numEpisodes }, Ranking = new MalRankingInfo { Rank = rank } };

    [Fact]
    public async Task GetRankingAsync_FetchingOneListDoesNotMarkAnotherAsFetchedToday()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.GetRankingAsync(TopAnimeRankingType.All);

        // Movie has never been fetched, so viewing it live-fetches too — it
        // was not marked fresh by All's fetch.
        await service.GetRankingAsync(TopAnimeRankingType.Movie);

        Assert.Equal(["all", "movie"], malClient.RankingCalls);
    }

    [Fact]
    public async Task GetRankingAsync_RefreshingOneListLeavesAnotherListsCachedRowsIntact()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1), Edge(2, 2)],
            ["movie"] = [Edge(3, 1)],
        });
        var service = CreateService(db, malClient);

        var allBeforeMovie = await service.GetRankingAsync(TopAnimeRankingType.All);
        Assert.Equal([1, 2], allBeforeMovie.Select(i => i.AnimeId));

        await service.GetRankingAsync(TopAnimeRankingType.Movie);

        // All's rows are untouched by Movie's fetch — same two anime, same ranks.
        var allRows = await db.TopAnimeRankingEntries.Where(r => r.RankingType == "all").ToListAsync();
        Assert.Equal(2, allRows.Count);
        Assert.Equal([1, 2], allRows.OrderBy(r => r.Rank).Select(r => r.AnimeId));

        var movieRows = await db.TopAnimeRankingEntries.Where(r => r.RankingType == "movie").ToListAsync();
        Assert.Single(movieRows);
        Assert.Equal(3, movieRows[0].AnimeId);
    }

    [Fact]
    public async Task GetRankingAsync_SameDayRevisitMakesNoMalCall()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1)],
        });
        var service = CreateService(db, malClient);

        await service.GetRankingAsync(TopAnimeRankingType.All);
        Assert.Single(malClient.RankingCalls);

        await service.GetRankingAsync(TopAnimeRankingType.All);

        Assert.Single(malClient.RankingCalls); // still just the one fetch
    }

    [Fact]
    public async Task GetRankingAsync_NewDayRevisitFetchesAgain()
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

        await service.GetRankingAsync(TopAnimeRankingType.All);

        Assert.Single(malClient.RankingCalls);
    }

    [Fact]
    public async Task GetRankingAsync_FailedFetchLeavesTheListUnmarkedAndStillServesCache()
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

        var result = await service.GetRankingAsync(TopAnimeRankingType.All);

        // Cache is still served despite the failed live fetch.
        Assert.Single(result);
        Assert.Equal(1, result[0].AnimeId);

        // The failure did not mark the list as fetched for today — the log
        // row is unchanged from yesterday's seed.
        var log = await db.TopAnimeFetchLogs.SingleAsync(f => f.RankingType == "all");
        Assert.Equal(yesterday, log.LastFetchedAt);
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
    public async Task GetRankingAsync_FullyFetchedStrangerRecordsNoUpdate()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60) });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(new Dictionary<string, List<MalAnimeListEdge>>
        {
            ["all"] = [Edge(1, 1, numEpisodes: 12)],
        });
        var service = CreateService(db, malClient);

        await service.GetRankingAsync(TopAnimeRankingType.All);

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
    }

    [Fact]
    public async Task GetRankingAsync_FullyFetchedLinkedAnimeStillRecordsItsEpisodeCountReveal()
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

        await service.GetRankingAsync(TopAnimeRankingType.All);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased, update.Kinds);
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

    private sealed class FakeMalClient(
        Dictionary<string, List<MalAnimeListEdge>>? rankings = null,
        HashSet<string>? failing = null) : IMalClient
    {
        private readonly Dictionary<string, List<MalAnimeListEdge>> _rankings = rankings ?? [];
        private readonly HashSet<string> _failing = failing ?? [];

        public List<string> RankingCalls { get; } = [];

        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default)
        {
            RankingCalls.Add(rankingType);
            if (_failing.Contains(rankingType))
                throw new InvalidOperationException($"Simulated MAL failure for ranking type '{rankingType}'.");

            var edges = _rankings.GetValueOrDefault(rankingType, []);
            return Task.FromResult(new MalPagedResponse<MalAnimeListEdge> { Data = edges });
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
