using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Series;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Tmdb.FakeTmdbClient;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Controllers;

// SeriesController.RefreshTmdbPictures (tmdb-artwork "A series page fetches its
// TMDB sets within a bounded budget", series-page "The series read carries the
// franchise's TMDB pictures and fetches due sets afterwards", design.md D10,
// D11): the one follow-up request a series visit makes. It runs over the real
// TmdbArtworkService and a fake TMDB client.
public class SeriesControllerTmdbTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // Only the series lookup and the TMDB service are reached; the list, bulk
    // build, artwork-selection, picture-refresh and metadata dependencies
    // belong to the other actions.
    private static SeriesController CreateController(AnimeTrackerDbContext db, FakeTmdbClient client, string apiKey = "test-key") =>
        new(seriesService: new FakeSeriesService(db), seriesListService: null!, bulkBuildTrigger: null!, bulkBuildProgress: null!,
            artworkSelectionService: null!, pictureRefreshService: null!,
            tmdbArtwork: ArtworkService(db, client, apiKey), metadataRepository: null!);

    private static SeriesTmdbPicturesDto Body(IActionResult result) =>
        Assert.IsType<SeriesTmdbPicturesDto>(Assert.IsType<OkObjectResult>(result).Value);

    // One series whose main line holds `count` anime with a film of its own
    // each: `count` sets, all due.
    private static async Task SeedSeriesWithDueSetsAsync(AnimeTrackerDbContext db, int count)
    {
        var members = Enumerable.Range(1, count).ToArray();
        foreach (var id in members)
            AddAnime(db, id, movieIds: [1000 + id]);
        AddSeries(db, 1, members);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AnAnimeInNoSeriesIs404AndNothingIsFetched()
    {
        using var db = CreateDb();
        AddAnime(db, 1, movieIds: [635302]); // mapped, but a member of no series
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task ASeriesWithFortyFiveDueSetsFetchesTwentyAndReportsTwentyFiveRemaining()
    {
        using var db = CreateDb();
        await SeedSeriesWithDueSetsAsync(db, 45);
        var client = new FakeTmdbClient();

        var body = Body(await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None));

        Assert.Equal(TmdbArtworkService.SeriesFetchBudget, client.Calls.Count);
        Assert.Equal(20, client.Calls.Count);
        Assert.Equal(25, body.PendingCount);
    }

    [Fact]
    public async Task EachLaterVisitContinuesWhereTheLastStoppedUntilNothingIsDue()
    {
        using var db = CreateDb();
        await SeedSeriesWithDueSetsAsync(db, 45);
        var client = new FakeTmdbClient();
        var controller = CreateController(db, client);

        var remaining = new List<int>();
        for (var visit = 0; visit < 3; visit++)
            remaining.Add(Body(await controller.RefreshTmdbPictures(1, CancellationToken.None)).PendingCount);

        Assert.Equal([25, 5, 0], remaining);
        Assert.Equal(45, client.Calls.Distinct().Count()); // every set fetched, none twice
        Assert.Equal(45, client.Calls.Count);

        await controller.RefreshTmdbPictures(1, CancellationToken.None);
        Assert.Equal(45, client.Calls.Count); // a fourth visit finds nothing due
    }

    [Fact]
    public async Task TheSeriesIsFoundFromAnyMemberAndItsImagesAreReturned()
    {
        using var db = CreateDb();
        AddAnime(db, 1, tvId: 1429, season: 1);
        AddAnime(db, 2, movieIds: [635302]);
        AddSeries(db, 1, [1], (2, null, 0));
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient()
            .Serve(TmdbSetKey.Tv(1429), [Image("/tv.jpg", "ja")])
            .Serve(TmdbSetKey.Movie(635302), [Image("/film.jpg", "ja")]);

        var body = Body(await CreateController(db, client).RefreshTmdbPictures(2, CancellationToken.None)); // arrived from the film

        Assert.True(body.HasMapping);
        Assert.Equal(0, body.PendingCount);
        var japanese = Assert.Single(body.Languages);
        Assert.Equal(["/tv.jpg", "/film.jpg"], japanese.Pictures.Select(p => p.Url[ImageBase.Length..]));
    }

    [Fact]
    public async Task ASetThatFailedToFetchIsStillCountedAsDue()
    {
        using var db = CreateDb();
        await SeedSeriesWithDueSetsAsync(db, 3);
        var client = new FakeTmdbClient { DefaultResult = new TmdbImagesResult.Failed() };

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        Assert.Equal(3, Body(result).PendingCount); // the action succeeded; a later visit retries all three
    }

    [Fact]
    public async Task WithNoKeyNothingIsFetchedAndNothingIsPending()
    {
        using var db = CreateDb();
        await SeedSeriesWithDueSetsAsync(db, 3);
        db.TmdbMovieImageSets.Add(MovieSet(1001, Fresh, Poster("/cached.jpg", "ja"))); // cached while a key was set
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var body = Body(await CreateController(db, client, apiKey: "").RefreshTmdbPictures(1, CancellationToken.None));

        Assert.Empty(client.Calls);
        Assert.Equal(0, body.PendingCount);
        Assert.Single(Assert.Single(body.Languages).Pictures);
    }

    // The controller only asks which series an anime belongs to, as the real
    // service answers it: through the anime's primary membership.
    private sealed class FakeSeriesService(AnimeTrackerDbContext db) : ISeriesService
    {
        public Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default) =>
            db.SeriesMembers.AsNoTracking()
                .Where(m => m.AnimeId == animeId && m.IsPrimary)
                .Select(m => (int?)m.SeriesId)
                .FirstOrDefaultAsync(ct);

        public Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
