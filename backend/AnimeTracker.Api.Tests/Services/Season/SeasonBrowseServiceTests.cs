using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeasonBrowseService = AnimeTracker.Api.Services.Season.SeasonBrowseService;
using SeasonRefreshOutcome = AnimeTracker.Api.Services.Season.SeasonRefreshOutcome;

namespace AnimeTracker.Api.Tests.Services.Season;

// SeasonBrowseService's refresh outcomes (design.md decisions 1 and 5,
// tasks.md 4.2/4.3): a null edge list — MAL's 404 — is an answer about the
// season, not a fetch failure, and must still count as the day's fetch.
public class SeasonBrowseServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SeasonBrowseService CreateService(AnimeTrackerDbContext db, FakeMalClient malClient) =>
        new(
            db,
            malClient,
            new SeasonRepository(db),
            new FakeBroadcastLocalTimeConverter(),
            new RefreshGate(),
            NullLogger<SeasonBrowseService>.Instance);

    [Fact]
    public async Task RefreshAsync_NullEdgeListWritesTheFetchLogAddsNoListingsAndReportsNotListed()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(edges: null);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2027, "spring");

        Assert.Equal(SeasonRefreshOutcome.NotListed, result.Outcome);

        var fetchLog = await db.SeasonFetchLogs.SingleAsync(f => f.Year == 2027 && f.Season == "spring");
        Assert.True(fetchLog.LastFetchedAt > DateTimeOffset.UtcNow.AddMinutes(-1));

        Assert.Empty(await db.SeasonAnimeListings.Where(l => l.Year == 2027 && l.Season == "spring").ToListAsync());
    }

    [Fact]
    public async Task RefreshAsync_SameDayRevisitReportsSkippedWithNoMalCall()
    {
        using var db = CreateDb();
        db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = 2026, Season = "summer", LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2026, "summer");

        Assert.Equal(SeasonRefreshOutcome.Skipped, result.Outcome);
        Assert.Equal(0, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshAsync_ThrowingClientReportsFailedAndWritesNoFetchLog()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(throws: true);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2026, "summer");

        Assert.Equal(SeasonRefreshOutcome.Failed, result.Outcome);
        Assert.False(await db.SeasonFetchLogs.AnyAsync(f => f.Year == 2026 && f.Season == "summer"));
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
    }

    private sealed class FakeMalClient(List<MalAnimeListEdge>? edges = null, bool throws = false) : IMalClient
    {
        public int FullSeasonCallCount { get; private set; }

        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default)
        {
            FullSeasonCallCount++;
            if (throws)
                throw new InvalidOperationException("Simulated MAL failure.");
            return Task.FromResult(edges);
        }

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
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
