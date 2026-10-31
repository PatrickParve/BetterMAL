using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme task 5.2: GuessTheThemePool.Query is the one definition
// of "can be drawn" behind the setup screen's count and every draw, and UsableSongs the one
// definition of a usable song. Each clause of the spec is pinned alone, against rows that
// sit either side of it.
public class GuessTheThemePoolTests
{
    private static async Task<List<int>> IdsAsync(
        AnimeTrackerDbContext db, GuessTheThemeSettings settings, int[]? played = null) =>
        await GuessTheThemePool.Query(db, settings, Now, played).OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();

    private static async Task<List<int>> SongIdsAsync(AnimeTrackerDbContext db, GuessTheThemeSettings settings) =>
        await GuessTheThemePool.UsableSongs(db, settings).OrderBy(t => t.SongId).Select(t => t.SongId!.Value).ToListAsync();

    // ---- the pool is the other games' pool, with a usable song ----

    [Fact]
    public async Task AnAnimeWithNoThemesIsNotInThePool()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task ThemesOfAnAnimeNotStoredCountForNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task AnAnimeTheOtherGamesCouldNotShowIsNotInThePoolWhateverItsSongs()
    {
        // No picture, no score and not yet aired: each fails the shared pool alone.
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1), Anime(2, picture: null), Anime(3, score: null), Anime(4, airingStatus: "not_yet_aired"));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20), Song(3, 30), Song(4, 40));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task TheSharedSettingsNarrowThePoolAsTheyDoElsewhere()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, airedFrom: new DateOnly(1995, 4, 1)),
            Anime(2, airedFrom: new DateOnly(2015, 4, 1)),
            Anime(3, airedFrom: new DateOnly(2015, 4, 1), mediaType: "movie"),
            Anime(4, airedFrom: new DateOnly(2015, 4, 1), rating: "rx"));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20), Song(3, 30), Song(4, 40));
        db.UserAnimeEntries.Add(OnList(2));
        await db.SaveChangesAsync();

        Assert.Equal([2, 3, 4], await IdsAsync(db, AnyTheme with { YearFrom = 2010 }));
        Assert.Equal([1, 2, 4], await IdsAsync(db, AnyTheme with { MediaTypes = ["tv"] }));
        Assert.Equal([2], await IdsAsync(db, AnyTheme with { List = GameListScope.OnlyMyList }));
        Assert.Equal([1, 3, 4], await IdsAsync(db, AnyTheme with { List = GameListScope.NotOnMyList }));
        Assert.Equal([1, 2, 3], await IdsAsync(db, AnyTheme with { HideHentai = true }));
    }

    [Fact]
    public async Task AnAnimeWithSeveralUsableSongsIsCountedOnce()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, AnimeThemeKind.Ending), Song(1, 12, sequence: 2));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    // ---- each clause of a usable song, alone ----

    [Fact]
    public async Task ATrackWithNoSongIsNotUsable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, songId: null));
        await db.SaveChangesAsync();

        Assert.Equal([10], await SongIdsAsync(db, AnyTheme));
        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task ATrackWithNoVideoIsNotUsable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, video: false));
        await db.SaveChangesAsync();

        Assert.Equal([10], await SongIdsAsync(db, AnyTheme));
        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task ATrackWithNoAudioIsNotUsable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, audio: false));
        await db.SaveChangesAsync();

        Assert.Equal([10], await SongIdsAsync(db, AnyTheme));
        Assert.Equal([1], await IdsAsync(db, AnyTheme));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ASpoilerIsNeverUsableWhateverHideNsfwSays(bool hideNsfw)
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, spoiler: true));
        await db.SaveChangesAsync();

        Assert.Equal([10], await SongIdsAsync(db, AnyTheme with { HideHentai = hideNsfw }));
        Assert.Equal([1], await IdsAsync(db, AnyTheme with { HideHentai = hideNsfw }));
    }

    [Fact]
    public async Task ASpoilerThatIsAlsoNsfwIsNeverUsableEither()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10, spoiler: true, nsfw: true));
        await db.SaveChangesAsync();

        Assert.Empty(await SongIdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task NsfwIsNotUsableWithHideNsfwOn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, nsfw: true));
        await db.SaveChangesAsync();

        Assert.Equal([10], await SongIdsAsync(db, AnyTheme with { HideHentai = true }));
        Assert.Equal([1], await IdsAsync(db, AnyTheme with { HideHentai = true }));
    }

    [Fact]
    public async Task NsfwIsUsableWithHideNsfwOffForAnAnimeNotRatedRx()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, rating: "r"));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, nsfw: true));
        await db.SaveChangesAsync();

        Assert.Equal([10, 20], await SongIdsAsync(db, AnyTheme));
        Assert.Equal([1, 2], await IdsAsync(db, AnyTheme));
    }

    [Fact]
    public async Task OnlyTheNsfwSongOfAnAnimeIsLeftOutNotTheAnime()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10, nsfw: true), Song(1, 11, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        var hide = AnyTheme with { HideHentai = true };

        Assert.Equal([11], await SongIdsAsync(db, hide));
        Assert.Equal([1], await IdsAsync(db, hide));
    }

    // ---- the Theme setting ----

    [Fact]
    public async Task AllAllowsOpeningsAndEndings()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        Assert.Equal([10, 20], await SongIdsAsync(db, AnyTheme with { Themes = GameThemeChoice.All }));
        Assert.Equal([1, 2], await IdsAsync(db, AnyTheme with { Themes = GameThemeChoice.All }));
    }

    [Fact]
    public async Task OpeningsAllowsOnlyOpenings()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        var openings = AnyTheme with { Themes = GameThemeChoice.Openings };

        Assert.Equal([10], await SongIdsAsync(db, openings));
        Assert.Equal([1], await IdsAsync(db, openings));
    }

    [Fact]
    public async Task EndingsAllowsOnlyEndings()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        var endings = AnyTheme with { Themes = GameThemeChoice.Endings };

        Assert.Equal([20], await SongIdsAsync(db, endings));
        Assert.Equal([2], await IdsAsync(db, endings));
    }

    [Fact]
    public async Task AnAnimeWithAnEndingAndNoOpeningIsOutWhenOpeningsAreChosen()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        Assert.Empty(await IdsAsync(db, AnyTheme with { Themes = GameThemeChoice.Openings }));
    }

    [Fact]
    public async Task TheCountFollowsTheThemeSetting()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Anime(3));
        db.AnimeThemes.AddRange(
            Song(1, 10), Song(2, 20, AnimeThemeKind.Ending), Song(3, 30), Song(3, 31, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        Assert.Equal(3, await GuessTheThemePool.Query(db, AnyTheme with { Themes = GameThemeChoice.All }, Now).CountAsync());
        Assert.Equal(2, await GuessTheThemePool.Query(db, AnyTheme with { Themes = GameThemeChoice.Endings }, Now).CountAsync());
        Assert.Equal(2, await GuessTheThemePool.Query(db, AnyTheme with { Themes = GameThemeChoice.Openings }, Now).CountAsync());
    }

    // ---- songs already played ----

    [Fact]
    public async Task APlayedSongTakesAnAnimeOutOnceItHasNoneLeft()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20), Song(2, 21, sequence: 2));
        await db.SaveChangesAsync();

        Assert.Equal([2], await IdsAsync(db, AnyTheme, played: [10]));
        // Anime 2 still has 21 after 20.
        Assert.Equal([1, 2], await IdsAsync(db, AnyTheme, played: [20]));
    }

    [Fact]
    public async Task ASongSharedByTwoAnimeIsGoneForBothOncePlayed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 500), Song(2, 500), Song(1, 501, sequence: 2));
        await db.SaveChangesAsync();

        // Anime 2's only usable song is 500, which has been played through anime 1.
        Assert.Equal([1], await IdsAsync(db, AnyTheme, played: [500]));
    }

    [Fact]
    public async Task NoPlayedSongsLeavesThePoolAsItIs()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();

        Assert.Equal([1, 2], await IdsAsync(db, AnyTheme, played: []));
        Assert.Equal([1, 2], await IdsAsync(db, AnyTheme, played: null));
    }

    // ---- it translates on Npgsql ----

    private static AnimeTrackerDbContext NpgsqlDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseNpgsql("Host=localhost;Database=none").Options);

    [Fact]
    public void ThePoolTranslatesToOneStatementOnNpgsql()
    {
        using var db = NpgsqlDb();
        var settings = AnyTheme with { HideHentai = true, Themes = GameThemeChoice.Openings, YearFrom = 2000 };

        var sql = GuessTheThemePool.Query(db, settings, Now, [10, 20]).ToCandidates().ToQueryString();

        // The shared pool's own rules are still there, with the song rule as a subquery.
        Assert.Contains("\"MalScore\"", sql);
        Assert.Contains("EXISTS", sql);
        Assert.Contains("\"AnimeThemes\"", sql);
        Assert.Contains("\"SongId\" IS NOT NULL", sql);
        Assert.Contains("\"VideoUrl\" IS NOT NULL", sql);
        Assert.Contains("\"AudioUrl\" IS NOT NULL", sql);
        Assert.Contains("NOT (", sql);
        Assert.Contains("\"Spoiler\"", sql);
        Assert.Contains("\"Nsfw\"", sql);
        Assert.Contains("\"Kind\"", sql);
        Assert.Contains("ANY", sql);
    }

    [Fact]
    public void WithoutHideNsfwOrATypeTheStatementDoesNotMentionThem()
    {
        using var db = NpgsqlDb();

        var sql = GuessTheThemePool.Query(db, AnyTheme, Now).ToQueryString();

        Assert.Contains("\"Spoiler\"", sql);
        Assert.DoesNotContain("\"Nsfw\"", sql);
        Assert.DoesNotContain("t.\"Kind\"", sql);
    }

    [Fact]
    public void TheUsableSongRuleTranslatesOnItsOwn()
    {
        using var db = NpgsqlDb();

        var sql = GuessTheThemePool.UsableSongs(db, AnyTheme with { Themes = GameThemeChoice.Endings }).ToQueryString();

        Assert.Contains("\"Kind\" = 'Ending'", sql);
    }
}
