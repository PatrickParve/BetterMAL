using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Search;
using AnimeTracker.Api.Tests.Services.AnimeThemes;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Search;

// theme-search spec (improve-search-page tasks 3.3 to 3.6): ThemeSearchService over the real
// ThemeSearchIndex and an in-memory catalogue. An anime the app has saved is given to the
// service through the anime index, as in the app; one it hasn't is named by the row itself.
public class ThemeSearchServiceTests
{
    private static AnimeTrackerDbContext CreateDb() => ThemeSearchTestKit.CreateDb();

    private static Task SeedAsync(AnimeTrackerDbContext db, params AnimeTheme[] rows) =>
        ThemeSearchTestKit.SeedAsync(db, rows);

    private static ThemeSearchService CreateService(
        AnimeTrackerDbContext db, IAnimeSearchIndex? animeIndex = null) =>
        new(new ThemeSearchIndex(new ThemeSearchTestKit.DbScopeFactory(db)), animeIndex ?? new FakeAnimeSearchIndex(), db);

    private static AnimeTheme Row(
        int animeId, int themeId, string? songTitle = "A song", string[]? artists = null, string? animeName = null,
        AnimeThemeKind kind = AnimeThemeKind.Opening, int sequence = 1, bool nsfw = false, bool spoiler = false) =>
        StoredThemes.Row(animeId, themeId, kind, sequence, songTitle: songTitle, artists: artists, nsfw: nsfw, spoiler: spoiler, animeName: animeName);

    private static AnimeTitleProjection Saved(int id, string title, string? english = null, string? picture = null, int? rank = null) =>
        new(id, title, english, picture, rank);

    private static List<int> ThemeIds(ThemeSearchResponseDto response) => response.Results.Select(r => r.Theme.Id).ToList();

    // --- What a query matches ---

    [Fact]
    public async Task ASongTitleMatches()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Blue Bird", animeName: "Naruto Shippuden"),
            Row(2, 20, songTitle: "Something Else", animeName: "Bleach"));

        var response = await CreateService(db).SearchAsync("blue bird", 5);

        Assert.True(response.Ready);
        Assert.Equal([10], ThemeIds(response));
        Assert.Equal("Blue Bird", response.Results[0].Theme.SongTitle);
    }

    [Fact]
    public async Task AnArtistMatches()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Blue Bird", artists: ["Ikimonogakari"], animeName: "Naruto Shippuden"),
            Row(1, 11, songTitle: "Sakura", artists: ["Ikimonogakari", "Someone"], animeName: "Naruto Shippuden"),
            Row(2, 20, songTitle: "Gurenge", artists: ["LiSA"], animeName: "Demon Slayer"));

        var response = await CreateService(db).SearchAsync("ikimonogakari", 5);

        Assert.Equal([10, 11], ThemeIds(response).Order());
    }

    [Fact]
    public async Task AnAnimesTitleOrEnglishTitleMatches_SavedOrNot()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(100, 1, songTitle: "A"), // saved: title contains it
            Row(101, 2, songTitle: "B"), // saved: only the English title contains it
            Row(102, 3, songTitle: "C"), // saved: neither
            Row(103, 4, songTitle: "D", animeName: "Naruto Spin-Off: Rock Lee"), // not saved: AnimeThemes' name
            Row(104, 5, songTitle: "E", animeName: "Boruto"));                   // not saved: no
        var animeIndex = new FakeAnimeSearchIndex(
        [
            Saved(100, "Naruto: Shippuuden"),
            Saved(101, "Kimetsu no Yaiba", english: "Not Naruto But Close"),
            Saved(102, "Bleach"),
        ]);

        var response = await CreateService(db, animeIndex).SearchAsync("naruto", 20);

        Assert.Equal([100, 101, 103], response.Results.Select(r => r.AnimeId).Order());
    }

    [Theory]
    [InlineData("blue bird")]
    [InlineData("bluebird")]
    [InlineData("Blue-Bird")]
    [InlineData("BLUE  BIRD!")]
    [InlineData("blue_bird")]
    public async Task SpacingPunctuationAndCaseDoNotMatter(string query)
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "Blue Bird", animeName: "Show"), Row(2, 20, songTitle: "Red Bird", animeName: "Show 2"));

        var response = await CreateService(db).SearchAsync(query, 5);

        Assert.Equal([10], ThemeIds(response));
    }

    [Theory]
    [InlineData("clementine", "Clémentine")]
    [InlineData("Clémentine", "Clementine")]
    [InlineData("okami", "Ōkami")]
    public async Task AccentsDoNotMatter(string query, string title)
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: title, animeName: "Show"));

        var response = await CreateService(db).SearchAsync(query, 5);

        Assert.Equal([10], ThemeIds(response));
    }

    [Fact]
    public async Task AQuotedQueryMatchesOnlyAnEqualText()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Blue Bird", animeName: "One"),
            Row(2, 20, songTitle: "Blue Bird Days", animeName: "Two"),
            Row(3, 30, songTitle: "Other", artists: ["Blue Bird"], animeName: "Three"),
            Row(4, 40, songTitle: "Other", animeName: "Blue-Bird"),
            Row(5, 50, songTitle: "Beyond Blue Bird", animeName: "Five"));

        var response = await CreateService(db).SearchAsync("\"blue bird\"", 20);

        // Song title, one of the artists, or the anime's title: equal under the normalization rule.
        Assert.Equal([10, 30, 40], ThemeIds(response).Order());
    }

    [Fact]
    public async Task WordsInDifferentFieldsDoNotCombine()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "Gurenge", artists: ["LiSA"], animeName: "Kimetsu no Yaiba"));
        var service = CreateService(db);

        Assert.Empty((await service.SearchAsync("gurenge lisa", 5)).Results);
        Assert.Single((await service.SearchAsync("gurenge", 5)).Results);
        Assert.Single((await service.SearchAsync("lisa", 5)).Results);
        Assert.Single((await service.SearchAsync("kimetsu", 5)).Results);
        Assert.Empty((await service.SearchAsync("lisa kimetsu", 5)).Results);
    }

    [Fact]
    public async Task TheLabelIsNotMatched()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Song A", animeName: "Show A", kind: AnimeThemeKind.Ending, sequence: 2),
            Row(2, 20, songTitle: "Song B", animeName: "Show B", kind: AnimeThemeKind.Opening, sequence: 1));

        Assert.Empty((await CreateService(db).SearchAsync("ed2", 5)).Results);
        Assert.Empty((await CreateService(db).SearchAsync("op1", 5)).Results);
    }

    [Theory]
    [InlineData("!!!")]
    [InlineData("   ")]
    [InlineData("")]
    [InlineData("\"")]
    [InlineData("\"\"")]
    [InlineData("?!")]
    public async Task AQueryThatNormalizesToNothingMatchesNothing(string query)
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "!!!", artists: ["???"], animeName: "!!!"), Row(2, 20, songTitle: "Real", animeName: "Show"));

        var response = await CreateService(db).SearchAsync(query, 5);

        Assert.True(response.Ready); // the catalogue is loaded; there is simply nothing to find
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task FlaggedSongsAreListedWithTheirFlags()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Lewd Song", animeName: "One", nsfw: true),
            Row(2, 20, songTitle: "Lewd Spoiler", animeName: "Two", spoiler: true),
            Row(3, 30, songTitle: "Lewd Plain", animeName: "Three"));

        var response = await CreateService(db).SearchAsync("lewd", 5);

        // The search has no Hide NSFW setting to apply: every song of the catalogue is findable.
        Assert.Equal(3, response.Results.Count);
        Assert.True(response.Results.Single(r => r.Theme.Id == 10).Theme.Nsfw);
        Assert.True(response.Results.Single(r => r.Theme.Id == 20).Theme.Spoiler);
    }

    [Fact]
    public async Task BeforeTheCatalogueHasLoadedItSaysSoInsteadOfFindingNothing()
    {
        using var db = CreateDb();
        await ThemeSearchTestKit.SeedAsync(db, [Row(1, 10, songTitle: "Blue Bird", animeName: "Show")], ready: false);

        var response = await CreateService(db).SearchAsync("blue bird", 5);

        Assert.False(response.Ready);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task AnEmptyCatalogueThatHasLoadedIsReadyWithNoResults()
    {
        using var db = CreateDb();
        StoredThemes.MarkSynced(db);
        await db.SaveChangesAsync();

        var response = await CreateService(db).SearchAsync("anything", 5);

        Assert.True(response.Ready);
        Assert.Empty(response.Results);
    }

    [Fact]
    public async Task ASongIsTheSameThemeTheAnimePageShows()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Blue Bird", artists: ["Ikimonogakari", "Other"], animeName: "Show", nsfw: true, spoiler: true));
        var found = (await CreateService(db).SearchAsync("blue bird", 5)).Results.Single().Theme;

        var onThePage = (await new AnimeThemesReads(db).GetForAnimeAsync(1)).Themes.Single();

        Assert.Equal(JsonSerializer.Serialize(onThePage), JsonSerializer.Serialize(found));
    }

    // --- How matches are ranked ---

    [Fact]
    public async Task AnExactTitleComesBeforeOnesThatOnlyStartWithOrContainIt()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Beyond Blue Bird", animeName: "A"),
            Row(2, 20, songTitle: "Blue Bird Days", animeName: "B"),
            Row(3, 30, songTitle: "Blue Bird", animeName: "C"));

        var response = await CreateService(db).SearchAsync("blue bird", 5);

        Assert.Equal(["Blue Bird", "Blue Bird Days", "Beyond Blue Bird"], response.Results.Select(r => r.Theme.SongTitle));
    }

    [Fact]
    public async Task StartingWithTheQueryBeatsContainingIt()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "My Blue Moon", animeName: "A"),
            Row(2, 20, songTitle: "Blue Moon Rising", animeName: "B"));

        var response = await CreateService(db).SearchAsync("blue moon", 5);

        Assert.Equal([20, 10], ThemeIds(response));
    }

    [Fact]
    public async Task ASongTitleMatchBeatsAnAnimeTitleMatchEvenForAMorePopularAnime()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Other Song"), // its anime is titled exactly "Gurenge", and is the more popular
            Row(2, 20, songTitle: "Gurenge"));   // its song is titled exactly "Gurenge"
        var animeIndex = new FakeAnimeSearchIndex([Saved(1, "Gurenge", rank: 1), Saved(2, "Demon Slayer", rank: 900)]);

        var response = await CreateService(db, animeIndex).SearchAsync("gurenge", 5);

        Assert.Equal([20, 10], ThemeIds(response));
    }

    [Fact]
    public async Task ASongTitleMatchComesBeforeAnArtistMatchWhichComesBeforeAnAnimeTitleMatch()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Other", animeName: "Lisa"),
            Row(2, 20, songTitle: "Other", artists: ["LiSA"], animeName: "Show"),
            Row(3, 30, songTitle: "Lisa", animeName: "Show 3"));

        var response = await CreateService(db).SearchAsync("lisa", 5);

        Assert.Equal([30, 20, 10], ThemeIds(response));
    }

    [Fact]
    public async Task ASongIsRankedByItsBestFieldNotItsFirst()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 10, songTitle: "Lisa Goes On", animeName: "A"),                     // prefix only
            Row(2, 20, songTitle: "Lisa Song", artists: ["LiSA"], animeName: "B"));    // prefix on the song, exact on the artist

        var response = await CreateService(db).SearchAsync("lisa", 5);

        Assert.Equal([20, 10], ThemeIds(response));
    }

    [Fact]
    public async Task PopularAnimeComeFirst_AndUnrankedOnesLast()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(10, 1, songTitle: "S"), Row(11, 2, songTitle: "S"), Row(12, 3, songTitle: "S"), Row(13, 4, songTitle: "S"));
        var animeIndex = new FakeAnimeSearchIndex(
        [
            Saved(10, "Fate Five", rank: 5),
            Saved(11, "Fate One", rank: 1),
            Saved(12, "Fate None", rank: null),
            Saved(13, "Fate Zero Rank", rank: 0),
        ]);

        var response = await CreateService(db, animeIndex).SearchAsync("fate", 10);

        Assert.Equal([11, 10], response.Results.Take(2).Select(r => r.AnimeId));
        Assert.Equal([12, 13], response.Results.Skip(2).Select(r => r.AnimeId).Order()); // unranked: 0 counts as none
    }

    [Fact]
    public async Task OneAnimesSongsFollowTheOrderOfItsOwnBox()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 30, songTitle: "Hello C", animeName: "Show", kind: AnimeThemeKind.Ending, sequence: 1),
            Row(1, 31, songTitle: "Hello B", animeName: "Show", kind: AnimeThemeKind.Opening, sequence: 2),
            Row(1, 32, songTitle: "Hello A", animeName: "Show", kind: AnimeThemeKind.Opening, sequence: 1));

        var response = await CreateService(db).SearchAsync("hello", 5);

        Assert.Equal(["OP1", "OP2", "ED1"], response.Results.Select(r => r.Theme.Label));
    }

    [Fact]
    public async Task AnimeThemesOwnBoxOrderAndTheSearchOrderAgreeForOneAnime()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(1, 3, songTitle: "Hello", animeName: "Show", kind: AnimeThemeKind.Ending, sequence: 2),
            Row(1, 1, songTitle: "Hello", animeName: "Show", kind: AnimeThemeKind.Opening, sequence: 1),
            Row(1, 4, songTitle: "Hello", animeName: "Show", kind: AnimeThemeKind.Ending, sequence: 1),
            Row(1, 2, songTitle: "Hello", animeName: "Show", kind: AnimeThemeKind.Opening, sequence: 2));

        var searched = (await CreateService(db).SearchAsync("hello", 10)).Results.Select(r => r.Theme.Id);
        var box = (await new AnimeThemesReads(db).GetForAnimeAsync(1)).Themes.Select(t => t.Id);

        Assert.Equal(box, searched);
    }

    [Fact]
    public async Task AnUnsavedAnimesSongsComeAfterASavedAnimesWhenTheyMatchEquallyWell()
    {
        using var db = CreateDb();
        // Everything ties but the last keys: both anime start with the query, neither has a popularity
        // rank. By title alone the unsaved one ("Alpha") would come first, so only the saved-before-
        // unsaved rule can put the saved one ("Zulu") ahead.
        await SeedAsync(db,
            Row(1, 10, songTitle: "Song", animeName: "Starlight Alpha"),
            Row(2, 20, songTitle: "Song")); // saved, and with no popularity rank either
        var animeIndex = new FakeAnimeSearchIndex([Saved(2, "Starlight Zulu", rank: null)]);

        var response = await CreateService(db, animeIndex).SearchAsync("starlight", 5);

        Assert.Equal([20, 10], ThemeIds(response));
    }

    [Fact]
    public async Task TheSameSearchGivesTheSameOrder_WhateverOrderTheRowsWereStoredIn()
    {
        // Songs that tie on every ranking key but the last ones, over anime that tie on popularity.
        IEnumerable<AnimeTheme> Rows() =>
            Enumerable.Range(1, 6).SelectMany(anime => Enumerable.Range(1, 4).Select(n =>
                Row(anime, anime * 10 + n, songTitle: $"Echo {n}", animeName: $"Show {anime}",
                    kind: n % 2 == 0 ? AnimeThemeKind.Ending : AnimeThemeKind.Opening, sequence: (n + 1) / 2)));

        List<(int, int)> Order(ThemeSearchResponseDto r) => r.Results.Select(x => (x.AnimeId, x.Theme.Id)).ToList();

        using var forward = CreateDb();
        await SeedAsync(forward, Rows().ToArray());
        using var reversed = CreateDb();
        await SeedAsync(reversed, Rows().Reverse().ToArray());

        var first = Order(await CreateService(forward).SearchAsync("echo", 50));
        var again = Order(await CreateService(forward).SearchAsync("echo", 50));
        var other = Order(await CreateService(reversed).SearchAsync("echo", 50));

        Assert.Equal(24, first.Count);
        Assert.Equal(first, again);
        Assert.Equal(first, other);
    }

    // --- How many ---

    [Fact]
    public async Task TheDropdownTakesTheFirstFiveOfTheSameOrderThePageTakesTwoHundredOf()
    {
        using var db = CreateDb();
        await SeedAsync(db, Enumerable.Range(1, 300)
            .Select(i => Row(i, i, songTitle: $"Love Song {i:000}", animeName: $"Anime {i:000}")).ToArray());
        var service = CreateService(db);

        var five = await service.SearchAsync("love", 5);
        var page = await service.SearchAsync("love", 200);
        var everything = await service.SearchAsync("love", 1000);

        Assert.Equal(5, five.Results.Count);
        Assert.Equal(200, page.Results.Count);
        Assert.Equal(300, everything.Results.Count);
        Assert.Equal(page.Results.Take(5).Select(r => r.Theme.Id), five.Results.Select(r => r.Theme.Id));
        Assert.Equal(everything.Results.Take(200).Select(r => r.Theme.Id), page.Results.Select(r => r.Theme.Id));
        Assert.True(five.Ready && page.Ready);
    }

    [Fact]
    public async Task ALimitBelowOneReturnsNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "Blue Bird", animeName: "Show"));

        Assert.Empty((await CreateService(db).SearchAsync("blue", 0)).Results);
    }

    // --- Every song is named, even when the app hasn't saved its anime ---

    [Fact]
    public async Task ASavedAnimesSongIsShownUnderItsStoredTitlesAndPicture()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "Gurenge", animeName: "Kimetsu no Yaiba (AnimeThemes)"));
        var animeIndex = new FakeAnimeSearchIndex([Saved(1, "Kimetsu no Yaiba", english: "Demon Slayer", picture: "https://mal/1.jpg", rank: 3)]);

        var result = Assert.Single((await CreateService(db, animeIndex).SearchAsync("gurenge", 5)).Results);

        Assert.Equal(1, result.AnimeId);
        Assert.Equal("Kimetsu no Yaiba", result.Title);
        Assert.Equal("Demon Slayer", result.EnglishTitle);
        Assert.Equal("https://mal/1.jpg", result.PictureUrl);
    }

    [Fact]
    public async Task ASavedAnimeIsMatchedByItsOwnTitles_NotAnimeThemesName()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(1, 10, songTitle: "Gurenge", animeName: "Totally Different Name"));
        var animeIndex = new FakeAnimeSearchIndex([Saved(1, "Kimetsu no Yaiba", english: "Demon Slayer")]);
        var service = CreateService(db, animeIndex);

        Assert.Single((await service.SearchAsync("demon slayer", 5)).Results);
        Assert.Single((await service.SearchAsync("kimetsu", 5)).Results);
        Assert.Empty((await service.SearchAsync("totally different", 5)).Results);
    }

    [Fact]
    public async Task AChosenPictureIsTheOneShown()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Kimetsu no Yaiba", MalPictureUrl = "mal.jpg", SelectedPictureUrl = "chosen.jpg" };
        anime.ResolvePictureUrl();
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();
        await SeedAsync(db, Row(1, 10, songTitle: "Gurenge", animeName: "Kimetsu"));

        // The real anime index over the same data: the picture the app displays is what its own projection reads.
        var animeIndex = new AnimeSearchIndexCache(new AnimeIndexScopeFactory(db));
        var result = Assert.Single((await CreateService(db, animeIndex).SearchAsync("gurenge", 5)).Results);

        Assert.Equal("chosen.jpg", result.PictureUrl);
    }

    [Fact]
    public async Task AnUnsavedAnimesSongIsShownUnderAnimeThemesNameWithNoPicture()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(5, 50, songTitle: "Gurenge", animeName: "Kimetsu no Yaiba"));

        var result = Assert.Single((await CreateService(db).SearchAsync("gurenge", 5)).Results);

        Assert.Equal(5, result.AnimeId);
        Assert.Equal("Kimetsu no Yaiba", result.Title);
        Assert.Null(result.EnglishTitle);
        Assert.Null(result.PictureUrl);
    }

    [Fact]
    public async Task AnUnsavedAnimeWithNoNameIsLeftOut_EvenWhenItsSongMatches()
    {
        using var db = CreateDb();
        await SeedAsync(db,
            Row(5, 50, songTitle: "Gurenge", animeName: null),
            Row(7, 70, songTitle: "Gurenge", animeName: "Named"));

        var response = await CreateService(db).SearchAsync("gurenge", 5);

        Assert.Equal([70], ThemeIds(response));
    }

    [Fact]
    public async Task OpeningItSavesIt_SoTheNextSearchShowsItsTitleAndPictureWithoutRebuildingTheCatalogue()
    {
        using var db = CreateDb();
        await SeedAsync(db, Row(5, 50, songTitle: "Gurenge", animeName: "Kimetsu (AnimeThemes)"));
        var animeIndex = new FakeAnimeSearchIndex();
        var service = CreateService(db, animeIndex);

        var before = Assert.Single((await service.SearchAsync("gurenge", 5)).Results);
        Assert.Equal("Kimetsu (AnimeThemes)", before.Title);
        Assert.Null(before.PictureUrl);

        animeIndex.Rows = [Saved(5, "Kimetsu no Yaiba", english: "Demon Slayer", picture: "https://mal/5.jpg", rank: 3)];
        var after = Assert.Single((await service.SearchAsync("gurenge", 5)).Results);

        Assert.Equal("Kimetsu no Yaiba", after.Title);
        Assert.Equal("Demon Slayer", after.EnglishTitle);
        Assert.Equal("https://mal/5.jpg", after.PictureUrl);
    }

    [Fact]
    public async Task BeforeNamesAreStored_SongsOfSavedAnimeAreListedAndOthersAreNot()
    {
        using var db = CreateDb();
        // A catalogue stored by the version before names: no row has one.
        await SeedAsync(db,
            Row(1, 10, songTitle: "Gurenge", animeName: null),
            Row(2, 20, songTitle: "Gurenge", animeName: null));
        var animeIndex = new FakeAnimeSearchIndex([Saved(1, "Kimetsu no Yaiba")]);

        var response = await CreateService(db, animeIndex).SearchAsync("gurenge", 5);

        Assert.Equal([1], response.Results.Select(r => r.AnimeId));
    }

    // Backs the real AnimeSearchIndexCache with the same in-memory context.
    private sealed class AnimeIndexScopeFactory(AnimeTrackerDbContext db) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new Scope(db);

        private sealed class Scope(AnimeTrackerDbContext db) : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new Provider(db);
            public void Dispose() { }
        }

        private sealed class Provider(AnimeTrackerDbContext db) : IServiceProvider
        {
            public object? GetService(Type serviceType) =>
                serviceType == typeof(IAnimeMetadataRepository) ? new AnimeMetadataRepository(db) : null;
        }
    }
}
