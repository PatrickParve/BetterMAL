using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme task 5.4: the draw of design D8. An anime first, on
// Guess the Anime's curve, then a song at random from the ones that anime has left; a song
// plays once in a game; nothing is held back between games.
public class GuessTheThemeDrawTests
{
    private static GuessTheThemeDraw Make(AnimeTrackerDbContext db, int seed = 7) => new(db, new Random(seed));

    /// <summary>Plays a whole game's worth of draws the way the service will: each song is
    /// recorded as played the moment it is drawn.</summary>
    private static async Task<List<GuessTheThemeRound>> PlayAllAsync(
        GuessTheThemeDraw draw, GuessTheThemeSettings settings, int limit = 1000)
    {
        var rounds = new List<GuessTheThemeRound>();
        var played = new List<int>();
        while (rounds.Count < limit && await draw.DrawAsync(settings, played) is { } round)
        {
            rounds.Add(round);
            played.Add(round.SongId);
        }

        return rounds;
    }

    private static async Task<Dictionary<int, int>> CountAnimeAsync(
        GuessTheThemeDraw draw, GuessTheThemeSettings settings, int draws)
    {
        var counts = new Dictionary<int, int>();
        for (var i = 0; i < draws; i++)
        {
            var round = await draw.DrawAsync(settings, []);
            counts[round!.AnimeId] = counts.GetValueOrDefault(round.AnimeId) + 1;
        }

        return counts;
    }

    // ---- the round a draw makes ----

    [Fact]
    public async Task ADrawCopiesTheSongOffTheCatalogue()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        var theme = Song(1, 42, AnimeThemeKind.Ending, sequence: 2);
        theme.SongTitle = "Silhouette";
        theme.Artists = ["KANA-BOON"];
        db.AnimeThemes.Add(theme);
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, []);

        Assert.NotNull(round);
        Assert.Equal(1, round.AnimeId);
        Assert.Equal(theme.ThemeId, round.ThemeId);
        Assert.Equal(AnimeThemeKind.Ending, round.Kind);
        Assert.Equal("ED2", round.Label);
        Assert.Equal(42, round.SongId);
        Assert.Equal("Silhouette", round.SongTitle);
        Assert.Equal(["KANA-BOON"], round.Artists);
        Assert.Equal(theme.AudioUrl, round.AudioUrl);
        Assert.Equal(theme.VideoUrl, round.VideoUrl);
    }

    [Fact]
    public async Task ADrawAddsNothingToTheContext()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        await Make(db).DrawAsync(AnyTheme, []);

        Assert.Empty(db.ChangeTracker.Entries<GuessTheThemeRound>());
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task TheRoundIsALooseCopyNotTheCataloguesRow()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, []);
        round!.Artists.Add("Someone else");

        Assert.Equal(["Artist A", "Artist B"], db.AnimeThemes.Single().Artists);
    }

    // ---- when nothing can be drawn ----

    [Fact]
    public async Task AnEmptyPoolDrawsNothing()
    {
        using var db = CreateDb();

        Assert.Null(await Make(db).DrawAsync(AnyTheme, []));
    }

    [Fact]
    public async Task AnimeWithNoUsableSongDrawNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10, spoiler: true));
        await db.SaveChangesAsync();

        Assert.Null(await Make(db).DrawAsync(AnyTheme, []));
    }

    [Fact]
    public async Task WithEverySongPlayedThereIsNoneToReturn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2), Song(2, 20));
        await db.SaveChangesAsync();

        Assert.Null(await Make(db).DrawAsync(AnyTheme, [10, 11, 20]));
    }

    // ---- an anime comes back for another song ----

    [Fact]
    public async Task AnAnimeComesUpOnceForEachOfItsSongsAndNotAFourthTime()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(
            Song(1, 10), Song(1, 11, AnimeThemeKind.Ending), Song(1, 12, sequence: 2));
        await db.SaveChangesAsync();

        var rounds = await PlayAllAsync(Make(db), AnyTheme);

        Assert.Equal(3, rounds.Count);
        Assert.All(rounds, r => Assert.Equal(1, r.AnimeId));
        Assert.Equal([10, 11, 12], rounds.Select(r => r.SongId).Order());
    }

    // ---- a song plays once in a game ----

    [Fact]
    public async Task NoSongRepeatsAcrossAWholeGame()
    {
        using var db = CreateDb();
        for (var anime = 1; anime <= 12; anime++)
        {
            db.AnimeMetadata.Add(Anime(anime, rank: anime));
            for (var n = 1; n <= 1 + anime % 4; n++)
                db.AnimeThemes.Add(Song(anime, anime * 10 + n, n % 2 == 0 ? AnimeThemeKind.Ending : AnimeThemeKind.Opening, sequence: n));
        }

        await db.SaveChangesAsync();

        var rounds = await PlayAllAsync(Make(db), AnyTheme);

        var songs = rounds.Select(r => r.SongId).ToList();
        Assert.Equal(songs.Count, songs.Distinct().Count());
        // Every song comes up before the pool is spent.
        Assert.Equal(db.AnimeThemes.Select(t => t.SongId!.Value).Distinct().Order(), songs.Order());
    }

    [Fact]
    public async Task AGameOf50SongsHasNoRepeat()
    {
        using var db = CreateDb();
        for (var anime = 1; anime <= 20; anime++)
        {
            db.AnimeMetadata.Add(Anime(anime));
            for (var n = 1; n <= 4; n++)
                db.AnimeThemes.Add(Song(anime, anime * 10 + n, sequence: n));
        }

        await db.SaveChangesAsync();

        var rounds = await PlayAllAsync(Make(db), AnyTheme, limit: 50);

        Assert.Equal(50, rounds.Count);
        Assert.Equal(50, rounds.Select(r => r.SongId).Distinct().Count());
    }

    [Fact]
    public async Task ASongSharedWithAnotherAnimeDoesNotPlayTwiceThroughTheOther()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        // Anime 2's only song is the one anime 1 has too.
        db.AnimeThemes.AddRange(Song(1, 500), Song(1, 501, sequence: 2), Song(2, 500));
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 30; seed++)
        {
            var rounds = await PlayAllAsync(Make(db, seed), AnyTheme);

            Assert.Equal(2, rounds.Count);
            Assert.Equal([500, 501], rounds.Select(r => r.SongId).Order());
        }
    }

    [Fact]
    public async Task AnAnimeWithNothingLeftIsPassedOver()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2), Song(2, 20));
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 40; seed++)
        {
            var round = await Make(db, seed).DrawAsync(AnyTheme, [10, 11]);

            Assert.Equal(2, round!.AnimeId);
        }
    }

    [Fact]
    public async Task APlayedSongOfAnAnimeLeavesItsOthersToBeDrawn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, [10]);

        Assert.Equal(11, round!.SongId);
    }

    // ---- nothing is held back from an earlier game ----

    [Fact]
    public async Task ASongJustPlayedCanComeUpInTheNextGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();
        var draw = Make(db);

        var first = await PlayAllAsync(draw, AnyTheme);
        var next = await PlayAllAsync(draw, AnyTheme);

        Assert.Equal([10], first.Select(r => r.SongId));
        Assert.Equal([10], next.Select(r => r.SongId));
    }

    [Fact]
    public async Task EveryAnimeCanOpenAGameWhateverTheLastOneHeard()
    {
        // With one anime in the pool the only way it isn't drawn is if something held it back.
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        for (var game = 0; game < 5; game++)
            Assert.NotNull(await Make(db, game).DrawAsync(AnyTheme, []));
    }

    // ---- the song is a random one of the anime's usable songs ----

    [Fact]
    public async Task EachUsableSongOfAnAnimeHasTheSameChance()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2), Song(1, 12, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();
        var draw = Make(db);

        var counts = new Dictionary<int, int>();
        for (var i = 0; i < 1500; i++)
        {
            var round = await draw.DrawAsync(AnyTheme, []);
            counts[round!.SongId] = counts.GetValueOrDefault(round.SongId) + 1;
        }

        Assert.Equal(3, counts.Count);
        Assert.All(counts.Values, n => Assert.InRange(n, 400, 600));
    }

    [Fact]
    public async Task OnlyASongTheSettingsAllowIsDrawn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2), Song(1, 12, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 30; seed++)
        {
            var round = await Make(db, seed).DrawAsync(AnyTheme with { Themes = GameThemeChoice.Endings }, []);

            Assert.Equal(12, round!.SongId);
            Assert.Equal(AnimeThemeKind.Ending, round.Kind);
        }
    }

    [Fact]
    public async Task ASpoilerAndAnNsfwSongUnderHideNsfwAreNeverDrawn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(
            Song(1, 10, spoiler: true), Song(1, 11, sequence: 2, nsfw: true), Song(1, 12, sequence: 3));
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 30; seed++)
        {
            var round = await Make(db, seed).DrawAsync(AnyTheme with { HideHentai = true }, []);

            Assert.Equal(12, round!.SongId);
        }
    }

    [Fact]
    public async Task ASongTwoOfAnAnimesThemesShareCountsOnceAmongItsSongs()
    {
        // OP1 and ED1 are the same song, and OP2 another: two songs, not three themes.
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(
            Song(1, 10, themeId: 1), Song(1, 10, AnimeThemeKind.Ending, themeId: 2), Song(1, 11, sequence: 2, themeId: 3));
        await db.SaveChangesAsync();
        var draw = Make(db);

        var counts = new Dictionary<int, int>();
        for (var i = 0; i < 1200; i++)
        {
            var round = await draw.DrawAsync(AnyTheme, []);
            counts[round!.SongId] = counts.GetValueOrDefault(round.SongId) + 1;
        }

        Assert.InRange(counts[10], 500, 700);
        Assert.InRange(counts[11], 500, 700);
    }

    // ---- an anime is drawn as Guess the Anime draws its answer ----

    [Fact]
    public async Task ASeededDrawIsRepeatable()
    {
        using var db = CreateDb();
        for (var anime = 1; anime <= 8; anime++)
        {
            db.AnimeMetadata.Add(Anime(anime));
            db.AnimeThemes.Add(Song(anime, anime * 10));
        }

        await db.SaveChangesAsync();

        var one = (await PlayAllAsync(Make(db, 99), AnyTheme)).Select(r => r.AnimeId);
        var two = (await PlayAllAsync(Make(db, 99), AnyTheme)).Select(r => r.AnimeId);

        Assert.Equal(one, two);
    }

    [Fact]
    public async Task APopularAnimeComesUpFarMoreThanAnObscureOne()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 500), Anime(2, rank: 3000));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();

        var counts = await CountAnimeAsync(Make(db), AnyTheme, 1500);

        // 1,000 / 3,000 to the fourth is about 1.2%.
        Assert.True(counts[1] > 40 * counts.GetValueOrDefault(2), $"{counts[1]} against {counts.GetValueOrDefault(2)}");
    }

    [Fact]
    public async Task AnAnimeOnMyListIsNotPenalisedForBeingObscure()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 20_000), Anime(2, rank: 500));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();

        var counts = await CountAnimeAsync(Make(db), AnyTheme, 1500);

        Assert.InRange(counts[1], 650, 850);
        Assert.InRange(counts[2], 650, 850);
    }

    [Fact]
    public async Task APoolWithNothingPopularStillDrawsAndTheMostPopularComeUpMost()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 16_000), Anime(2, rank: 40_000));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();

        var counts = await CountAnimeAsync(Make(db), AnyTheme, 1000);

        Assert.True(counts[1] > counts.GetValueOrDefault(2));
        Assert.Equal(1000, counts.Values.Sum());
    }

    [Fact]
    public async Task AShowWithManySongsIsNoLikelierOnOneDrawThanAShowWithFew()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        for (var n = 1; n <= 20; n++)
            db.AnimeThemes.Add(Song(1, 100 + n, sequence: n));
        db.AnimeThemes.AddRange(Song(2, 200), Song(2, 201, sequence: 2));
        await db.SaveChangesAsync();

        var counts = await CountAnimeAsync(Make(db), AnyTheme, 2000);

        Assert.InRange(counts[1], 850, 1150);
        Assert.InRange(counts[2], 850, 1150);
    }

    // ---- what counts as a right guess ----

    [Fact]
    public async Task AcceptedIsTheDrawnAnimeAloneForASongNobodyElseHas()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, []);

        Assert.Equal([1], round!.AcceptedAnimeIds);
    }

    [Fact]
    public async Task AcceptedAddsEveryOtherAnimeTheCatalogueListsWithTheSameSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        // Anime 7 and 9 aren't stored at all; 8 holds the song as a spoiler, which makes it
        // unusable to draw but no less the same song.
        db.AnimeThemes.AddRange(
            Song(1, 500), Song(9, 500, AnimeThemeKind.Ending), Song(7, 500), Song(8, 500, spoiler: true));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, []);

        Assert.Equal(1, round!.AnimeId);
        Assert.Equal([1, 7, 8, 9], round.AcceptedAnimeIds);
    }

    [Fact]
    public async Task AcceptedLeavesOutAnotherSeasonWithADifferentSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 500), Song(2, 501));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme, []);

        Assert.Equal([1], round!.AcceptedAnimeIds);
    }

    [Fact]
    public async Task AcceptedIsFilledWhicheverAnimeOfASharedSongIsDrawn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 500), Song(2, 500));
        await db.SaveChangesAsync();

        var rounds = new List<GuessTheThemeRound>();
        for (var seed = 0; seed < 20; seed++)
            rounds.Add((await Make(db, seed).DrawAsync(AnyTheme, []))!);

        Assert.Equal([1, 2], rounds.Select(r => r.AnimeId).Distinct().Order());
        Assert.All(rounds, r => Assert.Equal([1, 2], r.AcceptedAnimeIds));
    }

    [Fact]
    public async Task AcceptedIsNotNarrowedByTheSettings()
    {
        // Only my list, and the other anime that shares the song isn't on it.
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.UserAnimeEntries.Add(OnList(1));
        db.AnimeThemes.AddRange(Song(1, 500), Song(2, 500));
        await db.SaveChangesAsync();

        var round = await Make(db).DrawAsync(AnyTheme with { List = GameListScope.OnlyMyList }, []);

        Assert.Equal(1, round!.AnimeId);
        Assert.Equal([1, 2], round.AcceptedAnimeIds);
    }
}
