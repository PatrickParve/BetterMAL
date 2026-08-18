using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Scheduling;
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

    private static TopAnimeService CreateService(AnimeTrackerDbContext db, FakeMalClient malClient) =>
        new(
            db,
            malClient,
            new TopAnimeRepository(db),
            new FakeBroadcastLocalTimeConverter(),
            new RefreshGate(),
            NullLogger<TopAnimeService>.Instance);

    private static MalAnimeListEdge Edge(int id, int rank) =>
        new() { Node = new MalAnimeNode { Id = id, Title = $"Anime {id}" }, Ranking = new MalRankingInfo { Rank = rank } };

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

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
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
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
