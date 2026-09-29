using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
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
    // A shared name gives two contexts the same in-memory store, which is how
    // two requests each get their own scoped DbContext over one database.
    private static AnimeTrackerDbContext CreateDb(string? name = null) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options);

    private static SeasonBrowseService CreateService(
        AnimeTrackerDbContext db, IMalClient malClient, IAnimeSearchIndex? searchIndex = null, RefreshGate? refreshGate = null) =>
        new(
            db,
            malClient,
            new AnimeMetadataChangeDetector(db, new AnimeUpdateRecorder(db, new AnimeUpdateRelevance(db, new RelationResolver(db))), new SeriesBuildTrigger()),
            new SeasonRepository(db),
            new FakeBroadcastLocalTimeConverter(),
            refreshGate ?? new RefreshGate(),
            searchIndex ?? new FakeAnimeSearchIndex(),
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

    // simplify-settings-and-first-fetch-states design D1: what looks like two
    // fetches of a never-fetched season is one MAL request. A second request
    // for the same season (another tab, a remounted page) meets the first at
    // the RefreshGate and is answered Skipped once the first's stamp is in.
    [Fact]
    public async Task RefreshAsync_TwoOverlappingCallsOnANeverFetchedSeasonFetchOnceAndReportFetchedThenSkipped()
    {
        var dbName = Guid.NewGuid().ToString();
        using var firstDb = CreateDb(dbName);
        using var secondDb = CreateDb(dbName);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var malClient = new FakeMalClient(edges: [], release: release);
        var gate = new RefreshGate();
        var firstService = CreateService(firstDb, malClient, refreshGate: gate);
        var secondService = CreateService(secondDb, malClient, refreshGate: gate);

        var firstCall = firstService.RefreshAsync(2026, "summer");
        await malClient.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10)); // the first holds the gate, blocked inside MAL

        var secondCall = secondService.RefreshAsync(2026, "summer");
        await Task.Delay(100);
        Assert.False(secondCall.IsCompleted); // waiting on the gate, not fetching

        release.SetResult();
        var first = await firstCall.WaitAsync(TimeSpan.FromSeconds(10));
        var second = await secondCall.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(SeasonRefreshOutcome.Fetched, first.Outcome);
        Assert.Equal(SeasonRefreshOutcome.Skipped, second.Outcome);
        Assert.Equal(1, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshYearAsync_ANeverFetchedPastYearFetchesEachSeasonOnceAndASecondVisitTheSameDayFetchesNothing()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(SeasonsInYearOrder.ToDictionary(s => s, _ => new MalSeasonResponse(Edges: [])));
        var service = CreateService(db, malClient);

        var first = await service.RefreshYearAsync(2020);

        Assert.Equal(SeasonRefreshOutcome.Fetched, first.Outcome);
        Assert.Equal(SeasonsInYearOrder, malClient.CalledSeasons);
        Assert.Equal(4, malClient.FullSeasonCallCount);

        var second = await service.RefreshYearAsync(2020);

        Assert.Equal(SeasonRefreshOutcome.Skipped, second.Outcome);
        Assert.Equal(4, malClient.FullSeasonCallCount); // no further call
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

    // --- cache-type-ahead-search-index tasks.md 5.2: a browse invalidates the search index ---

    [Fact]
    public async Task RefreshAsync_CachingANotYetSeenAnimeInvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient([SeasonEdge(1, 2027, "winter", numEpisodes: 12, startDate: "2027-01-12")]);
        var searchIndex = new FakeAnimeSearchIndex();

        await CreateService(db, malClient, searchIndex).RefreshAsync(2027, "winter");

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    [Fact]
    public async Task RefreshAsync_LeanlyRewritingAnExistingRowInvalidatesTheSearchIndex()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient([SeasonEdge(1, 2027, "winter", numEpisodes: 12, startDate: "2027-01-12")]);
        var searchIndex = new FakeAnimeSearchIndex();

        await CreateService(db, malClient, searchIndex).RefreshAsync(2027, "winter");

        Assert.Equal(1, searchIndex.InvalidateCallCount);
    }

    // --- fix-season-pruning-and-bound-clamp: B3, a fetch that returns anime prunes what MAL no longer files here (design D1-D3) ---

    private static async Task SeedListing(AnimeTrackerDbContext db, int animeId, int year, string season)
    {
        if (!await db.AnimeMetadata.AnyAsync(a => a.Id == animeId))
            db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" });
        db.SeasonAnimeListings.Add(new SeasonAnimeListing { Year = year, Season = season, AnimeId = animeId });
        await db.SaveChangesAsync();
    }

    private static async Task<List<int>> ListedIds(AnimeTrackerDbContext db, int year, string season) =>
        (await db.SeasonAnimeListings.AsNoTracking()
            .Where(l => l.Year == year && l.Season == season)
            .Select(l => l.AnimeId)
            .ToListAsync())
        .OrderBy(id => id)
        .ToList();

    // Overload of SeasonEdge above, for an edge MAL returns with no
    // start_season at all — trusted to whichever season the response belongs
    // to (design D1).
    private static MalAnimeListEdge SeasonEdge(int id, int? numEpisodes = null, string? startDate = null) =>
        new()
        {
            Node = new MalAnimeNode
            {
                Id = id,
                Title = $"Anime {id}",
                MediaType = "tv",
                NumEpisodes = numEpisodes,
                StartDate = startDate,
            },
        };

    [Fact]
    public async Task RefreshAsync_AMovedAnimeLeavesItsOldSeason()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["spring"] = new MalSeasonResponse(Edges: [SeasonEdge(2, 2020, "spring"), SeasonEdge(3, 2020, "spring")]),
            ["summer"] = new MalSeasonResponse(Edges: [SeasonEdge(1, 2020, "summer")]),
        });
        var service = CreateService(db, malClient);

        var summerResult = await service.RefreshAsync(2020, "summer");
        var springResult = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Fetched, summerResult.Outcome);
        Assert.Equal(SeasonRefreshOutcome.Fetched, springResult.Outcome);
        Assert.Equal([2, 3], await ListedIds(db, 2020, "spring"));
        Assert.Equal([1], await ListedIds(db, 2020, "summer"));
    }

    [Fact]
    public async Task RefreshAsync_ReturnedButFiledElsewhereIsPruned()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["spring"] = new MalSeasonResponse(Edges: [SeasonEdge(1, 2019, "fall"), SeasonEdge(2, 2020, "spring")]),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal([2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshAsync_NoStartSeasonIsKept()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["spring"] = new MalSeasonResponse(Edges: [SeasonEdge(1), SeasonEdge(2, 2020, "spring")]),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal([1, 2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshAsync_NotListedResponseRemovesNothing()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new() { ["spring"] = new MalSeasonResponse(Edges: null) });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.NotListed, result.Outcome);
        Assert.Equal([1, 2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshAsync_EmptyResponseRemovesNothing()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new() { ["spring"] = new MalSeasonResponse(Edges: []) });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal([1, 2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshAsync_FailedFetchRemovesNothing()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new() { ["spring"] = new MalSeasonResponse(Edges: null, Throws: true) });
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Failed, result.Outcome);
        Assert.Equal([1, 2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshAsync_SkippedRefreshRemovesNothing()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");
        db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = 2020, Season = "spring", LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var malClient = new PerSeasonFakeMalClient(new());
        var service = CreateService(db, malClient);

        var result = await service.RefreshAsync(2020, "spring");

        Assert.Equal(SeasonRefreshOutcome.Skipped, result.Outcome);
        Assert.Equal(0, malClient.FullSeasonCallCount);
        Assert.Equal([1, 2], await ListedIds(db, 2020, "spring"));
    }

    [Fact]
    public async Task RefreshYearAsync_TheYearShowsAMovedAnimeOnce()
    {
        using var db = CreateDb();
        await SeedListing(db, 1, 2020, "spring");
        await SeedListing(db, 2, 2020, "spring");

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["winter"] = new MalSeasonResponse(Edges: null),
            ["spring"] = new MalSeasonResponse(Edges: [SeasonEdge(2, 2020, "spring")]),
            ["summer"] = new MalSeasonResponse(Edges: [SeasonEdge(1, 2020, "summer")]),
            ["fall"] = new MalSeasonResponse(Edges: null),
        });
        var service = CreateService(db, malClient);

        await service.RefreshYearAsync(2020);
        var page = await service.GetYearPageAsync(2020, hideHentai: false);

        Assert.Equal([1, 2], page.Items.Select(i => i.AnimeId).OrderBy(id => id));
        Assert.Equal([2], await ListedIds(db, 2020, "spring"));
        Assert.Equal([1], await ListedIds(db, 2020, "summer"));
    }

    // --- fix-season-pruning-and-bound-clamp: PF4, GetRequestRangeAsync (design D7) ---

    [Fact]
    public async Task GetRequestRangeAsync_NothingCachedEndsAtCurrentPlusTwo()
    {
        using var db = CreateDb();
        var current = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));
        var expected = SeasonCalendar.Shift(current.Year, current.Season, 2);
        var service = CreateService(db, new FakeMalClient());

        var range = await service.GetRequestRangeAsync();

        Assert.Equal(1917, range.EarliestYear);
        Assert.Equal(expected, (range.LatestYear, range.LatestSeason));
    }

    [Fact]
    public async Task GetRequestRangeAsync_ASeededFarFutureListingRaisesTheCeiling()
    {
        using var db = CreateDb();
        var current = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));
        var (year, season) = SeasonCalendar.Shift(current.Year, current.Season, 5);
        await SeedListing(db, 1, year, season);
        var service = CreateService(db, new FakeMalClient());

        var range = await service.GetRequestRangeAsync();

        Assert.Equal((year, season), (range.LatestYear, range.LatestSeason));
    }

    [Fact]
    public async Task GetRequestRangeAsync_TodaysNotListedCurrentPlusTwoStillEndsThereWhileBoundsRetreats()
    {
        using var db = CreateDb();
        var current = SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));
        var (year, season) = SeasonCalendar.Shift(current.Year, current.Season, 2);
        db.SeasonFetchLogs.Add(new SeasonFetchLog { Year = year, Season = season, LastFetchedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        var malClient = new FakeMalClient();
        var service = CreateService(db, malClient);

        var range = await service.GetRequestRangeAsync();
        var bounds = await service.GetBoundsAsync();
        var expectedRetreat = SeasonCalendar.Shift(current.Year, current.Season, 1);

        Assert.Equal((year, season), (range.LatestYear, range.LatestSeason));
        Assert.Equal(expectedRetreat, (bounds.LatestYear, bounds.LatestSeason));

        var refreshResult = await service.RefreshAsync(year, season);
        Assert.Equal(SeasonRefreshOutcome.Skipped, refreshResult.Outcome);
        Assert.Equal(0, malClient.FullSeasonCallCount);
    }

    // --- bound-browse-range-and-sorting: ProbeHorizonAsync (design D10/D10a, tasks.md 2.8) ---
    //
    // Background probing (onDemand: false) additionally requires today to
    // fall in the final month of the current season — that rule is exercised
    // directly against fabricated dates in HorizonProbeTests. ProbeHorizonAsync
    // reads the real clock (like RefreshAsync) and can't be handed a fake one,
    // so onDemand: true — which ignores that one gate — is used below to keep
    // these tests deterministic on any day they run.

    private static (int Year, string Season) CurrentSeason() =>
        SeasonCalendar.GetSeasonFor(DateOnly.FromDateTime(DateTime.UtcNow));

    private static (int Year, string Season) ProbeTarget() =>
        SeasonCalendar.Shift(CurrentSeason().Year, CurrentSeason().Season, 3);

    [Fact]
    public async Task ProbeHorizonAsync_OnDemandSuccessCachesTheSeasonAndRaisesTheCeiling()
    {
        using var db = CreateDb();
        var target = ProbeTarget();
        var malClient = new FakeMalClient([SeasonEdge(1, target.Year, target.Season)]);
        var service = CreateService(db, malClient);

        var bounds = await service.ProbeHorizonAsync(onDemand: true);

        Assert.Equal((target.Year, target.Season), (bounds.LatestYear, bounds.LatestSeason));
        Assert.Equal(1, malClient.FullSeasonCallCount);
        Assert.Equal([1], await ListedIds(db, target.Year, target.Season));
    }

    [Fact]
    public async Task ProbeHorizonAsync_OnDemand404LeavesBothCeilingsUnchangedAndOnlyStampsTheFetchLog()
    {
        using var db = CreateDb();
        var target = ProbeTarget();
        var expectedDefaultCeiling = SeasonCalendar.Shift(CurrentSeason().Year, CurrentSeason().Season, 2);
        var malClient = new FakeMalClient(edges: null);
        var service = CreateService(db, malClient);

        var bounds = await service.ProbeHorizonAsync(onDemand: true);
        var range = await service.GetRequestRangeAsync();

        Assert.Equal(expectedDefaultCeiling, (bounds.LatestYear, bounds.LatestSeason));
        Assert.Equal(expectedDefaultCeiling, (range.LatestYear, range.LatestSeason));
        Assert.Equal(1, malClient.FullSeasonCallCount);
        Assert.True(await db.SeasonFetchLogs.AnyAsync(f => f.Year == target.Year && f.Season == target.Season));
    }

    [Fact]
    public async Task ProbeHorizonAsync_OnDemandSecondProbeTheSameLocalDayMakesNoMalRequest()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        await service.ProbeHorizonAsync(onDemand: true);
        await service.ProbeHorizonAsync(onDemand: true);

        Assert.Equal(1, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task ProbeHorizonAsync_BackgroundModeOnlyFetchesInTheFinalMonthOfTheCurrentSeason()
    {
        using var db = CreateDb();
        var malClient = new FakeMalClient(edges: []);
        var service = CreateService(db, malClient);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var current = SeasonCalendar.GetSeasonFor(today);
        var isInFinalMonth = today.Month == 3 * (SeasonCalendar.GetSeasonIndex(current.Season) + 1);

        await service.ProbeHorizonAsync();

        Assert.Equal(isInFinalMonth ? 1 : 0, malClient.FullSeasonCallCount);
    }

    // --- bound-browse-range-and-sorting: RefreshYearAsync trims to the outer ceiling (design D11, tasks.md 3.3) ---

    [Fact]
    public async Task RefreshYearAsync_APastYearStillRefreshesAllFourSeasons()
    {
        using var db = CreateDb();
        var malClient = new PerSeasonFakeMalClient(SeasonsInYearOrder.ToDictionary(s => s, _ => new MalSeasonResponse(Edges: [])));
        var service = CreateService(db, malClient);

        await service.RefreshYearAsync(2020);

        Assert.Equal(SeasonsInYearOrder, malClient.CalledSeasons);
        Assert.Equal(4, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshYearAsync_TheCeilingsYearRefreshesOnlyTheSeasonsAtOrBeforeItWithNoRequestForTheRest()
    {
        using var db = CreateDb();
        var current = CurrentSeason();
        var farYear = current.Year + 5; // comfortably beyond any real "today"'s default ceiling
        await SeedListing(db, 1, farYear, "summer"); // pins the outer ceiling to (farYear, summer)

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["winter"] = new MalSeasonResponse(Edges: []),
            ["spring"] = new MalSeasonResponse(Edges: []),
            ["summer"] = new MalSeasonResponse(Edges: []),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(farYear);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(["winter", "spring", "summer"], malClient.CalledSeasons);
        Assert.Equal(3, malClient.FullSeasonCallCount);
    }

    [Fact]
    public async Task RefreshYearAsync_AYearWhoseOnlyInRangeSeasonFetchesSuccessfullyReportsFetched()
    {
        using var db = CreateDb();
        var current = CurrentSeason();
        var farYear = current.Year + 6;
        await SeedListing(db, 1, farYear, "winter"); // pins the outer ceiling to (farYear, winter) — only that one season is in range

        var malClient = new PerSeasonFakeMalClient(new()
        {
            ["winter"] = new MalSeasonResponse(Edges: []),
        });
        var service = CreateService(db, malClient);

        var result = await service.RefreshYearAsync(farYear);

        Assert.Equal(SeasonRefreshOutcome.Fetched, result.Outcome);
        Assert.Equal(["winter"], malClient.CalledSeasons);
        Assert.Equal(1, malClient.FullSeasonCallCount);
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

    // With a release source, GetFullSeasonAsync answers only once it is set —
    // Entered marks the moment a caller is inside MAL, so a test can start a
    // second caller while the first is held there.
    private sealed class FakeMalClient(List<MalAnimeListEdge>? edges = null, bool throws = false, TaskCompletionSource? release = null) : IMalClient
    {
        public int FullSeasonCallCount { get; private set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default)
        {
            FullSeasonCallCount++;
            Entered.TrySetResult();
            if (throws)
                throw new InvalidOperationException("Simulated MAL failure.");
            return release is null ? Task.FromResult(edges) : AnswerOnceReleased();

            async Task<List<MalAnimeListEdge>?> AnswerOnceReleased()
            {
                await release.Task;
                return edges;
            }
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
