using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.Tmdb;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Tmdb.FakeTmdbClient;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Controllers;

// AnimeDetailController.RefreshTmdbPictures (anime-detail "The detail response
// carries the anime's TMDB pictures and asks for a TMDB fetch when one is
// due", design.md D11): the one follow-up request the page makes on the
// detail read's flag. It runs over the real TmdbArtworkService and a fake
// TMDB client, so what it answers is what the client would merge.
public class AnimeDetailControllerTmdbTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    // The detail, artwork-selection and picture-refresh dependencies belong to
    // the other actions, which these tests never call.
    private static AnimeDetailController CreateController(AnimeTrackerDbContext db, FakeTmdbClient client, string apiKey = "test-key") =>
        new(detailService: null!, artworkSelectionService: null!, pictureRefreshService: null!,
            tmdbArtwork: ArtworkService(db, client, apiKey), metadataRepository: new AnimeMetadataRepository(db));

    // The action answers with an anonymous { tmdb, tmdbFetchPending }, as its
    // neighbours answer; reading it back by property name is what the
    // client's JSON parse does.
    private static T Read<T>(IActionResult result, string property)
    {
        var body = Assert.IsType<OkObjectResult>(result).Value!;
        return (T)body.GetType().GetProperty(property)!.GetValue(body)!;
    }

    [Fact]
    public async Task AnAnimeThatIsNotCachedIs404AndNothingIsFetched()
    {
        using var db = CreateDb();
        var client = new FakeTmdbClient();

        var result = await CreateController(db, client).RefreshTmdbPictures(99, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task AnAnimeNotInMyListGetsNullAndFalseWithoutAFetch()
    {
        using var db = CreateDb();
        AddAnime(db, 1, inMyList: false, tvId: 1429, season: 3); // mapped, and its sets are due — but there is no picture to choose
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        Assert.Null(Read<AnimeTmdbPicturesDto?>(result, "tmdb"));
        Assert.False(Read<bool>(result, "tmdbFetchPending"));
        Assert.Empty(client.Calls);
    }

    [Fact]
    public async Task AMyListAnimeHasItsDueSetsFetchedAndTheImagesReturnedWithNothingPending()
    {
        using var db = CreateDb();
        AddAnime(db, 1, tvId: 1429, season: 3);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient()
            .Serve(TmdbSetKey.Tv(1429), [Image("/tv.jpg", "ja")])
            .Serve(TmdbSetKey.Season(1429, 3), [Image("/s3.jpg", "en")]);

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        Assert.Equal([TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 3)], client.Calls); // its two due sets, and no others
        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(Read<AnimeTmdbPicturesDto?>(result, "tmdb"));
        Assert.Equal([TmdbScope.Series, TmdbScope.Season], tmdb.Scopes.Select(scope => scope.Scope));
        Assert.Equal($"{ImageBase}/s3.jpg", Assert.Single(Assert.Single(tmdb.Scopes[1].Languages).Pictures).Url);
        Assert.False(Read<bool>(result, "tmdbFetchPending"));
    }

    [Fact]
    public async Task FreshSetsAreNotFetchedAgain()
    {
        using var db = CreateDb();
        AddAnime(db, 1, tvId: 1429, season: 3);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja")));
        db.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Fresh, Poster("/s3.jpg", "en")));
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        Assert.Empty(client.Calls); // not "Refresh data": that forces, this asks for what is due
        Assert.Equal(2, Read<AnimeTmdbPicturesDto?>(result, "tmdb")!.Scopes.Count);
        Assert.False(Read<bool>(result, "tmdbFetchPending"));
    }

    [Fact]
    public async Task WhenTmdbFailsTheActionStillSucceedsAndTheSetsStayPending()
    {
        using var db = CreateDb();
        AddAnime(db, 1, tvId: 1429, season: 3);
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient { DefaultResult = new TmdbImagesResult.Failed() };

        var result = await CreateController(db, client).RefreshTmdbPictures(1, CancellationToken.None);

        var tmdb = Assert.IsType<AnimeTmdbPicturesDto>(Read<AnimeTmdbPicturesDto?>(result, "tmdb"));
        Assert.True(tmdb.HasMapping);
        Assert.Empty(tmdb.Scopes);
        Assert.True(Read<bool>(result, "tmdbFetchPending")); // a later visit tries again
    }

    [Fact]
    public async Task WithNoKeyNothingIsFetchedAndNothingIsPending()
    {
        using var db = CreateDb();
        AddAnime(db, 1, tvId: 1429, season: 3);
        db.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"))); // cached while a key was set
        await db.SaveChangesAsync();
        var client = new FakeTmdbClient();

        var result = await CreateController(db, client, apiKey: "").RefreshTmdbPictures(1, CancellationToken.None);

        Assert.Empty(client.Calls);
        Assert.Equal(TmdbScope.Series, Assert.Single(Read<AnimeTmdbPicturesDto?>(result, "tmdb")!.Scopes).Scope);
        Assert.False(Read<bool>(result, "tmdbFetchPending"));
    }
}
