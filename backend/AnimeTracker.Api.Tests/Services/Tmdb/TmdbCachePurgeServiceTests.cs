using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Tmdb.FakeTmdbClient;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec, "No cached TMDB set is kept for more than five months"
// (design.md D20): TmdbCachePurgeService over EF InMemory. Time passing is
// simulated by seeding FetchedAt relative to UtcNow, as the rest of this suite
// does, with one age just past the 150 days and one just inside them.
public class TmdbCachePurgeServiceTests
{
    private static readonly TimeSpan Expired = TimeSpan.FromDays(151);
    private static readonly TimeSpan JustInside = TimeSpan.FromDays(149);

    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    // Each call is a new context, as each tick's purge step is: the service
    // must find its sets on its own rather than in a tracked graph.
    private static async Task<int> PurgeAsync(
        DbContextOptions<AnimeTrackerDbContext> options, CapturingLogger<TmdbCachePurgeService>? logger = null)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await new TmdbCachePurgeService(db, logger ?? new CapturingLogger<TmdbCachePurgeService>()).PurgeExpiredAsync();
    }

    private sealed record Remaining(
        List<int> TvSets, List<string> TvImages,
        List<(int TvId, int Season)> SeasonSets, List<string> SeasonImages,
        List<int> MovieSets, List<string> MovieImages);

    // "1429:/a.jpg" for an image, so two sets sharing a file path stay apart.
    private static async Task<Remaining> ReadAsync(DbContextOptions<AnimeTrackerDbContext> options)
    {
        using var db = new AnimeTrackerDbContext(options);
        return new Remaining(
            (await db.TmdbTvImageSets.AsNoTracking().ToListAsync()).Select(s => s.TvId).Order().ToList(),
            (await db.TmdbTvImages.AsNoTracking().ToListAsync()).Select(i => $"{i.TvId}:{i.FilePath}").Order().ToList(),
            (await db.TmdbSeasonImageSets.AsNoTracking().ToListAsync()).Select(s => (s.TvId, s.SeasonNumber)).Order().ToList(),
            (await db.TmdbSeasonImages.AsNoTracking().ToListAsync()).Select(i => $"{i.TvId}/{i.SeasonNumber}:{i.FilePath}").Order().ToList(),
            (await db.TmdbMovieImageSets.AsNoTracking().ToListAsync()).Select(s => s.MovieId).Order().ToList(),
            (await db.TmdbMovieImages.AsNoTracking().ToListAsync()).Select(i => $"{i.MovieId}:{i.FilePath}").Order().ToList());
    }

    [Fact]
    public async Task AnExpiredSetIsRemovedWithItsImagesFromEachOfTheThreeStores()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            seed.TmdbTvImageSets.Add(TvSet(1429, Expired, Poster("/tv-a.jpg", "ja"), Backdrop("/tv-b.jpg", null)));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Expired, Poster("/season.jpg", "ja")));
            seed.TmdbMovieImageSets.Add(MovieSet(635302, Expired, Poster("/movie.jpg", "en")));
        });

        var removed = await PurgeAsync(options);

        Assert.Equal(3, removed);
        var left = await ReadAsync(options);
        Assert.Empty(left.TvSets);
        Assert.Empty(left.TvImages);
        Assert.Empty(left.SeasonSets);
        Assert.Empty(left.SeasonImages);
        Assert.Empty(left.MovieSets);
        Assert.Empty(left.MovieImages);
    }

    [Fact]
    public async Task ASetInsideTheLimitIsKeptWithItsImages_EvenOneThatIsDueForARefresh()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            seed.TmdbTvImageSets.Add(TvSet(1429, JustInside, Poster("/tv.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, JustInside, Poster("/season.jpg", "ja")));
            seed.TmdbMovieImageSets.Add(MovieSet(635302, TimeSpan.FromDays(31), Poster("/movie.jpg", "en"))); // stale, not expired
        });

        var removed = await PurgeAsync(options);

        Assert.Equal(0, removed);
        var left = await ReadAsync(options);
        Assert.Equal([1429], left.TvSets);
        Assert.Equal(["1429:/tv.jpg"], left.TvImages);
        Assert.Equal([(1429, 3)], left.SeasonSets);
        Assert.Equal(["1429/3:/season.jpg"], left.SeasonImages);
        Assert.Equal([635302], left.MovieSets);
        Assert.Equal(["635302:/movie.jpg"], left.MovieImages);
    }

    [Fact]
    public async Task OnlyTheExpiredSetsGo_AndTheOnesKeptKeepTheirOwnImages()
    {
        // Show 1429's series set has expired but its season 1 set has not: the
        // stores, and the sets within one, age on their own. Show 2000 shares
        // a file path with it, which must not be lost with the expired set.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            seed.TmdbTvImageSets.Add(TvSet(1429, Expired, Poster("/same.jpg", "ja"), Poster("/only-1429.jpg", "ja")));
            seed.TmdbTvImageSets.Add(TvSet(2000, TmdbTestData.Fresh, Poster("/same.jpg", "ja"), Poster("/only-2000.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, TmdbTestData.Fresh, Poster("/season-1.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 2, Expired, Poster("/season-2.jpg", "ja")));
            seed.TmdbMovieImageSets.Add(MovieSet(7, Expired, Poster("/m.jpg", "en")));
            seed.TmdbMovieImageSets.Add(MovieSet(8, JustInside, Poster("/m.jpg", "en")));
        });

        var removed = await PurgeAsync(options);

        Assert.Equal(3, removed);
        var left = await ReadAsync(options);
        Assert.Equal([2000], left.TvSets);
        Assert.Equal(["2000:/only-2000.jpg", "2000:/same.jpg"], left.TvImages);
        Assert.Equal([(1429, 1)], left.SeasonSets);
        Assert.Equal(["1429/1:/season-1.jpg"], left.SeasonImages);
        Assert.Equal([8], left.MovieSets);
        Assert.Equal(["8:/m.jpg"], left.MovieImages);
    }

    [Fact]
    public async Task ASetFetchedAndEmptyExpiresLikeAnyOther()
    {
        // The row is what says "fetched, and there was nothing"; it is TMDB
        // information of the same age as one holding images.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            seed.TmdbTvImageSets.Add(TvSet(99, Expired));
            seed.TmdbSeasonImageSets.Add(SeasonSet(99, 2, Expired));
        });

        var removed = await PurgeAsync(options);

        Assert.Equal(2, removed);
        var left = await ReadAsync(options);
        Assert.Empty(left.TvSets);
        Assert.Empty(left.SeasonSets);
    }

    [Fact]
    public async Task NothingToRemoveReturnsZeroAndSaysNothing()
    {
        var options = CreateOptions();
        var logger = new CapturingLogger<TmdbCachePurgeService>();
        Assert.Equal(0, await PurgeAsync(options, logger)); // an empty cache

        await SeedAsync(options, seed => seed.TmdbTvImageSets.Add(TvSet(1429, TmdbTestData.Fresh, Poster("/a.jpg", "ja"))));

        Assert.Equal(0, await PurgeAsync(options, logger));
        Assert.Empty(logger.Lines); // it runs every hour: a quiet pass is not news
    }

    [Fact]
    public async Task WhatWasRemovedIsLogged()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            seed.TmdbTvImageSets.Add(TvSet(1429, Expired, Poster("/a.jpg", "ja"), Poster("/b.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Expired, Poster("/c.jpg", "ja")));
        });
        var logger = new CapturingLogger<TmdbCachePurgeService>();

        await PurgeAsync(options, logger);

        var line = Assert.Single(logger.Lines);
        Assert.Equal("Information: Removed 2 cached TMDB image sets (3 images) last fetched more than 150 days ago.", line);
    }

    [Fact]
    public async Task AChosenPictureAndTheMappingAreNotTouchedWhenTheirSetIsRemoved()
    {
        // A picked TMDB picture is stored on the anime or series as its full
        // address (design.md D9), not in the cache: the pages that show it read
        // that row, so it survives its set going (design.md D20).
        var animePick = TmdbImageUrl.Original("/anime-pick.jpg");
        var seriesPick = TmdbImageUrl.Original("/series-pick.jpg");
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            var anime = AddAnime(seed, 1, tvId: 1429, season: 1);
            anime.MalPictureUrl = "https://mal/main.jpg";
            anime.SelectedPictureUrl = animePick;
            anime.ResolvePictureUrl();
            AddSeries(seed, 1, [1]).SelectedPictureUrl = seriesPick;
            seed.TmdbTvImageSets.Add(TvSet(1429, Expired, Poster("/anime-pick.jpg", "ja"), Poster("/series-pick.jpg", "ja")));
        });

        var removed = await PurgeAsync(options);

        Assert.Equal(1, removed);
        using var db = new AnimeTrackerDbContext(options);
        var animeRow = await db.AnimeMetadata.AsNoTracking().SingleAsync(a => a.Id == 1);
        Assert.Equal(animePick, animeRow.SelectedPictureUrl);
        Assert.Equal(animePick, animeRow.PictureUrl); // what My List, Profile and the rest render
        Assert.Equal(seriesPick, (await db.Series.AsNoTracking().SingleAsync(s => s.Id == 1)).SelectedPictureUrl);
        Assert.Single(await db.AnimeIdMappings.AsNoTracking().ToListAsync()); // Fribb's data is not TMDB's
    }

    [Fact]
    public async Task ARemovedSetIsNeverFetchedAgain_SoTheNextVisitFetchesItLikeANewOne()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(1429, Expired, Poster("/old.jpg", "ja")));
        });
        await PurgeAsync(options);

        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), [Image("/new.jpg")]);
        using var db = new AnimeTrackerDbContext(options);
        var service = ArtworkService(db, client);

        Assert.True(await service.IsAnimeFetchDueAsync(1));
        await service.RefreshAnimeAsync(1, force: false);

        Assert.Contains(TmdbSetKey.Tv(1429), client.Calls);
        using var check = new AnimeTrackerDbContext(options);
        var set = await check.TmdbTvImageSets.AsNoTracking().Include(s => s.Images).SingleAsync(s => s.TvId == 1429);
        Assert.Equal(["/new.jpg"], set.Images.Select(i => i.FilePath));
        Assert.InRange(set.FetchedAt, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void TheLimitIsPastTheRefreshIntervalAndInsideTheSixMonthsTheTermsAllow()
    {
        // A set anyone opens is refreshed at 30 days, so it never gets near the
        // limit; and the limit stays at least two weeks under TMDB's six months
        // (taken as 180 days), room for the app having been stopped for a
        // while, since nothing is deleted while it is off.
        Assert.True(TmdbCachePurgeService.MaxAge > TmdbArtworkService.StaleAfter);
        Assert.True(TmdbCachePurgeService.MaxAge <= TimeSpan.FromDays(180) - TimeSpan.FromDays(14));
    }
}
