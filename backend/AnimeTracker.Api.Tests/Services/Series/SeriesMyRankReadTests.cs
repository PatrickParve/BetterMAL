using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The series rank as the app reads it (add-my-rank-badges tasks 2.2/2.3/2.5): the
// series read and the my-rank endpoint, over real services on an in-memory database.
// The graph builder is null on purpose — a stored series built just now needs no
// build, and a rank read that built anything would throw.
public class SeriesMyRankReadTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SeriesService CreateService(AnimeTrackerDbContext db) =>
        new(db, graphBuilder: null!, new RefreshGate(), new EpisodeScheduleService(new EpisodeAiringRepository(db), new BroadcastLocalTimeConverter()), RankingService(db),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""), TestIdMappings.Resolver(db),
            ScoreHistoryServiceTestKit.Create(db), AnimeThemesReadsTestKit.Create(db), SeriesRankServiceTestKit.Create(db));

    private static AnimeRankingService RankingService(AnimeTrackerDbContext db) =>
        new(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db));

    private static SeriesController CreateController(AnimeTrackerDbContext db) =>
        new(seriesService: null!, scoreHistoryService: null!, seriesListService: null!, bulkBuildTrigger: null!,
            bulkBuildProgress: null!, artworkSelectionService: null!, pictureRefreshService: null!, tmdbArtwork: null!,
            metadataRepository: null!, seriesRankService: SeriesRankServiceTestKit.Create(db));

    private static async Task<MyRankDto> EndpointAsync(AnimeTrackerDbContext db, int seriesId) =>
        Assert.IsType<MyRankDto>(Assert.IsType<OkObjectResult>(
            await CreateController(db).GetMyRank(seriesId, CancellationToken.None)).Value);

    // One series whose main line is its root anime alone, rooted at `id`.
    private static void AddSeries(
        AnimeTrackerDbContext db, int id, int? score, string? title = null, string? selectedTitle = null,
        string membershipKind = "Core", WatchStatus? status = WatchStatus.Completed)
    {
        db.Series.Add(new SeriesModel { Id = id, BuiltAt = DateTimeOffset.UtcNow, SelectedTitle = selectedTitle });
        var anime = new AnimeMetadata { Id = id, Title = title ?? $"Anime {id}", AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.SeriesMembers.Add(new SeriesMember
        {
            SeriesId = id, AnimeId = id, IsMainLine = true, IsPrimary = true, Order = 0, MembershipKind = membershipKind,
        });
        if (status is { } s)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = id, Anime = anime, Status = s, MyScore = score });
    }

    // Series 1, 2 and 3 average 9, 8 and 7; series 4 is in my list with no score at all.
    private static void SeedFour(AnimeTrackerDbContext db)
    {
        AddSeries(db, 1, score: 9);
        AddSeries(db, 2, score: 8);
        AddSeries(db, 3, score: 7);
        AddSeries(db, 4, score: null);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    public async Task TheSeriesReadAndTheEndpointAgreeForARankedSeries(int seriesId, int expectedRank)
    {
        using var db = CreateDb();
        SeedFour(db);
        await db.SaveChangesAsync();

        var page = await CreateService(db).GetSeriesAsync(seriesId);
        var endpoint = await EndpointAsync(db, seriesId);

        Assert.Equal(expectedRank, page.MyRank);
        Assert.Equal(3, page.MyRankedCount);
        Assert.Equal(new MyRankDto(expectedRank, 3), endpoint);
    }

    [Fact]
    public async Task ASeriesWithNoMyAverageHoldsNoRankOnTheReadOrTheEndpoint()
    {
        using var db = CreateDb();
        SeedFour(db);
        await db.SaveChangesAsync();

        var page = await CreateService(db).GetSeriesAsync(4);
        var endpoint = await EndpointAsync(db, 4);

        Assert.Null(page.MyRank);
        Assert.Equal(3, page.MyRankedCount);
        Assert.Equal(new MyRankDto(null, 3), endpoint);
    }

    [Fact]
    public async Task ASeriesTheListDoesNotHoldAnswersNoRank()
    {
        using var db = CreateDb();
        SeedFour(db);
        await db.SaveChangesAsync();

        Assert.Equal(new MyRankDto(null, 3), await EndpointAsync(db, 999));
    }

    // A member that is only a version neighbour does not list its series, so even a
    // main-line score on it earns the series no rank (series-browser "A version-neighbour
    // membership does not list a series"). Series 5 would otherwise average 10 and take rank 1.
    [Theory]
    [InlineData("NeighbourTelling")]
    [InlineData("FoldedVersion")]
    public async Task ASeriesListedOnlyThroughAVersionNeighbourMembershipHoldsNoRank(string membershipKind)
    {
        using var db = CreateDb();
        SeedFour(db);
        AddSeries(db, 5, score: 10, membershipKind: membershipKind);
        await db.SaveChangesAsync();

        var page = await CreateService(db).GetSeriesAsync(5);
        var endpoint = await EndpointAsync(db, 5);

        Assert.Null(page.MyRank);
        Assert.Equal(3, page.MyRankedCount);
        Assert.Equal(new MyRankDto(null, 3), endpoint);
        // And it displaces nobody: the series that really are ranked keep their places.
        Assert.Equal(new MyRankDto(1, 3), await EndpointAsync(db, 1));
    }

    [Fact]
    public async Task ReScoringAMemberMovesTheEndpointsAnswerWithNoRebuild()
    {
        using var db = CreateDb();
        SeedFour(db);
        await db.SaveChangesAsync();
        Assert.Equal(new MyRankDto(3, 3), await EndpointAsync(db, 3));

        (await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 3)).MyScore = 10;
        await db.SaveChangesAsync();

        // The graph builder is null, so a rebuild would have thrown rather than answered.
        Assert.Equal(new MyRankDto(1, 3), await EndpointAsync(db, 3));
        Assert.Equal(new MyRankDto(2, 3), await EndpointAsync(db, 1));
        Assert.Equal(1, (await CreateService(db).GetSeriesAsync(3)).MyRank);
    }

    [Fact]
    public async Task ScoringTheFirstEntryOfAnUnscoredSeriesGivesItARankAndCountsIt()
    {
        using var db = CreateDb();
        SeedFour(db);
        await db.SaveChangesAsync();
        Assert.Equal(new MyRankDto(null, 3), await EndpointAsync(db, 4));

        (await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 4)).MyScore = 8; // ties series 2 on average
        await db.SaveChangesAsync();

        var endpoint = await EndpointAsync(db, 4);
        Assert.NotNull(endpoint.Rank);
        Assert.Equal(4, endpoint.RankedCount);
        // The tie is broken by the order's later steps, so series 2 and 4 hold ranks 2 and 3 in some order.
        Assert.Equal([2, 3], new[] { (await EndpointAsync(db, 2)).Rank!.Value, endpoint.Rank!.Value }.Order());
    }

    // The series read hands the ranking it already holds to the rank read, so the
    // average-ranking-position step must come from that snapshot: series 1 and 2 both
    // average 8, series 1's title is the earlier, but anime 2 sits above anime 1 in my ranking
    // (the ranking breaks the tie by title), so series 2 comes first.
    [Fact]
    public async Task TheAverageRankingPositionBreaksATieOnTheReadAndTheEndpoint()
    {
        using var db = CreateDb();
        AddSeries(db, 1, score: 8, title: "Zed", selectedTitle: "Aaa");
        AddSeries(db, 2, score: 8, title: "Alpha", selectedTitle: "Zzz");
        await db.SaveChangesAsync();

        Assert.Equal(1, (await CreateService(db).GetSeriesAsync(2)).MyRank);
        Assert.Equal(2, (await CreateService(db).GetSeriesAsync(1)).MyRank);
        Assert.Equal(new MyRankDto(1, 2), await EndpointAsync(db, 2));
        Assert.Equal(new MyRankDto(2, 2), await EndpointAsync(db, 1));
    }
}
