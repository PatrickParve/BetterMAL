using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.ScoreHistory;
using AnimeTracker.Api.Tests.Services.ScoreHistory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Controllers;

// add-score-history-graph task 4.3: GET api/anime/{animeId}/score-history and
// GET api/series/{seriesId}/score-history. Both only read stored records, over
// the real service; the controllers' other dependencies belong to other actions.
public class ScoreHistoryControllerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeDetailController AnimeController(AnimeTrackerDbContext db) =>
        new(detailService: null!, scoreHistoryService: ScoreHistoryServiceTestKit.Create(db), artworkSelectionService: null!,
            pictureRefreshService: null!, tmdbArtwork: null!, metadataRepository: null!);

    private static SeriesController SeriesController(AnimeTrackerDbContext db) =>
        new(seriesService: null!, scoreHistoryService: ScoreHistoryServiceTestKit.Create(db), seriesListService: null!,
            bulkBuildTrigger: null!, bulkBuildProgress: null!, artworkSelectionService: null!, pictureRefreshService: null!,
            tmdbArtwork: null!, metadataRepository: null!);

    private static async Task SeedAsync(AnimeTrackerDbContext db)
    {
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1", AiringStatus = "finished_airing", AiredFrom = new DateOnly(2020, 1, 1) });
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = T0 });
        db.SeriesMembers.Add(new SeriesMember { SeriesId = 1, AnimeId = 1, IsMainLine = true, IsPrimary = true, Order = 0 });
        db.AnimeScoreRecords.Add(new AnimeScoreRecord { AnimeId = 1, Score = 8.31, DetectedAt = T0 });
        db.AnimeScoreRecords.Add(new AnimeScoreRecord { AnimeId = 1, Score = 8.33, DetectedAt = T0.AddDays(3) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task TheAnimeRouteAnswersItsPoints()
    {
        using var db = CreateDb();
        await SeedAsync(db);

        var result = await AnimeController(db).GetScoreHistory(1, CancellationToken.None);

        var history = Assert.IsType<ScoreHistoryDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([8.31, 8.33], history.Points.Select(p => p.Score));
        Assert.Empty(history.Markers);
    }

    [Fact]
    public async Task TheAnimeRouteAnswersNoPointsForAnUnknownAnimeRatherThanNotFound()
    {
        using var db = CreateDb();

        var result = await AnimeController(db).GetScoreHistory(999, CancellationToken.None);

        Assert.Empty(Assert.IsType<ScoreHistoryDto>(Assert.IsType<OkObjectResult>(result).Value).Points);
    }

    [Fact]
    public async Task TheSeriesRouteAnswersTheMainLinesPoints()
    {
        using var db = CreateDb();
        await SeedAsync(db);

        var result = await SeriesController(db).GetScoreHistory(1, CancellationToken.None);

        var history = Assert.IsType<ScoreHistoryDto>(Assert.IsType<OkObjectResult>(result).Value);
        Assert.Equal([8.31, 8.33], history.Points.Select(p => p.Score));
    }

    [Fact]
    public async Task TheSeriesRouteIsNotFoundForAnUnknownSeries()
    {
        using var db = CreateDb();

        var result = await SeriesController(db).GetScoreHistory(999, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task NeitherRouteWritesAnything()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var before = await db.AnimeScoreRecords.CountAsync();

        await AnimeController(db).GetScoreHistory(1, CancellationToken.None);
        await SeriesController(db).GetScoreHistory(1, CancellationToken.None);

        Assert.False(db.ChangeTracker.HasChanges());
        Assert.Equal(before, await db.AnimeScoreRecords.CountAsync());
    }
}
