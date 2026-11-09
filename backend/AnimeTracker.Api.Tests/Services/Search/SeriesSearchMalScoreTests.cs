using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using AnimeTracker.Api.Tests.Services.IdMapping;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.Series;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Search;

// improve-search-page task 1.3 against stored rows. SeriesSearchLookupTests builds the
// index from hand-made projections, so it can't see whether the lookup's query carries each
// member's MAL score at all; this reads a stored series through the lookup. It also holds
// the series card's average to the one the series page reports as the main series' MAL
// average (design.md D2), read through the real SeriesService on the same rows.
public class SeriesSearchMalScoreTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // graphBuilder is never reached: a series built just now and not partial needs no build.
    private static SeriesService CreateSeriesService(AnimeTrackerDbContext db) =>
        new(db, graphBuilder: null!, new RefreshGate(), new FakeEpisodeScheduleService(), new NoOpAnimeRankingService(),
            TmdbTestData.ArtworkService(db, new FakeTmdbClient(), apiKey: ""), TestIdMappings.Resolver(db),
            ScoreHistoryServiceTestKit.Create(db), AnimeThemesReadsTestKit.Create(db),
            SeriesRankServiceTestKit.Create(db));

    private static SeriesMember Member(
        int animeId, int order, bool mainLine = true, int? slotKey = null, int? branchHead = null,
        string? relationGroup = null) =>
        new()
        {
            SeriesId = 1, AnimeId = animeId, IsMainLine = mainLine, IsPrimary = true, Order = order,
            VersionSlotKey = slotKey, BranchHeadAnimeId = branchHead, RelationGroup = relationGroup, MembershipKind = "Core",
        };

    // Series 1, rooted at anime 1. Main line: 1, then 2 and 3 (the alternatives of one version slot,
    // both of which count for the average) and 4 (no MAL score). Extras: 5 and 6, whose scores would
    // drag the figure far from the main line's if they counted. The scores have two decimals, as MAL's
    // do, and their mean is a repeating decimal.
    private static void SeedSeries(AnimeTrackerDbContext db)
    {
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        (int Id, double? Score)[] scores = [(1, 8.31), (2, 7.64), (3, 8.97), (4, null), (5, 9.9), (6, 2.1)];
        foreach (var (id, score) in scores)
        {
            db.AnimeMetadata.Add(new AnimeMetadata
            {
                Id = id, Title = $"Fate Stay {id}", MalScore = score, AiringStatus = "finished_airing",
                AiredFrom = new DateOnly(2020, 1, 1), AiredTo = new DateOnly(2020, 6, 1),
            });
        }

        db.SeriesMembers.AddRange(
            Member(1, 0),
            Member(2, 1, slotKey: 2, branchHead: 2),
            Member(3, 2, slotKey: 2, branchHead: 3),
            Member(4, 3),
            Member(5, 0, mainLine: false, relationGroup: "SideStory"),
            Member(6, 1, mainLine: false, relationGroup: "SideStory"));
    }

    [Fact]
    public async Task TheLookupCarriesTheStoredScoresIntoTheSeriesCard()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        var index = await new SeriesSearchLookup(db).LoadAsync();
        var series = Assert.Single(index.Match("fate stay", exact: false));

        Assert.Equal((8.31 + 7.64 + 8.97) / 3, series.MalScore!.Value, 9);
        Assert.Equal(6, series.EntryCount); // the count still takes in the extras
    }

    [Fact]
    public async Task TheFigureEqualsTheSeriesPagesMainSeriesAverage()
    {
        using var db = CreateDb();
        SeedSeries(db);
        await db.SaveChangesAsync();

        var card = Assert.Single((await new SeriesSearchLookup(db).LoadAsync()).Match("fate stay", exact: false));
        var page = (await CreateSeriesService(db).GetSeriesAsync(1)).Scores.MalMain;

        Assert.NotNull(page.Value);
        Assert.Equal(3, page.ScoredCount);
        Assert.Equal(page.Value!.Value, card.MalScore!.Value, 9);
    }

    [Fact]
    public async Task ASeriesWhoseMainLineHasNoScoreHasNoFigureOnTheCardOrThePage()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 1, Title = "Fate Stay", MalScore = null, AiringStatus = "not_yet_aired",
        });
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = 2, Title = "Fate Stay Special", MalScore = 8.0, AiringStatus = "finished_airing",
            AiredFrom = new DateOnly(2020, 1, 1), AiredTo = new DateOnly(2020, 6, 1),
        });
        db.SeriesMembers.AddRange(Member(1, 0), Member(2, 0, mainLine: false, relationGroup: "SideStory"));
        await db.SaveChangesAsync();

        var card = Assert.Single((await new SeriesSearchLookup(db).LoadAsync()).Match("fate stay", exact: false));
        var page = (await CreateSeriesService(db).GetSeriesAsync(1)).Scores.MalMain;

        Assert.Null(card.MalScore);
        Assert.Null(page.Value);
    }

    // The same stand-ins as the other SeriesService tests: these don't score or
    // schedule anything, so an empty ranking and a schedule that is never asked
    // are faithful.
    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
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
    }

    private sealed class NoOpAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Build([], []));
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
