using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme tasks 6.2 and 6.3: GuessTheThemeService's reads and
// Start on InMemory. Moves and endings are in GuessTheThemeServiceMovesTests.
public class GuessTheThemeServiceTests
{
    internal static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // The lobby reads share the service's Random, as they do in the app.
    internal static GuessTheThemeService Make(
        AnimeTrackerDbContext db,
        int seed = 11,
        IAnimeThemesMediaRelay? relay = null,
        IAnimeThemesMediaHold? media = null,
        GuessTheThemeOnDeck? onDeck = null)
    {
        var random = new Random(seed);
        return new GuessTheThemeService(
            db,
            new GuessTheThemeDraw(db, random),
            new GameLobbyReads(db, random),
            relay ?? new FakeMediaRelay(),
            media ?? new FakeMediaHold(),
            onDeck ?? new GuessTheThemeOnDeck());
    }

    private static async Task<List<GuessTheThemeRound>> RoundsAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRounds.AsNoTracking().OrderBy(r => r.RunId).ThenBy(r => r.Number).ToListAsync();

    private static async Task<GuessTheThemeRun> StoredRunAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();

    // ---- the read both pages open on (6.2) ----

    [Fact]
    public async Task WithNoGameTheReadHasNoGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airedFrom: new DateOnly(2013, 4, 7)));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        var state = await Make(db).GetStateAsync(hideHentai: false);

        Assert.Null(state.Game);
        Assert.Equal(new GameYearsDto(2013, 2013), state.Years);
    }

    [Fact]
    public async Task TheReadSaysWhetherTheCatalogueHasEverBeenLoaded()
    {
        using var db = CreateDb();
        var before = await Make(db).GetStateAsync(false);

        MarkSynced(db);
        await db.SaveChangesAsync();
        var after = await Make(db).GetStateAsync(false);

        Assert.False(before.CatalogueReady);
        Assert.True(after.CatalogueReady);
    }

    [Fact]
    public async Task AFailedFirstAttemptIsStillNotReady()
    {
        using var db = CreateDb();
        db.AnimeThemesSyncStates.Add(new AnimeThemesSyncState { LastSyncedAt = null, LastAttemptAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        Assert.False((await Make(db).GetStateAsync(false)).CatalogueReady);
    }

    [Fact]
    public async Task AGameInProgressIsInTheReadAtItsSongAndLength()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Anime(3));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1, step: 2, roundGuesses: [3, 2], moves: 4, round: 6);

        var game = (await Make(db).GetStateAsync(false)).Game;

        Assert.NotNull(game);
        Assert.Equal(run.Id, game.Id);
        Assert.Equal(4, game.Move);
        Assert.Equal(6, game.Round);
        Assert.Equal(2, game.Step);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Equal([0.1, 0.5, 1, 3, 5, 10], game.Lengths);
        Assert.Equal([3, 2], game.GuessedIds);
        Assert.Equal([3, 2], game.Guessed.Select(c => c.AnimeId));
        Assert.Null(game.Reveal);
        Assert.Null(game.NextRound);
    }

    [Fact]
    public async Task AGuessedCardCarriesTheTitlesAndPicture()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        await AddThemeRunAsync(db, animeId: 1, step: 1, roundGuesses: [2]);

        var card = Assert.Single((await Make(db).GetStateAsync(false)).Game!.Guessed);

        Assert.Equal(2, card.AnimeId);
        Assert.Equal("Anime 2", card.Title);
        Assert.Equal("English 2", card.EnglishTitle);
        Assert.Equal("https://img/2.jpg", card.PictureUrl);
    }

    [Fact]
    public async Task AGuessOfAnAnimeNotStoredKeepsItsIdAndHasNoCard()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        await AddThemeRunAsync(db, animeId: 1, step: 2, roundGuesses: [999, 2]);

        var game = (await Make(db).GetStateAsync(false)).Game!;

        Assert.Equal([999, 2], game.GuessedIds);
        Assert.Equal([2], game.Guessed.Select(c => c.AnimeId));
    }

    [Fact]
    public async Task AGameDuringARevealCarriesItsRevealAndTheSongDrawnAfterIt()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1, step: 3, phase: GuessTheThemePhase.Revealed, round: 4);
        AddRound(db, run.Id, 5, animeId: 2, songId: 600);
        await db.SaveChangesAsync();

        var game = (await Make(db).GetStateAsync(false)).Game!;

        Assert.Equal(GuessTheThemePhase.Revealed, game.Phase);
        Assert.Equal(5, game.NextRound);
        Assert.NotNull(game.Reveal);
        Assert.Equal(1, game.Reveal.AnimeId);
        Assert.Equal("Anime 1", game.Reveal.Title);
        Assert.Equal("English 1", game.Reveal.EnglishTitle);
        Assert.Equal("https://img/1.jpg", game.Reveal.PictureUrl);
        Assert.Equal(AnimeThemeKind.Opening, game.Reveal.Kind);
        Assert.Equal("OP1", game.Reveal.Label);
        Assert.Equal("Song 500", game.Reveal.SongTitle);
        Assert.Equal(["Artist A"], game.Reveal.Artists);
        Assert.StartsWith("https://v.animethemes.moe/", game.Reveal.VideoUrl);
        Assert.True(game.Reveal.InMyList);
        // Got at the length the run is on.
        Assert.Equal(3, game.Reveal.HeardSeconds);
    }

    [Fact]
    public async Task AnAnimeNotOnMyListIsSaidNotToBe()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1, phase: GuessTheThemePhase.Revealed);
        AddRound(db, run.Id, 2, animeId: 1, songId: 600);
        await db.SaveChangesAsync();

        Assert.False((await Make(db).GetStateAsync(false)).Game!.Reveal!.InMyList);
    }

    [Fact]
    public async Task ARevealAppearsOnlyInTheRevealedPhase()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1);
        AddRound(db, run.Id, 2, animeId: 2, songId: 600); // a song drawn but not heard
        await db.SaveChangesAsync();

        var game = (await Make(db).GetStateAsync(false)).Game!;

        // The song drawn for after this one is a number the board loads its audio by, and
        // names nothing; only the reveal does.
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Null(game.Reveal);
        Assert.Equal(2, game.NextRound);
    }

    [Fact]
    public async Task ARunWithNoCurrentRoundCountsAsNoGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.GuessTheThemeRuns.Add(new GuessTheThemeRun { Round = 3, PlayedSongIds = [], RoundGuesses = [] });
        await db.SaveChangesAsync();

        var state = await Make(db).GetStateAsync(false);

        Assert.Null(state.Game);
        // A read doesn't write: the stray row is Start's to replace.
        Assert.Equal(1, await db.GuessTheThemeRuns.CountAsync());
    }

    [Fact]
    public async Task ARunWhoseCurrentRoundIsMissingButWhoseNextIsThereCountsAsNoGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        var (run, round) = await AddThemeRunAsync(db, animeId: 1, phase: GuessTheThemePhase.Revealed);
        AddRound(db, run.Id, 2, animeId: 2, songId: 600);
        db.GuessTheThemeRounds.Remove(round);
        await db.SaveChangesAsync();

        Assert.Null((await Make(db).GetStateAsync(false)).Game);
    }

    // ---- nothing that names an unrevealed song is sent ----

    private const int HiddenAnime = 41467;
    private const int HiddenSong = 77777;

    private static void AssertNamesNothingOf(string json, AnimeMetadata anime)
    {
        Assert.DoesNotContain(HiddenAnime.ToString(), json);
        Assert.DoesNotContain(HiddenSong.ToString(), json);
        Assert.DoesNotContain(anime.Title, json);
        Assert.DoesNotContain(anime.EnglishTitle!, json);
        Assert.DoesNotContain(anime.PictureUrl!, json);
        Assert.DoesNotContain("Song", json);
        Assert.DoesNotContain("animethemes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("OP1", json);
        Assert.DoesNotContain("Artist", json);
        Assert.DoesNotContain("videoUrl", json);
        Assert.DoesNotContain("audioUrl", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reveal\":{", json);
    }

    [Fact]
    public async Task NothingThatNamesTheSongIsInTheStateOfARound()
    {
        using var db = CreateDb();
        var hidden = Anime(HiddenAnime, rank: 1);
        db.AnimeMetadata.AddRange(hidden, Anime(2, rank: 2), Anime(3, rank: 3));
        db.AnimeThemes.Add(Song(HiddenAnime, HiddenSong));
        await db.SaveChangesAsync();
        await AddThemeRunAsync(db, animeId: HiddenAnime, songId: HiddenSong, step: 1, roundGuesses: [2]);

        var state = await Make(db).GetStateAsync(false);

        // Rank 1 would be the likeliest poster, so this also shows the poster is left out.
        AssertNamesNothingOf(JsonSerializer.Serialize(state, Wire), hidden);
    }

    [Fact]
    public async Task NothingThatNamesTheSongIsInTheStartResponse()
    {
        using var db = CreateDb();
        var hidden = Anime(HiddenAnime);
        db.AnimeMetadata.Add(hidden);
        db.AnimeThemes.Add(Song(HiddenAnime, HiddenSong));
        await db.SaveChangesAsync();

        var game = await Make(db).StartAsync(AnyTheme);

        AssertNamesNothingOf(JsonSerializer.Serialize(game, Wire), hidden);
    }

    // ---- the posters ----

    [Fact]
    public async Task ThePostersLeaveOutTheAnimeBeingHidden()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 1), Anime(2, rank: 2), Anime(3, rank: 3));
        await db.SaveChangesAsync();
        await AddThemeRunAsync(db, animeId: 1);

        for (var seed = 0; seed < 25; seed++)
        {
            var pictures = (await Make(db, seed).GetStateAsync(false)).Pictures;

            Assert.Equal(2, pictures.Count);
            Assert.DoesNotContain("https://img/1.jpg", pictures);
        }
    }

    [Fact]
    public async Task DuringARevealThePostersAlsoLeaveOutTheSongDrawnNext()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 1), Anime(2, rank: 2), Anime(3, rank: 3));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1, phase: GuessTheThemePhase.Revealed);
        AddRound(db, run.Id, 2, animeId: 2, songId: 600);
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 25; seed++)
        {
            var pictures = (await Make(db, seed).GetStateAsync(false)).Pictures;

            // Only anime 3 is left to show: neither the revealed song's nor the next one's.
            Assert.Equal(["https://img/3.jpg"], pictures);
        }
    }

    [Fact]
    public async Task WithNoGameThePostersAreOfPopularAnime()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 1), Anime(2, rank: 2), Anime(3, rank: 3));
        await db.SaveChangesAsync();

        Assert.Equal(2, (await Make(db).GetStateAsync(false)).Pictures.Count);
    }

    [Fact]
    public async Task HideNsfwLeavesAnRxAnimeOutOfThePostersAndYears()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, rank: 1, rating: "rx", airedFrom: new DateOnly(1999, 1, 1)),
            Anime(2, rank: 2, airedFrom: new DateOnly(2010, 1, 1)));
        await db.SaveChangesAsync();

        var shown = await Make(db).GetStateAsync(hideHentai: false);
        var hidden = await Make(db).GetStateAsync(hideHentai: true);

        Assert.Contains("https://img/1.jpg", shown.Pictures);
        Assert.Equal(new GameYearsDto(1999, 2010), shown.Years);
        Assert.Equal(["https://img/2.jpg"], hidden.Pictures);
        Assert.Equal(new GameYearsDto(2010, 2010), hidden.Years);
    }

    // ---- the count ----

    [Fact]
    public async Task ThePoolCountsAnimeWithAUsableSongAndSaysWhetherThatIsEnough()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Anime(3));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, spoiler: true));
        await db.SaveChangesAsync();

        var pool = await Make(db).GetPoolAsync(AnyTheme);

        Assert.Equal(new GamePoolDto(1, true), pool);
    }

    [Fact]
    public async Task ThePoolWithNoSongsIsNotPlayable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        Assert.Equal(new GamePoolDto(0, false), await Make(db).GetPoolAsync(AnyTheme));
    }

    [Fact]
    public async Task ThePoolFollowsTheThemeSetting()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();
        var service = Make(db);

        Assert.Equal(2, (await service.GetPoolAsync(AnyTheme)).Count);
        Assert.Equal(1, (await service.GetPoolAsync(AnyTheme with { Themes = GameThemeChoice.Endings })).Count);
    }

    [Fact]
    public async Task ThePoolRefusesSettingsThatCannotDescribeAGame()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<GameSettingsException>(() => Make(db).GetPoolAsync(AnyTheme with { YearFrom = 2020, YearTo = 2010 }));
        await Assert.ThrowsAsync<GameSettingsException>(() => Make(db).GetPoolAsync(AnyTheme with { Themes = (GameThemeChoice)5 }));
    }

    // ---- Start (6.3) ----

    [Fact]
    public async Task StartDrawsTheFirstSongAndStoresTheGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 42, AnimeThemeKind.Ending, sequence: 2));
        await db.SaveChangesAsync();

        var game = await Make(db).StartAsync(AnyTheme);

        var run = await StoredRunAsync(db);
        Assert.Equal(run.Id, game.Id);
        Assert.Equal(0, game.Move);
        Assert.Equal(1, game.Round);
        Assert.Equal(0, game.Step);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Equal([0.1, 0.5, 1, 3, 5, 10], game.Lengths);
        Assert.Empty(game.GuessedIds);
        Assert.Null(game.Reveal);
        Assert.Null(game.NextRound);

        Assert.Equal([42], run.PlayedSongIds);
        Assert.Empty(run.RoundGuesses);
        var round = Assert.Single(await RoundsAsync(db));
        Assert.Equal((run.Id, 1, 1, 42, "ED2"), (round.RunId, round.Number, round.AnimeId, round.SongId, round.Label));
    }

    [Fact]
    public async Task StartStoresTheSettingsTheGameBeganWith()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airedFrom: new DateOnly(2005, 5, 5)));
        db.AnimeThemes.Add(Song(1, 10));
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();

        await Make(db).StartAsync(new GuessTheThemeSettings(
            2000, 2010, ["tv", "tv"], GameListScope.OnlyMyList, true, GameThemeChoice.Openings));

        var run = await StoredRunAsync(db);
        Assert.Equal(2000, run.YearFrom);
        Assert.Equal(2010, run.YearTo);
        Assert.Equal(["tv"], run.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, run.ListScope);
        Assert.True(run.HideHentai);
        Assert.Equal(GameThemeChoice.Openings, run.Themes);
    }

    [Fact]
    public async Task StartWithNoTypesStoresEveryType()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();

        await Make(db).StartAsync(AnyTheme with { MediaTypes = [] });

        Assert.Null((await StoredRunAsync(db)).MediaTypes);
    }

    [Fact]
    public async Task StartRecordsTheAcceptedAnimeOfASharedSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.AddRange(Song(1, 500), Song(8, 500, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        await Make(db).StartAsync(AnyTheme);

        Assert.Equal([1, 8], (await RoundsAsync(db)).Single().AcceptedAnimeIds);
    }

    [Fact]
    public async Task StartWithNoSongRefusesAndStoresNothing()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<GuessTheThemeNotPlayableException>(() => Make(db).StartAsync(AnyTheme));

        Assert.Empty(await db.GuessTheThemeRuns.ToListAsync());
        Assert.Empty(await RoundsAsync(db));
    }

    [Fact]
    public async Task StartBeforeTheCatalogueIsLoadedRefuses()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<GuessTheThemeNotPlayableException>(() => Make(db).StartAsync(AnyTheme));
    }

    [Fact]
    public async Task AStartThatIsRefusedLeavesAGameInProgressAsItWas()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.Add(Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1, step: 3, roundGuesses: [2], moves: 5);

        // Only openings, and the only usable song is an ending: nothing to draw.
        await Assert.ThrowsAsync<GuessTheThemeNotPlayableException>(
            () => Make(db).StartAsync(AnyTheme with { Themes = GameThemeChoice.Openings }));

        var kept = await StoredRunAsync(db);
        Assert.Equal((run.Id, 3, 5), (kept.Id, kept.Step, kept.Moves));
        Assert.Equal([2], kept.RoundGuesses);
        Assert.Single(await RoundsAsync(db));
    }

    [Fact]
    public async Task InvalidSettingsAreRefusedBeforeAnythingIsDeleted()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, animeId: 1);

        await Assert.ThrowsAsync<GameSettingsException>(
            () => Make(db).StartAsync(AnyTheme with { YearFrom = 2020, YearTo = 2010 }));
        await Assert.ThrowsAsync<GameSettingsException>(
            () => Make(db).StartAsync(AnyTheme with { Themes = (GameThemeChoice)5 }));

        Assert.Equal(run.Id, (await StoredRunAsync(db)).Id);
        Assert.Single(await RoundsAsync(db));
    }

    [Fact]
    public async Task StartReplacesAGameInProgressAndItsSongs()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.Add(Song(2, 20));
        await db.SaveChangesAsync();
        var (old, _) = await AddThemeRunAsync(db, animeId: 1, songId: 500, phase: GuessTheThemePhase.Revealed, step: 4, roundGuesses: [2]);
        AddRound(db, old.Id, 2, animeId: 1, songId: 501);
        await db.SaveChangesAsync();

        var game = await Make(db).StartAsync(AnyTheme);

        var run = await StoredRunAsync(db);
        Assert.NotEqual(old.Id, run.Id);
        Assert.Equal(run.Id, game.Id);
        Assert.Equal(0, run.Step);
        Assert.Empty(run.RoundGuesses);
        var round = Assert.Single(await RoundsAsync(db));
        Assert.Equal((run.Id, 1, 2, 20), (round.RunId, round.Number, round.AnimeId, round.SongId));
        // The new game does not carry the old one's songs.
        Assert.Equal([20], run.PlayedSongIds);
    }

    [Fact]
    public async Task StartLeavesAGameOfEitherOtherGameAlone()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Whole(3));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();
        var higher = await AddRunAsync(db, left: 2, right: 3, streak: 4);
        var guess = await AddGuessRunAsync(db, answer: 3, guessed: [2]);

        await Make(db).StartAsync(AnyTheme);

        var keptHigher = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        var keptGuess = await db.GuessTheAnimeRuns.AsNoTracking().SingleAsync();
        Assert.Equal((higher.Id, 4), (keptHigher.Id, keptHigher.Streak));
        Assert.Equal((guess.Id, 1), (keptGuess.Id, keptGuess.GuessCount));
    }

    [Fact]
    public async Task StartRetriesOnceWhenAMoveLandsWhileTheOldGameIsBeingDeleted()
    {
        const string name = "start-while-guessing";
        using var tabOne = CreateDb(name);
        tabOne.AnimeMetadata.AddRange(Anime(1), Anime(2));
        tabOne.AnimeThemes.Add(Song(2, 20));
        await tabOne.SaveChangesAsync();
        var (old, _) = await AddThemeRunAsync(tabOne, animeId: 1);
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheThemeRuns.Single();
            stored.Step = 1;
            stored.Moves++;
            tabTwo.SaveChanges();
        };

        var game = await Make(tabOne).StartAsync(AnyTheme);

        using var check = CreateDb(name);
        var run = await StoredRunAsync(check);
        Assert.NotEqual(old.Id, run.Id);
        Assert.Equal(run.Id, game.Id);
        Assert.Single(await RoundsAsync(check));
    }
}
