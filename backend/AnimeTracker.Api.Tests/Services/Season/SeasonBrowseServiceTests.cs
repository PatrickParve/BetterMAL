using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SeasonBrowseService = AnimeTracker.Api.Services.Season.SeasonBrowseService;
using SeasonCalendar = AnimeTracker.Api.Services.Season.SeasonCalendar;
using SeasonRefreshOutcome = AnimeTracker.Api.Services.Season.SeasonRefreshOutcome;
using YearRefreshResultDto = AnimeTracker.Api.Services.Season.YearRefreshResultDto;

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

    private static SeasonBrowseService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(
            db,
            malClient,
            new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), new SeriesBuildTrigger()),
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

    // Turns "N whole years ago" into a (year, season) pair anchored to
    // today's real current season-quarter, shifted back N full years (4*N
    // quarters). This lands exactly N whole years old by
    // SeasonRefreshCadence's reckoning on any day this runs: RefreshAsync
    // reads DateTimeOffset.UtcNow itself (not an injectable clock), so a
    // hard-coded year would eventually age out of the tier it was meant to
    // test. A negative count shifts forward instead, landing on a season
    // that has not started yet.
    private static (int Year, string Season) SeasonWholeYearsAgo(int wholeYears)
    {
        var (year, season) = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));
        return SeasonCalendar.Shift(year, season, -4 * wholeYears);
    }

    // The age-tiered cadence (SeasonRefreshCadence, tasks.md 2.1): a season's
    // own age sets how many days must pass before it is stale again. These
    // mirror the boundary cases SeasonRefreshCadenceTests already covers at
    // the unit level, but exercised through RefreshAsync end to end.
    [Theory]
    [InlineData(1, 2, SeasonRefreshOutcome.Skipped, 0)]
    [InlineData(1, 3, SeasonRefreshOutcome.Fetched, 1)]
    [InlineData(3, 4, SeasonRefreshOutcome.Skipped, 0)]
    [InlineData(3, 5, SeasonRefreshOutcome.Fetched, 1)]
    [InlineData(8, 9, SeasonRefreshOutcome.Skipped, 0)]
    [InlineData(8, 10, SeasonRefreshOutcome.Fetched, 1)]
    public async Task RefreshAsync_AgeTieredIntervalGatesTheRefetch(int wholeYearsOld, int daysSinceLastFetch, SeasonRefreshOutcome expectedOutcome, int expectedMalCallCount)
    {
        using var db = CreateDb();
        var (year, season) = SeasonWholeYearsAgo(wholeYearsOld);
        db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = year, Season = season, LastFetchedAt = DateTimeOffset.UtcNow.AddDays(-daysSinceLastFetch) });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(year, season);

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Equal(expectedMalCallCount, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshAsync_OldSeasonWithNoFetchLogAlwaysFetchesOnFirstVisit()
    {
        using var db = CreateDb();
        var (year, season) = SeasonWholeYearsAgo(8);
        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(year, season);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(1, malClient.FullSeasonCallCount);
    }

    // Design D8: a future season is age 0 (the daily tier), which is what
    // lets GetBoundsAsync's forward horizon keep re-probing it every day.
    [Fact]
    public async Task RefreshAsync_FutureSeasonStampedYesterdayStillFetchesSoTheHorizonKeepsProbing()
    {
        using var db = CreateDb();
        var (year, season) = SeasonWholeYearsAgo(-1);
        db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = year, Season = season, LastFetchedAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(year, season);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(1, malClient.FullSeasonCallCount);
    }

    // RefreshYearAsync's fold (design.md D2, tasks.md 2.4): the four
    // per-season outcomes collapse to one year outcome by precedence
    // Fetched > Skipped > NotListed > Failed.
    private static readonly string[] SeasonsInYearOrder = ["winter", "spring", "summer", "fall"];

    // A year straddling the one-year age boundary (design D9, tasks.md 2.8):
    // last year's winter/spring/summer are already a year old (3-day
    // interval) while its fall — the most recently started of the four — is
    // still under a year old (1-day interval). Stamped alike 2 days ago, the
    // older three read as fresh and only fall is stale enough to re-fetch.
    // Anchored to today's real date like SeasonWholeYearsAgo above, so it
    // reflects whichever of last year's quarters is currently under a year
    // old rather than a year number that would eventually age out.
    [Fact]
    public async Task RefreshYearAsync_StraddlingYearFetchesOnlyTheSeasonUnderItsOwnAgeThreshold()
    {
        using var db = CreateDb();
        var year = DateTime.UtcNow.Year - 1;
        var lastFetchedAt = DateTimeOffset.UtcNow.AddDays(-2);
        foreach (var season in SeasonsInYearOrder)
            db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = year, Season = season, LastFetchedAt = lastFetchedAt });
        await db.SaveChangesAsync();

        var malClient = new PerSeasonFakeMalClient(new() { ["fall"] = new MalSeasonResponse(Edges: []) });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(year);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(1, malClient.FullSeasonCallCount);
        Assert.Equal(["fall"], malClient.CalledSeasons);
    }

    [Fact]
    public async Task RefreshYearAsync_OneFetchingThreeAlreadyFetchedTodayFoldsToFetched()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        foreach (var season in new[] { "winter", "spring", "fall" })
            db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = 2026, Season = season, LastFetchedAt = now });
        await db.SaveChangesAsync();

        var malClient = new PerSeasonFakeMalClient(new() { ["summer"] = new MalSeasonResponse(Edges: []) });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(1, malClient.FullSeasonCallCount);
        Assert.Equal(["summer"], malClient.CalledSeasons);
    }

    [Fact]
    public async Task RefreshYearAsync_AllFourAlreadyFetchedTodayFoldsToSkippedWithNoMalCall()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        foreach (var season in SeasonsInYearOrder)
            db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = 2026, Season = season, LastFetchedAt = now });
        await db.SaveChangesAsync();

        var malClient = new PerSeasonFakeMalClient(new());
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.Skipped, result.Outcome);
        Assert.Equal(0, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshYearAsync_AllFourNotListedFoldsToNotListed()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(SeasonsInYearOrder.ToDictionary(s => s, _ => new MalSeasonResponse(Edges: null)));
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.NotListed, result.Outcome);
        Assert.Equal(4, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshYearAsync_AllFourFailingFoldsToFailed()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(SeasonsInYearOrder.ToDictionary(s => s, _ => new MalSeasonResponse(Edges: null, Throws: true)));
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.Failed, result.Outcome);
    }

    [Fact]
    public async Task RefreshYearAsync_TwoListedTwoNotListedFoldsToFetched()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["winter"] = new MalSeasonResponse(Edges: []),
            ["spring"] = new MalSeasonResponse(Edges: []),
            ["summer"] = new MalSeasonResponse(Edges: null),
            ["fall"] = new MalSeasonResponse(Edges: null),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
    }

    [Fact]
    public async Task RefreshYearAsync_ThreeFetchingOneFailingFoldsToFetched()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["winter"] = new MalSeasonResponse(Edges: []),
            ["spring"] = new MalSeasonResponse(Edges: []),
            ["summer"] = new MalSeasonResponse(Edges: []),
            ["fall"] = new MalSeasonResponse(Edges: null, Throws: true),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(2026);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
    }

    [Fact]
    public async Task RefreshYearAsync_RunsTheFourSeasonsSequentiallyInCalendarOrder()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(SeasonsInYearOrder.ToDictionary(s => s, _ => new MalSeasonResponse(Edges: [])));
        var service = CreateService(db, malClient);

        await service.RefreshYearAsync(2026);

        // One DbContext, awaited one at a time — a concurrent implementation
        // would either throw (a scoped context can't serve overlapping EF
        // queries) or interleave the call order; observing all four in exact
        // calendar order is what a sequential, awaited loop guarantees.
        Assert.Equal(SeasonsInYearOrder, malClient.CalledSeasons);
    }

    // --- fix-false-updates-on-lean-rows: a lean listing write detects too ---

    private static MalAnimeListEdge SeasonEdge(int id, int year, string season, int? numEpisodes = null, string? startDate = null) =>
        new()
        {
            Node = new MalAnimeNode
            {
                Id = id,
                Title = $"Anime {id}",
                MediaType = "tv",
                NumEpisodes = numEpisodes,
                StartDate = startDate,
                StartSeason = new MalStartSeason { Year = year, Season = season },
            },
        };

    // Design D4: season browsing writes AiredFrom by hand, outside ApplyLeanTo
    // — so before this change a genuine premiere move on a browsed anime was
    // *absorbed*, stored without news, leaving the next full fetch nothing to
    // find. The row is fully fetched and not yet aired, so the write is a real
    // diff against a real prior observation and the airing gate lets it past.
    [Fact]
    public async Task RefreshAsync_LeanWriteOverAFullyFetchedAnimeRecordsThePremiereMoveItWrites()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            AiredFrom = new DateOnly(2027, 1, 5),
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
        };
        anime.RelatedAnime.Add(new AnimeRelatedAnime { AnimeId = 1, RelatedAnimeId = 2, RelationType = "sequel", Title = "Anime 2" });
        db.AnimeMetadata.Add(anime);
        // anime-updates "Nothing is recorded for an anime outside my list and
        // its direct relations" (scope-updates-to-my-list tasks 6.1): without
        // a non-Dropped entry of my own, the relevance gate would silence
        // this reveal regardless of the diff below finding it.
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([SeasonEdge(1, 2027, "winter", numEpisodes: 12, startDate: "2027-01-12")]);
        await CreateService(db, malClient).RefreshAsync(2027, "winter");

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.Equal(AnimeUpdateKinds.EpisodeCountReleased | AnimeUpdateKinds.StartDateChanged, update.Kinds);
        Assert.Equal(new DateOnly(2027, 1, 5), update.PreviousStartDate);

        // A lean write never touches relations and this path never Includes
        // them, so the snapshot does not observe them (SnapshotListing leaves
        // Relations null) and no edge is discovered off a browse.
        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    // anime-updates spec ("Nothing is recorded for an anime outside my list
    // and its direct relations"; scope-updates-to-my-list tasks 6.7): the
    // same premiere move as above, but for an anime with no list entry and no
    // relation to one — the common case a season browse writes for hundreds
    // of anime the user will never add.
    [Fact]
    public async Task RefreshAsync_LeanWriteOverAFullyFetchedStrangerRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata
        {
            Id = 1,
            Title = "Anime 1",
            AiringStatus = "not_yet_aired",
            AiredFrom = new DateOnly(2027, 1, 5),
            LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-60),
        };
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([SeasonEdge(1, 2027, "winter", numEpisodes: 12, startDate: "2027-01-12")]);
        await CreateService(db, malClient).RefreshAsync(2027, "winter");

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        Assert.Empty(await db.RelationDiscoveries.ToListAsync());
    }

    // The other half of D1: the same browse over a row that has never been
    // fully fetched records nothing. Its null premiere date and null episode
    // count were never observations of anything — the row is a listing stub,
    // not a prior state to diff against.
    [Fact]
    public async Task RefreshAsync_LeanWriteOverANeverFullyFetchedRowRecordsNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new MalAnimeNode { Id = 1, Title = "Anime 1", MediaType = "tv" }
            .ToLeanAnimeMetadata(DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([SeasonEdge(1, 2026, "summer", numEpisodes: 12, startDate: "2026-07-12")]);
        await CreateService(db, malClient).RefreshAsync(2026, "summer");

        Assert.Empty(await db.AnimeUpdates.ToListAsync());
        Assert.Empty(await db.RelationDiscoveries.ToListAsync());

        // The browse still caches what it fetched — it just doesn't call it news.
        var stored = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(12, stored.TotalEpisodes);
        Assert.Equal(new DateOnly(2026, 7, 12), stored.AiredFrom);
    }

    private sealed record MalSeasonResponse(List<MalAnimeListEdge>? Edges, bool Throws = false);

    // Per-season configurable fake — RefreshYearAsync's fold needs each of a
    // year's four seasons to answer differently within the same test, unlike
    // FakeMalClient below which answers every call the same way.
    private sealed class PerSeasonFakeMalClient(Dictionary<string, MalSeasonResponse> bySeason) : IMalClient
    {
        public int FullSeasonCallCount { get; private set; }
        public List<string> CalledSeasons { get; } = [];

        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default)
        {
            FullSeasonCallCount++;
            CalledSeasons.Add(season);
            var response = bySeason[season];
            if (response.Throws)
                throw new InvalidOperationException("Simulated MAL failure.");
            return Task.FromResult(response.Edges);
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
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
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
