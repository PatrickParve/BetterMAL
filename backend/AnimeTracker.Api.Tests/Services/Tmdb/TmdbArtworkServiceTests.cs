using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Infrastructure;
using AnimeTracker.Api.Services.Tmdb;
using AnimeTracker.Api.Tests.Services.IdMapping;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Tmdb.FakeTmdbClient;
using static AnimeTracker.Api.Tests.Services.Tmdb.TmdbTestData;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec: TmdbArtworkService over EF InMemory and a fake
// ITmdbClient — the read side (grouping, order, de-duplication, mapping and
// pending flags) and the fetch side (which sets are fetched, when, how often,
// and what a result does to the cache). Time passing is simulated by seeding
// FetchedAt relative to UtcNow, as the rest of this suite does.
public class TmdbArtworkServiceTests
{
    private static DbContextOptions<AnimeTrackerDbContext> CreateOptions() =>
        new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

    private static TmdbArtworkService CreateService(
        AnimeTrackerDbContext db, FakeTmdbClient client, string apiKey = "test-key", RefreshGate? gate = null) =>
        ArtworkService(db, client, apiKey, gate);

    // --- Reading back ---

    private static async Task<TmdbTvImageSet?> ReadTvSetAsync(DbContextOptions<AnimeTrackerDbContext> options, int tvId)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.TmdbTvImageSets.AsNoTracking().Include(s => s.Images).FirstOrDefaultAsync(s => s.TvId == tvId);
    }

    private static async Task<TmdbSeasonImageSet?> ReadSeasonSetAsync(DbContextOptions<AnimeTrackerDbContext> options, int tvId, int season)
    {
        using var db = new AnimeTrackerDbContext(options);
        return await db.TmdbSeasonImageSets.AsNoTracking().Include(s => s.Images)
            .FirstOrDefaultAsync(s => s.TvId == tvId && s.SeasonNumber == season);
    }

    private static void AssertJustNow(DateTimeOffset stamp) =>
        Assert.InRange(stamp, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddSeconds(1));

    // "Series/ja: /a.jpg Poster, /b.jpg Backdrop" — one line per language group, in the order sent.
    private static List<string> Describe(AnimeTmdbPicturesDto dto) =>
        dto.Scopes.SelectMany(scope => scope.Languages.Select(group =>
            $"{scope.Scope}{(scope.SeasonNumber is { } n ? $" {n}" : "")}/{group.Language}: {DescribePictures(group)}")).ToList();

    private static List<string> Describe(SeriesTmdbPicturesDto dto) =>
        dto.Languages.Select(group => $"{group.Language}: {DescribePictures(group)}").ToList();

    private static string DescribePictures(TmdbLanguageGroupDto group) =>
        string.Join(", ", group.Pictures.Select(p => $"{p.Url[ImageBase.Length..]} {p.Kind}"));

    // =====================================================================
    // Fetch side
    // =====================================================================

    [Fact]
    public async Task TwoAnimeSharingATvIdCauseOneClientCallForIt()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            AddAnime(seed, 2, tvId: 1429, season: 2);
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);
        await service.RefreshAnimeAsync(2, force: false);

        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Tv(1429))); // shared by both seasons
        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Season(1429, 1)));
        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Season(1429, 2)));
        Assert.Equal(3, client.Calls.Count);
    }

    [Fact]
    public async Task ASetTwentyNineDaysOldIsNotRefetchedAndOneThirtyOneDaysOldIs()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(29), Poster("/old-tv.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, TimeSpan.FromDays(31), Poster("/old-season.jpg", "ja")));
        });
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Season(1429, 1), [Image("/new-season.jpg")]);
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        Assert.Equal([TmdbSetKey.Season(1429, 1)], client.Calls); // only the stale one
        var tv = await ReadTvSetAsync(options, 1429);
        Assert.Equal(["/old-tv.jpg"], tv!.Images.Select(i => i.FilePath)); // untouched
        var season = await ReadSeasonSetAsync(options, 1429, 1);
        Assert.Equal(["/new-season.jpg"], season!.Images.Select(i => i.FilePath)); // replaced
        AssertJustNow(season.FetchedAt);
    }

    [Fact]
    public async Task ForceRefetchesAFreshSet()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/old.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Fresh));
        });
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), [Image("/new.jpg")]);
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);
        Assert.Empty(client.Calls); // fresh: nothing due

        await service.RefreshAnimeAsync(1, force: true);

        Assert.Equal([TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 1)], client.Calls);
        Assert.Equal(["/new.jpg"], (await ReadTvSetAsync(options, 1429))!.Images.Select(i => i.FilePath));
    }

    [Fact]
    public async Task NotFoundStoresAnEmptySetThatIsNotRequestedAgain()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 1429));
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), new TmdbImagesResult.NotFound());
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);

        var set = await ReadTvSetAsync(options, 1429);
        Assert.NotNull(set); // "fetched", which is not "never fetched"
        Assert.Empty(set.Images);
        AssertJustNow(set.FetchedAt);

        await service.RefreshAnimeAsync(1, force: false);
        Assert.Single(client.Calls); // not requested again until it is due
        Assert.False(await service.IsAnimeFetchDueAsync(1));
    }

    [Fact]
    public async Task NotFoundEmptiesASetThatHeldImages()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(40), Poster("/gone.jpg", "ja")));
        });
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), new TmdbImagesResult.NotFound());
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        var set = await ReadTvSetAsync(options, 1429);
        Assert.Empty(set!.Images);
        AssertJustNow(set.FetchedAt);
    }

    [Fact]
    public async Task FailedKeepsThePreviousImagesAndTheOldTimestamp()
    {
        var options = CreateOptions();
        var fetchedAt = DateTimeOffset.UtcNow - TimeSpan.FromDays(40);
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(40), Poster("/keep-a.jpg", "ja"), Backdrop("/keep-b.jpg", null)));
        });
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), new TmdbImagesResult.Failed());
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);

        var set = await ReadTvSetAsync(options, 1429);
        Assert.Equal(["/keep-a.jpg", "/keep-b.jpg"], set!.Images.Select(i => i.FilePath).Order());
        Assert.InRange(set.FetchedAt, fetchedAt.AddMinutes(-1), fetchedAt.AddMinutes(1)); // not restamped
        Assert.True(await service.IsAnimeFetchDueAsync(1)); // so a later visit retries it
    }

    [Fact]
    public async Task FailedOnANeverFetchedSetStoresNothing()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 1429));
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), new TmdbImagesResult.Failed());
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        Assert.Null(await ReadTvSetAsync(options, 1429)); // still never fetched, not "fetched and empty"
    }

    [Fact]
    public async Task AnAnimeNotInMyListMakesNoCalls()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, inMyList: false, tvId: 1429, season: 1));
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);
        await service.RefreshAnimeAsync(1, force: true);

        Assert.Empty(client.Calls);
        Assert.Null(await service.GetAnimePicturesAsync(1));
        Assert.False(await service.IsAnimeFetchDueAsync(1));
    }

    [Fact]
    public async Task AMissingKeyMakesNoCallsAndReportsNothingPending()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            AddSeries(seed, 10, mainLine: [1]);
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client, apiKey: "");

        await service.RefreshAnimeAsync(1, force: false);
        await service.RefreshAnimeAsync(1, force: true);
        var remaining = await service.RefreshSeriesAsync(10, budget: 20, force: false);
        await service.RefreshSeriesAsync(10, budget: int.MaxValue, force: true);

        Assert.Empty(client.Calls);
        Assert.Equal(0, remaining);
        Assert.False(await service.IsAnimeFetchDueAsync(1));
        Assert.Equal(0, (await service.GetSeriesPicturesAsync(10)).PendingCount);
    }

    [Fact]
    public async Task ASeriesBudgetOfTwentyAgainstFortyFiveDueKeysMakesTwentyCallsAndReturnsTwentyFive()
    {
        // 45 main-line members, each a show of its own and none of them in my
        // list: whether members are in my list does not limit a series fetch.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            for (var i = 0; i < 45; i++)
                AddAnime(seed, 100 + i, inMyList: false, tvId: 5000 + i);
            AddSeries(seed, 100, mainLine: Enumerable.Range(100, 45).ToArray());
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        var remaining = await service.RefreshSeriesAsync(100, TmdbArtworkService.SeriesFetchBudget, force: false);

        Assert.Equal(20, client.Calls.Count);
        Assert.Equal(25, remaining);
        Assert.Equal(Enumerable.Range(5000, 20).Select(TmdbSetKey.Tv), client.Calls); // key order: main-line order
        Assert.Equal(25, (await service.GetSeriesPicturesAsync(100)).PendingCount);

        // Later visits continue until none remain.
        Assert.Equal(5, await service.RefreshSeriesAsync(100, TmdbArtworkService.SeriesFetchBudget, force: false));
        Assert.Equal(0, await service.RefreshSeriesAsync(100, TmdbArtworkService.SeriesFetchBudget, force: false));
        Assert.Equal(45, client.Calls.Count);
        Assert.Equal(45, client.Calls.Distinct().Count());

        // Nothing due: no request.
        Assert.Equal(0, await service.RefreshSeriesAsync(100, TmdbArtworkService.SeriesFetchBudget, force: false));
        Assert.Equal(45, client.Calls.Count);
    }

    [Fact]
    public async Task AForcedSeriesRefreshWithNoBudgetFetchesEverySetEvenFreshOnes()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, inMyList: false, tvId: 1429, season: 1);
            AddAnime(seed, 2, inMyList: false, movieIds: [635302]);
            AddSeries(seed, 1, [1], (2, "SideStory", 0));
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Fresh));
            seed.TmdbMovieImageSets.Add(MovieSet(635302, Fresh));
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        Assert.Equal(0, await service.RefreshSeriesAsync(1, TmdbArtworkService.SeriesFetchBudget, force: false));
        Assert.Empty(client.Calls);

        var remaining = await service.RefreshSeriesAsync(1, int.MaxValue, force: true);

        Assert.Equal(0, remaining);
        Assert.Equal([TmdbSetKey.Tv(1429), TmdbSetKey.Season(1429, 1), TmdbSetKey.Movie(635302)], client.Calls);
    }

    [Fact]
    public async Task ASeriesFetchesItsExtrasMoviesInTheMoreSectionsDisplayOrder()
    {
        // Extras display by relation group (Prequel before SideStory before
        // an unrecognised group, which counts as Other), then by position.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, inMyList: false, tvId: 1429);
            AddAnime(seed, 20, inMyList: false, movieIds: [20]);
            AddAnime(seed, 10, inMyList: false, movieIds: [10]);
            AddAnime(seed, 30, inMyList: false, movieIds: [30]);
            AddAnime(seed, 40, inMyList: false, movieIds: [40]);
            AddSeries(seed, 1, [1], (20, "SideStory", 0), (10, "Prequel", 0), (30, null, 0), (40, "SideStory", 1));
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshSeriesAsync(1, int.MaxValue, force: false);

        Assert.Equal(
            [TmdbSetKey.Tv(1429), TmdbSetKey.Movie(10), TmdbSetKey.Movie(20), TmdbSetKey.Movie(40), TmdbSetKey.Movie(30)],
            client.Calls);
    }

    [Fact]
    public async Task ConcurrentRefreshesOfAnimeSharingAKeyProduceOneCallForIt()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            AddAnime(seed, 2, tvId: 1429, season: 2);
        });
        var gate = new RefreshGate();
        var client = new FakeTmdbClient();
        var release = new TaskCompletionSource();
        var firstCallEntered = new TaskCompletionSource();
        client.BeforeAnswer = async _ =>
        {
            firstCallEntered.TrySetResult();
            await release.Task;
        };
        using var firstDb = new AnimeTrackerDbContext(options);
        using var secondDb = new AnimeTrackerDbContext(options);

        var first = CreateService(firstDb, client, gate: gate).RefreshAnimeAsync(1, force: false);
        await firstCallEntered.Task; // the first is inside the TMDB call, holding the TV set's lock
        var second = CreateService(secondDb, client, gate: gate).RefreshAnimeAsync(2, force: false);
        await Task.Delay(50); // long enough for the second to reach that lock and queue behind it
        Assert.False(second.IsCompleted);
        release.SetResult();
        await Task.WhenAll(first, second);

        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Tv(1429)));
        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Season(1429, 1)));
        Assert.Equal(1, client.Calls.Count(k => k == TmdbSetKey.Season(1429, 2)));
    }

    // --- What a fetch does to the stored set ---

    [Fact]
    public async Task AnImagesResultReplacesTheSetWholesale()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(40),
                Poster("/a.jpg", "ja", 0), Poster("/b.jpg", "ja", 1), Backdrop("/c.jpg", null)));
        });
        // /a.jpg changes kind, /b.jpg changes language and position, /c.jpg is
        // gone, /d.jpg is new.
        var client = new FakeTmdbClient().Serve(
            TmdbSetKey.Tv(1429),
            posters: [Image("/b.jpg", "en", 1000, 1500), Image("/d.jpg", "ja")],
            backdrops: [Image("/a.jpg", null, 3840, 2160)]);
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        var set = await ReadTvSetAsync(options, 1429);
        AssertJustNow(set!.FetchedAt);
        var rows = set.Images.OrderBy(i => i.FilePath).ToList();
        Assert.Equal(["/a.jpg", "/b.jpg", "/d.jpg"], rows.Select(i => i.FilePath));
        Assert.Equal((TmdbImageKind.Backdrop, (string?)null, 3840, 2160, 0), (rows[0].Kind, rows[0].Language, rows[0].Width, rows[0].Height, rows[0].Position));
        Assert.Equal((TmdbImageKind.Poster, (string?)"en", 1000, 1500, 0), (rows[1].Kind, rows[1].Language, rows[1].Width, rows[1].Height, rows[1].Position));
        Assert.Equal((TmdbImageKind.Poster, (string?)"ja", 2000, 3000, 1), (rows[2].Kind, rows[2].Language, rows[2].Width, rows[2].Height, rows[2].Position));
    }

    [Fact]
    public async Task ImagesKeepTmdbsOrderWithinEachKind()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 1429, season: 1));
        var client = new FakeTmdbClient()
            .Serve(TmdbSetKey.Tv(1429), [Image("/p0.jpg"), Image("/p1.jpg"), Image("/p2.jpg")], [Image("/b0.jpg", null), Image("/b1.jpg", null)])
            .Serve(TmdbSetKey.Season(1429, 1), [Image("/s0.jpg"), Image("/s1.jpg")]);
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        var tv = await ReadTvSetAsync(options, 1429);
        Assert.Equal([0, 1, 2], tv!.Images.Where(i => i.Kind == TmdbImageKind.Poster).OrderBy(i => i.Position).Select(i => i.Position));
        Assert.Equal(["/b0.jpg", "/b1.jpg"], tv.Images.Where(i => i.Kind == TmdbImageKind.Backdrop).OrderBy(i => i.Position).Select(i => i.FilePath));
        var season = await ReadSeasonSetAsync(options, 1429, 1);
        Assert.Equal(["/s0.jpg", "/s1.jpg"], season!.Images.OrderBy(i => i.Position).Select(i => i.FilePath));
    }

    [Fact]
    public async Task APathTmdbListsTwiceIsStoredOnce()
    {
        // The primary key is (set, file path): a path in both arrays must not
        // fail the save, or the set could never be fetched.
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 1429));
        var client = new FakeTmdbClient().Serve(
            TmdbSetKey.Tv(1429), posters: [Image("/twice.jpg"), Image("/once.jpg")], backdrops: [Image("/twice.jpg", null)]);
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: false);

        var set = await ReadTvSetAsync(options, 1429);
        Assert.Equal(["/once.jpg", "/twice.jpg"], set!.Images.Select(i => i.FilePath).Order());
        Assert.Equal(TmdbImageKind.Poster, set.Images.Single(i => i.FilePath == "/twice.jpg").Kind); // the first occurrence wins
    }

    [Fact]
    public async Task ASeasonSetAndAMovieSetAreStoredUnderTheirOwnKeys()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 85937, season: 2);
            AddAnime(seed, 2, movieIds: [7001, 7000]);
        });
        var client = new FakeTmdbClient()
            .Serve(TmdbSetKey.Season(85937, 2), [Image("/season.jpg")])
            .Serve(TmdbSetKey.Movie(7001), [Image("/m1.jpg")])
            .Serve(TmdbSetKey.Movie(7000), [Image("/m0.jpg")]);
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);
        await service.RefreshAnimeAsync(2, force: false);

        Assert.Equal(["/season.jpg"], (await ReadSeasonSetAsync(options, 85937, 2))!.Images.Select(i => i.FilePath));
        using var read = new AnimeTrackerDbContext(options);
        Assert.Equal(["/m1.jpg"], (await read.TmdbMovieImages.AsNoTracking().Where(i => i.MovieId == 7001).ToListAsync()).Select(i => i.FilePath));
        Assert.Equal(["/m0.jpg"], (await read.TmdbMovieImages.AsNoTracking().Where(i => i.MovieId == 7000).ToListAsync()).Select(i => i.FilePath));
    }

    [Fact]
    public async Task SeasonZeroIsNeverFetched()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 46260, season: 0));
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);

        await CreateService(db, client).RefreshAnimeAsync(1, force: true);

        Assert.Equal([TmdbSetKey.Tv(46260)], client.Calls);
    }

    [Fact]
    public async Task AFetchLeavesNothingOfTheSetTrackedOnTheContext()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(40), Poster("/old.jpg", "ja")));
        });
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), [Image("/new.jpg")]);
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        await service.RefreshAnimeAsync(1, force: false);
        await service.RefreshAnimeAsync(1, force: true); // a second store for the same key, on the same context

        Assert.Empty(db.ChangeTracker.Entries()); // shared with the caller's page or action, so nothing stale is left on it
        Assert.Equal(["/new.jpg"], (await ReadTvSetAsync(options, 1429))!.Images.Select(i => i.FilePath));
    }

    [Fact]
    public async Task AFailedSaveLeavesNothingStagedOnTheContext()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, tvId: 1429));
        var client = new FakeTmdbClient().Serve(TmdbSetKey.Tv(1429), [Image("/new.jpg")]);
        using var db = new FailingSaveContext(options);
        var service = CreateService(db, client);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.RefreshAnimeAsync(1, force: false));

        // The next unrelated SaveChanges in this scope must not retry it.
        Assert.Empty(db.ChangeTracker.Entries());
        Assert.Null(await ReadTvSetAsync(options, 1429));
    }

    // =====================================================================
    // Read side
    // =====================================================================

    [Fact]
    public async Task AnAnimesPicturesAreGroupedByScopeThenLanguageWithPostersFirstAndNoDuplicates()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 3);
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh,
                Poster("/tv-none.jpg", null),
                Poster("/tv-ja-b.jpg", "ja", 1),
                Poster("/tv-ja-a.jpg", "ja", 0),
                Poster("/tv-en.jpg", "en"),
                Poster("/dup.jpg", "ja", 2),
                Backdrop("/tv-back-ja.jpg", "ja"),
                Backdrop("/tv-back-none.jpg", null)));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Fresh,
                Poster("/dup.jpg", "ja"), // also in the series set: the earlier scope keeps it
                Poster("/s3-en-b.jpg", "en", 1),
                Poster("/s3-en-a.jpg", "en", 0)));
        });
        using var db = new AnimeTrackerDbContext(options);

        var dto = await CreateService(db, new FakeTmdbClient()).GetAnimePicturesAsync(1);

        Assert.NotNull(dto);
        Assert.True(dto.HasMapping);
        Assert.Equal(
        [
            "Series/none: /tv-none.jpg Poster, /tv-back-none.jpg Backdrop",
            "Series/ja: /tv-ja-a.jpg Poster, /tv-ja-b.jpg Poster, /dup.jpg Poster, /tv-back-ja.jpg Backdrop",
            "Series/en: /tv-en.jpg Poster",
            // No ja group for the season: its only ja image is the duplicate.
            "Season 3/en: /s3-en-a.jpg Poster, /s3-en-b.jpg Poster",
        ],
        Describe(dto));
    }

    [Fact]
    public async Task AnAnimesPicturesCarryTheOriginalUrlAndTheImagesSize()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429);
            seed.TmdbTvImageSets.Add(new TmdbTvImageSet
            {
                TvId = 1429,
                FetchedAt = DateTimeOffset.UtcNow,
                Images = [new TmdbTvImage { TvId = 1429, FilePath = "/abc123.jpg", Kind = TmdbImageKind.Backdrop, Language = null, Width = 3840, Height = 2160, Position = 0 }],
            });
        });
        using var db = new AnimeTrackerDbContext(options);

        var dto = await CreateService(db, new FakeTmdbClient()).GetAnimePicturesAsync(1);

        var picture = Assert.Single(Assert.Single(Assert.Single(dto!.Scopes).Languages).Pictures);
        Assert.Equal("https://image.tmdb.org/t/p/original/abc123.jpg", picture.Url); // built on read; the cache holds only the path
        Assert.Equal((TmdbImageKind.Backdrop, 3840, 2160), (picture.Kind, picture.Width, picture.Height));
    }

    [Fact]
    public async Task EmptyGroupsAndEmptyScopesAreOmitted()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 2, movieIds: [635302]);
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv-ja.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 2, Fresh)); // fetched and empty
            // The movie set has never been fetched.
        });
        using var db = new AnimeTrackerDbContext(options);

        var dto = await CreateService(db, new FakeTmdbClient()).GetAnimePicturesAsync(1);

        Assert.Equal(["Series/ja: /tv-ja.jpg Poster"], Describe(dto!)); // no Season, Movie, none or en
    }

    [Fact]
    public async Task TwoMovieIdsFormOneMovieScopeHoldingBothSetsImages()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, movieIds: [7001, 7000]);
            seed.TmdbMovieImageSets.Add(MovieSet(7000, Fresh, Poster("/second.jpg", "ja")));
            seed.TmdbMovieImageSets.Add(MovieSet(7001, Fresh, Poster("/first.jpg", "ja")));
        });
        using var db = new AnimeTrackerDbContext(options);

        var dto = await CreateService(db, new FakeTmdbClient()).GetAnimePicturesAsync(1);

        Assert.Equal(["Movie/ja: /first.jpg Poster, /second.jpg Poster"], Describe(dto!)); // the mapping's id order
    }

    [Fact]
    public async Task HasMappingIsFalseWithNoMappingRowOrAnImdbOnlyOne()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, withMapping: false);
            AddAnime(seed, 2, imdbIds: ["tt2560140"]);
            AddAnime(seed, 3, tvId: 1429);
        });
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        var none = await service.GetAnimePicturesAsync(1);
        var imdbOnly = await service.GetAnimePicturesAsync(2);
        var mapped = await service.GetAnimePicturesAsync(3);

        Assert.False(none!.HasMapping);
        Assert.Empty(none.Scopes);
        Assert.False(imdbOnly!.HasMapping);
        Assert.Empty(imdbOnly.Scopes);
        Assert.True(mapped!.HasMapping); // mapped, though nothing is cached yet
        Assert.Empty(mapped.Scopes);
        Assert.Empty(client.Calls); // reading never asks TMDB
    }

    [Fact]
    public async Task IsAnimeFetchDueNeedsAKeyMyListAndAtLeastOneDueSet()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1); // nothing cached: due
            AddAnime(seed, 2, tvId: 2000, season: 1); // both fresh
            AddAnime(seed, 3, tvId: 3000, season: 1); // series fresh, season 31 days old: due
            AddAnime(seed, 4, inMyList: false, tvId: 4000); // due, but not in my list
            AddAnime(seed, 5, imdbIds: ["tt1"]); // nothing to fetch
            seed.TmdbTvImageSets.Add(TvSet(2000, Fresh));
            seed.TmdbSeasonImageSets.Add(SeasonSet(2000, 1, Fresh));
            seed.TmdbTvImageSets.Add(TvSet(3000, Fresh));
            seed.TmdbSeasonImageSets.Add(SeasonSet(3000, 1, TimeSpan.FromDays(31)));
        });
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, new FakeTmdbClient());

        Assert.True(await service.IsAnimeFetchDueAsync(1));
        Assert.False(await service.IsAnimeFetchDueAsync(2));
        Assert.True(await service.IsAnimeFetchDueAsync(3));
        Assert.False(await service.IsAnimeFetchDueAsync(4));
        Assert.False(await service.IsAnimeFetchDueAsync(5));
        Assert.False(await CreateService(db, new FakeTmdbClient(), apiKey: "").IsAnimeFetchDueAsync(1)); // no key

    }

    [Fact]
    public async Task CachedImagesAreStillOfferedAfterTheKeyIsRemoved()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429);
            seed.TmdbTvImageSets.Add(TvSet(1429, TimeSpan.FromDays(90), Poster("/kept.jpg", "ja"))); // long stale, key long gone
        });
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, new FakeTmdbClient(), apiKey: "");

        var dto = await service.GetAnimePicturesAsync(1);
        var urls = await service.GetAnimeOptionUrlsAsync(1);

        Assert.Equal(["Series/ja: /kept.jpg Poster"], Describe(dto!));
        Assert.Equal([$"{ImageBase}/kept.jpg"], urls);
    }

    [Fact]
    public async Task TheDtosSayWhetherAKeyIsConfigured()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, withMapping: false);
            AddSeries(seed, 1, mainLine: [1]);
        });
        using var db = new AnimeTrackerDbContext(options);
        var withKey = CreateService(db, new FakeTmdbClient());
        var noKey = CreateService(db, new FakeTmdbClient(), apiKey: "");

        Assert.True((await withKey.GetAnimePicturesAsync(1))!.Configured);
        Assert.True((await withKey.GetSeriesPicturesAsync(1)).Configured);
        // Both DTOs are still returned with no key, since cached images outlive
        // it, so this flag is how the picker tells "TMDB has no match" from
        // "TMDB is off".
        Assert.False((await noKey.GetAnimePicturesAsync(1))!.Configured);
        Assert.False((await noKey.GetSeriesPicturesAsync(1)).Configured);
    }

    [Fact]
    public async Task AnAnimesOptionUrlsAreExactlyThePictureUrlsInThePickersOrder()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 3, imdbIds: ["tt1"]);
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja"), Backdrop("/tv-b.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 3, Fresh, Poster("/tv.jpg", "ja"), Poster("/s3.jpg", "en")));
        });
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, new FakeTmdbClient());

        var urls = await service.GetAnimeOptionUrlsAsync(1);

        Assert.Equal([$"{ImageBase}/tv.jpg", $"{ImageBase}/tv-b.jpg", $"{ImageBase}/s3.jpg"], urls); // no duplicate
    }

    [Fact]
    public async Task AnimeWithoutAMappingHaveNoOptionUrls()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, withMapping: false));
        using var db = new AnimeTrackerDbContext(options);

        Assert.Empty(await CreateService(db, new FakeTmdbClient()).GetAnimeOptionUrlsAsync(1));
    }

    [Fact]
    public async Task ASeriesPicturesMixSeriesSeasonAndMovieImagesUnderLanguagesOnly()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 100, season: 1);
            AddAnime(seed, 2, tvId: 100, season: 2);
            AddAnime(seed, 3, movieIds: [900]);
            AddSeries(seed, 1, [1, 2], (3, "SideStory", 0));
            seed.TmdbTvImageSets.Add(TvSet(100, Fresh, Poster("/tv-ja.jpg", "ja"), Backdrop("/tv-back-ja.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(100, 1, Fresh, Poster("/s1-ja.jpg", "ja")));
            // Season 2 has never been fetched: one due set.
            seed.TmdbMovieImageSets.Add(MovieSet(900, Fresh,
                Poster("/m-ja.jpg", "ja"), Backdrop("/m-back-ja.jpg", "ja"), Poster("/m-en.jpg", "en")));
        });
        using var db = new AnimeTrackerDbContext(options);

        var dto = await CreateService(db, new FakeTmdbClient()).GetSeriesPicturesAsync(1);

        Assert.True(dto.HasMapping);
        Assert.Equal(1, dto.PendingCount);
        // No Series/Season/Movie sub-division. Every poster comes before any
        // backdrop, each in key order: series set, seasons, then movies.
        Assert.Equal(
        [
            "ja: /tv-ja.jpg Poster, /s1-ja.jpg Poster, /m-ja.jpg Poster, /tv-back-ja.jpg Backdrop, /m-back-ja.jpg Backdrop",
            "en: /m-en.jpg Poster",
        ],
        Describe(dto));
    }

    [Fact]
    public async Task ASeriesPicturesUseTheMainLinesShowsAndIgnoreASpinOffsTvId()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 100, season: 1);
            AddAnime(seed, 2, tvId: 555, season: 1); // an extra with a show of its own
            AddSeries(seed, 1, [1], (2, "SpinOff", 0));
            seed.TmdbTvImageSets.Add(TvSet(100, Fresh, Poster("/main.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(100, 1, Fresh));
            seed.TmdbTvImageSets.Add(TvSet(555, Fresh, Poster("/spinoff.jpg", "ja")));
        });
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, new FakeTmdbClient());

        var dto = await service.GetSeriesPicturesAsync(1);

        Assert.Equal(["ja: /main.jpg Poster"], Describe(dto));
        Assert.Equal([$"{ImageBase}/main.jpg"], await service.GetSeriesOptionUrlsAsync(1));
    }

    [Fact]
    public async Task ASeriesHasMappingWhenAnyMemberHasATmdbId()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, withMapping: false);
            AddAnime(seed, 2, imdbIds: ["tt2560140"]);
            AddAnime(seed, 3, movieIds: [635302]);
            AddSeries(seed, 1, mainLine: [1, 2]);
            AddSeries(seed, 2, [1], (3, "SideStory", 0));
        });
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, new FakeTmdbClient());

        Assert.False((await service.GetSeriesPicturesAsync(1)).HasMapping); // no row, and an IMDb-only row
        Assert.True((await service.GetSeriesPicturesAsync(2)).HasMapping); // an extra's movie id counts: any member
        Assert.Empty((await service.GetSeriesPicturesAsync(1)).Languages);
    }

    [Fact]
    public async Task ASeriesWithNoMembersHasNothingAndFetchesNothing()
    {
        var options = CreateOptions();
        var client = new FakeTmdbClient();
        using var db = new AnimeTrackerDbContext(options);
        var service = CreateService(db, client);

        var dto = await service.GetSeriesPicturesAsync(999);

        Assert.False(dto.HasMapping);
        Assert.Empty(dto.Languages);
        Assert.Equal(0, dto.PendingCount);
        Assert.Equal(0, await service.RefreshSeriesAsync(999, 20, force: false));
        Assert.Empty(client.Calls);
    }

    // =====================================================================
    // The custom id mapping (design.md D19): every read sees the merged view
    // =====================================================================

    [Fact]
    public async Task ACustomOnlyMappingDecidesTheScopesTheMatchAndWhatIsFetched()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed => AddAnime(seed, 1, withMapping: false));
        var client = new FakeTmdbClient()
            .Serve(TmdbSetKey.Tv(280564), [Image("/tv.jpg", "ja")])
            .Serve(TmdbSetKey.Season(280564, 1), [Image("/season1.jpg", "en")]);
        using var db = new AnimeTrackerDbContext(options);
        var service = ArtworkService(db, client, custom: new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, tv: 280564, season: 1)));

        Assert.True((await service.GetAnimePicturesAsync(1))!.HasMapping); // so no "TMDB has no match"
        Assert.True(await service.IsAnimeFetchDueAsync(1));                   // nothing cached yet
        await service.RefreshAnimeAsync(1, force: false);

        Assert.Equal([TmdbSetKey.Tv(280564), TmdbSetKey.Season(280564, 1)], client.Calls);
        Assert.Equal(["Series/ja: /tv.jpg Poster", "Season 1/en: /season1.jpg Poster"], Describe((await service.GetAnimePicturesAsync(1))!));
        Assert.False(await service.IsAnimeFetchDueAsync(1));
    }

    [Fact]
    public async Task AnOverrideOfTheSeasonDrawsTheSeasonSetItNamesNotTheSourcesOne()
    {
        // Fate/Zero Season 2: the source says TMDB season 1, and TMDB has a season 2.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 11741, tvId: 45845, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(45845, Fresh, Poster("/tv.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(45845, 1, Fresh, Poster("/season1.jpg", "en")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(45845, 2, Fresh, Poster("/season2.jpg", "en")));
        });
        using var db = new AnimeTrackerDbContext(options);
        var overridden = new FakeCustomIdMappings(FakeCustomIdMappings.Override(11741, tv: 45845, season: 2));

        var asTheSourceHasIt = await CreateService(db, new FakeTmdbClient()).GetAnimePicturesAsync(11741);
        var corrected = await ArtworkService(db, new FakeTmdbClient(), custom: overridden).GetAnimePicturesAsync(11741);

        Assert.Equal(["Series/ja: /tv.jpg Poster", "Season 1/en: /season1.jpg Poster"], Describe(asTheSourceHasIt!));
        Assert.Equal(["Series/ja: /tv.jpg Poster", "Season 2/en: /season2.jpg Poster"], Describe(corrected!));
    }

    [Fact]
    public async Task AFillEntryStopsApplyingTheMomentTheSourceHasTheAnime()
    {
        // The source catching up: the entry's TV id (280564) gives way to the source's (999), with no edit.
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 999, season: 1);
            seed.TmdbTvImageSets.Add(TvSet(999, Fresh, Poster("/source.jpg", "ja")));
            seed.TmdbTvImageSets.Add(TvSet(280564, Fresh, Poster("/custom.jpg", "ja")));
        });
        using var db = new AnimeTrackerDbContext(options);
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(1, tv: 280564, season: 1));

        var dto = await ArtworkService(db, new FakeTmdbClient(), custom: custom).GetAnimePicturesAsync(1);

        Assert.Equal(["Series/ja: /source.jpg Poster"], Describe(dto!));
    }

    [Fact]
    public async Task ASeriesDrawsFromTheMovieOfACustomOnlyMember()
    {
        var options = CreateOptions();
        await SeedAsync(options, seed =>
        {
            AddAnime(seed, 1, tvId: 1429, season: 1);
            AddAnime(seed, 2, withMapping: false); // a film the source has no ids for
            AddSeries(seed, 1, mainLine: [1], (2, "SideStory", 0));
            seed.TmdbTvImageSets.Add(TvSet(1429, Fresh, Poster("/tv.jpg", "ja")));
            seed.TmdbSeasonImageSets.Add(SeasonSet(1429, 1, Fresh));
            seed.TmdbMovieImageSets.Add(MovieSet(635302, Fresh, Poster("/movie.jpg", "ja")));
        });
        using var db = new AnimeTrackerDbContext(options);
        var custom = new FakeCustomIdMappings(FakeCustomIdMappings.Fill(2, movies: [635302]));

        var dto = await ArtworkService(db, new FakeTmdbClient(), custom: custom).GetSeriesPicturesAsync(1);

        Assert.True(dto.HasMapping);
        Assert.Equal(["ja: /tv.jpg Poster, /movie.jpg Poster"], Describe(dto));
        Assert.Equal(0, dto.PendingCount);
    }

    // --- Doubles ---

    // Fails the first save that carries a TMDB image change, the way a database
    // error mid-write would.
    private sealed class FailingSaveContext(DbContextOptions<AnimeTrackerDbContext> options) : AnimeTrackerDbContext(options)
    {
        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
        {
            if (ChangeTracker.Entries().Any(e => (e.Entity is TmdbImageBase or TmdbTvImageSet) && (e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)))
                throw new DbUpdateException("simulated write failure");

            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
    }
}
