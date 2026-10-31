using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeServiceTests;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// What a game has read ahead, and what is forgotten when (add-anime-themes-and-guess-the-theme
// design.md D10): every round holds its own song and the next, a loss keeps the next for Play
// again, End game keeps nothing for later, and the setup screen readies one song's audio.
public class GuessTheThemeServiceMediaTests
{
    private static async Task<List<GuessTheThemeRound>> RoundsAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRounds.AsNoTracking().OrderBy(r => r.Number).ToListAsync();

    /// <summary>Anime 1..count, each with one song whose id is ten times its own.</summary>
    private static async Task SeedAsync(AnimeTrackerDbContext db, int count)
    {
        for (var id = 1; id <= count; id++)
        {
            db.AnimeMetadata.Add(Anime(id));
            db.AnimeThemes.Add(Song(id, id * 10));
        }

        await db.SaveChangesAsync();
    }

    private static HeldSong Whole(GuessTheThemeRound round) => new(round.AudioUrl, round.VideoUrl);
    private static HeldSong VideoOf(GuessTheThemeRound round) => new(null, round.VideoUrl);

    /// <summary>A game at its last length, so the next skip loses it.</summary>
    private static async Task<(GuessTheThemeRun Run, GuessTheThemeRound Round)> AtTheLastLengthAsync(
        AnimeTrackerDbContext db, bool withNextSong)
    {
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        var (run, round) = await AddThemeRunAsync(db, 1, songId: 10, step: GuessTheThemeLadder.LastStep, played: [10, 20]);
        if (withNextSong)
        {
            db.AnimeMetadata.Add(Anime(2));
            db.AnimeThemes.Add(Song(2, 20));
            AddRound(db, run.Id, 2, 2, 20);
            await db.SaveChangesAsync();
        }

        return (run, round);
    }

    // ---- a game in play ----

    [Fact]
    public async Task AGameHoldsItsFirstSongAndTheNext()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();

        await Make(db, media: media).StartAsync(AnyTheme);

        var rounds = await RoundsAsync(db);
        Assert.Equal([Whole(rounds[0]), Whole(rounds[1])], Assert.Single(media.Kept));
    }

    [Fact]
    public async Task AGameOfOneSongHoldsThatSong()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var media = new FakeMediaHold();

        await Make(db, media: media).StartAsync(AnyTheme);

        Assert.Equal([Whole((await RoundsAsync(db))[0])], Assert.Single(media.Kept));
    }

    [Fact]
    public async Task ARefusedStartHoldsNothingNew()
    {
        using var db = CreateDb();
        var media = new FakeMediaHold();

        await Assert.ThrowsAsync<GuessTheThemeNotPlayableException>(() => Make(db, media: media).StartAsync(AnyTheme));

        Assert.Empty(media.Kept);
    }

    [Fact]
    public async Task NextSongMovesTheHoldOnToTheNewSongAndTheOneAfter()
    {
        using var db = CreateDb();
        await SeedAsync(db, 4);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);
        var first = await RoundsAsync(db);
        var right = await service.GuessAsync(game.Id, game.Move, first[0].AnimeId);

        await service.NextAsync(game.Id, right.Game!.Move);

        var rounds = await RoundsAsync(db);
        Assert.Equal([2, 3], rounds.Select(r => r.Number));
        Assert.Equal([Whole(rounds[0]), Whole(rounds[1])], media.Kept[^1]);
        Assert.DoesNotContain(Whole(first[0]), media.Kept[^1]);
    }

    [Fact]
    public async Task ARightGuessWithTheNextSongDrawnChangesNothingHeld()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);
        var rounds = await RoundsAsync(db);

        await service.GuessAsync(game.Id, game.Move, rounds[0].AnimeId);

        Assert.Single(media.Kept);
    }

    [Fact]
    public async Task AWrongGuessChangesNothingHeld()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);
        var rounds = await RoundsAsync(db);
        var wrong = new[] { 1, 2, 3 }.First(id => !rounds[0].AcceptedAnimeIds.Contains(id));

        await service.GuessAsync(game.Id, game.Move, wrong);

        Assert.Single(media.Kept);
    }

    [Fact]
    public async Task ARightGuessThatDrawsTheNextSongHoldsIt()
    {
        using var db = CreateDb();
        await SeedAsync(db, 2);
        var (run, round) = await AddThemeRunAsync(db, 1, songId: 10);
        var media = new FakeMediaHold();

        await Make(db, media: media).GuessAsync(run.Id, 0, 1);

        var rounds = await RoundsAsync(db);
        Assert.Equal(2, rounds.Count);
        Assert.Equal([Whole(round), Whole(rounds[1])], Assert.Single(media.Kept));
    }

    [Fact]
    public async Task AReplacedSongIsHeldWithTheOneAfterIt()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);

        await service.ReplaceAsync(game.Id, game.Round);

        var rounds = await RoundsAsync(db);
        Assert.Equal([Whole(rounds[0]), Whole(rounds[1])], media.Kept[^1]);
    }

    [Fact]
    public async Task AReplacementWithNothingLeftHoldsNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);

        var replaced = await service.ReplaceAsync(game.Id, game.Round);

        Assert.Equal(GuessTheThemeOutcome.Cleared, replaced.Outcome);
        Assert.Empty(media.Kept[^1]);
    }

    // ---- a game ending ----

    [Fact]
    public async Task EndGameKeepsTheVideoOnScreenAndNothingElse()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);
        var rounds = await RoundsAsync(db);

        await service.EndAsync(game.Id);

        Assert.Equal([VideoOf(rounds[0])], media.Kept[^1]);
    }

    [Fact]
    public async Task EndGameDuringARevealKeepsThatVideoAndNothingElse()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        var game = await service.StartAsync(AnyTheme);
        var rounds = await RoundsAsync(db);
        await service.GuessAsync(game.Id, game.Move, rounds[0].AnimeId);

        await service.EndAsync(game.Id);

        Assert.Equal([VideoOf(rounds[0])], media.Kept[^1]);
    }

    [Fact]
    public async Task EndGameForgetsTheSongSoPlayAgainDrawsAfresh()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);
        var game = await service.StartAsync(AnyTheme);
        await service.PrepareAsync(AnyTheme);

        await service.EndAsync(game.Id);

        Assert.Null(onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task ALossKeepsTheVideoOnScreenAndTheSongThatWasNext()
    {
        using var db = CreateDb();
        var (run, round) = await AtTheLastLengthAsync(db, withNextSong: true);
        var next = (await RoundsAsync(db))[1];
        var media = new FakeMediaHold();

        var lost = await Make(db, media: media).SkipAsync(run.Id, 0);

        Assert.Equal(GuessTheThemeOutcome.Lost, lost.Outcome);
        Assert.Equal([VideoOf(round), Whole(next)], Assert.Single(media.Kept));
    }

    [Fact]
    public async Task ALossWithNoNextSongKeepsOnlyTheVideo()
    {
        using var db = CreateDb();
        var (run, round) = await AtTheLastLengthAsync(db, withNextSong: false);
        var media = new FakeMediaHold();
        var onDeck = new GuessTheThemeOnDeck();

        await Make(db, media: media, onDeck: onDeck).SkipAsync(run.Id, 0);

        Assert.Equal([VideoOf(round)], Assert.Single(media.Kept));
        Assert.Null(onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task PlayAgainAfterALossStartsOnTheSongThatWasNext()
    {
        using var db = CreateDb();
        var (run, _) = await AtTheLastLengthAsync(db, withNextSong: true);
        var next = (await RoundsAsync(db))[1];
        db.AnimeMetadata.Add(Anime(3));
        db.AnimeThemes.Add(Song(3, 30));
        await db.SaveChangesAsync();
        var onDeck = new GuessTheThemeOnDeck();
        var media = new FakeMediaHold();
        var service = Make(db, media: media, onDeck: onDeck);
        await service.SkipAsync(run.Id, 0);

        await service.StartAsync(AnyTheme);

        var rounds = await RoundsAsync(db);
        Assert.Equal(next.SongId, rounds[0].SongId);
        Assert.Equal(1, rounds[0].Number);
        Assert.Equal(2, rounds[0].AnimeId);
        Assert.Contains(new HeldSong(next.AudioUrl, next.VideoUrl), media.Kept[^1]);
    }

    [Fact]
    public async Task ClearingTheGameKeepsOnlyTheVideo()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var (run, round) = await AddThemeRunAsync(db, 1, songId: 10);
        var media = new FakeMediaHold();
        var onDeck = new GuessTheThemeOnDeck();
        onDeck.Put(AnyTheme, round);

        var cleared = await Make(db, media: media, onDeck: onDeck).GuessAsync(run.Id, 0, 1);

        Assert.Equal(GuessTheThemeOutcome.Cleared, cleared.Outcome);
        Assert.Equal([VideoOf(round)], Assert.Single(media.Kept));
        Assert.Null(onDeck.Peek(AnyTheme));
    }

    // ---- the setup screen ----

    [Fact]
    public async Task TheSetupScreenReadiesOneSongsAudioAndNotItsVideo()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();

        await Make(db, media: media).PrepareAsync(AnyTheme);

        var held = Assert.Single(Assert.Single(media.Kept));
        Assert.NotNull(held.AudioUrl);
        Assert.Null(held.VideoUrl);
    }

    [Fact]
    public async Task PlayStartsOnTheSongTheSetupScreenReadied()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);
        var ready = onDeck.Peek(AnyTheme)!;

        await service.StartAsync(AnyTheme);

        Assert.Equal(ready.SongId, (await RoundsAsync(db))[0].SongId);
    }

    [Fact]
    public async Task ReadiedAgainWithTheSameSettingsItKeepsTheSameSong()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);
        var ready = onDeck.Peek(AnyTheme)!;

        await service.PrepareAsync(AnyTheme);

        Assert.Same(ready, onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task ReadiedForOtherSettingsItDrawsForThose()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);
        var openings = AnyTheme with { Themes = GameThemeChoice.Openings };

        await service.PrepareAsync(openings);

        Assert.NotNull(onDeck.Peek(openings));
        Assert.Null(onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task PlayWithOtherSettingsThanTheReadiedSongDrawsForThose()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10, AnimeThemeKind.Opening));
        db.AnimeMetadata.Add(Anime(2));
        db.AnimeThemes.Add(Song(2, 20, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();
        var service = Make(db, onDeck: new GuessTheThemeOnDeck());
        var endings = AnyTheme with { Themes = GameThemeChoice.Endings };
        await service.PrepareAsync(AnyTheme with { Themes = GameThemeChoice.Openings });

        await service.StartAsync(endings);

        Assert.Equal(20, (await RoundsAsync(db))[0].SongId);
    }

    [Fact]
    public async Task ASongTheCatalogueHasSinceDroppedIsNotPlayed()
    {
        using var db = CreateDb();
        await SeedAsync(db, 2);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);
        var ready = onDeck.Peek(AnyTheme)!;
        db.AnimeThemes.Remove(await db.AnimeThemes.SingleAsync(t => t.AnimeId == ready.AnimeId));
        await db.SaveChangesAsync();

        await service.StartAsync(AnyTheme);

        Assert.NotEqual(ready.SongId, (await RoundsAsync(db))[0].SongId);
    }

    [Fact]
    public async Task ASongWhoseAnimeHasJoinedMyListIsNotPlayedUnderNotOnMyList()
    {
        using var db = CreateDb();
        await SeedAsync(db, 2);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        var notOnMyList = AnyTheme with { List = GameListScope.NotOnMyList };
        await service.PrepareAsync(notOnMyList);
        var ready = onDeck.Peek(notOnMyList)!;
        db.UserAnimeEntries.Add(OnList(ready.AnimeId));
        await db.SaveChangesAsync();

        await service.StartAsync(notOnMyList);

        Assert.NotEqual(ready.AnimeId, (await RoundsAsync(db))[0].AnimeId);
    }

    [Fact]
    public async Task AReadiedSongIsPlayedOnce()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var onDeck = new GuessTheThemeOnDeck();
        var service = Make(db, onDeck: onDeck);
        await service.PrepareAsync(AnyTheme);

        await service.StartAsync(AnyTheme);

        Assert.Null(onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task WithNoSongForTheSettingsNothingIsReadied()
    {
        using var db = CreateDb();
        var media = new FakeMediaHold();
        var onDeck = new GuessTheThemeOnDeck();

        await Make(db, media: media, onDeck: onDeck).PrepareAsync(AnyTheme);

        Assert.Empty(Assert.Single(media.Kept));
        Assert.Null(onDeck.Peek(AnyTheme));
    }

    [Fact]
    public async Task AGameInProgressKeepsItsHoldWhateverASetupScreenAsks()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var media = new FakeMediaHold();
        var service = Make(db, media: media);
        await service.StartAsync(AnyTheme);

        await service.PrepareAsync(AnyTheme);

        Assert.Single(media.Kept);
    }

    [Fact]
    public async Task SettingsThatCannotDescribeAGameAreRefused()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<GameSettingsException>(() =>
            Make(db).PrepareAsync(AnyTheme with { YearFrom = 2020, YearTo = 2010 }));
    }

    // ---- a reveal's video ----

    [Fact]
    public async Task ARevealGivesTheAppsOwnAddressForItsVideoAndTheFileBeside()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var round = (await RoundsAsync(db))[0];

        var right = await service.GuessAsync(game.Id, game.Move, round.AnimeId);

        var reveal = right.Game!.Reveal!;
        Assert.Equal($"/api/games/guess-the-theme/video?anime={round.AnimeId}&theme={round.ThemeId}", reveal.PlayUrl);
        Assert.Equal(round.VideoUrl, reveal.VideoUrl);
    }

    [Fact]
    public async Task ARevealsAddressFindsItsFileAfterTheGameHasGone()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var round = (await RoundsAsync(db))[0];

        await service.EndAsync(game.Id);

        Assert.Equal(round.VideoUrl, await service.GetVideoSourceAsync(round.AnimeId, round.ThemeId));
    }

    [Fact]
    public async Task AThemeTheCatalogueDoesNotHaveHasNoVideo()
    {
        using var db = CreateDb();

        Assert.Null(await Make(db).GetVideoSourceAsync(1, 2));
    }

    // ---- what is ready belongs to the settings it was drawn with ----

    [Fact]
    public void TheOrderTypesWereTickedInMeansNothing()
    {
        var onDeck = new GuessTheThemeOnDeck();
        var song = new GuessTheThemeRound();

        onDeck.Put(AnyTheme with { MediaTypes = ["tv", "movie"] }, song);

        Assert.Same(song, onDeck.Peek(AnyTheme with { MediaTypes = ["movie", "tv"] }));
    }

    [Fact]
    public void AnEmptyListOfTypesIsEveryType()
    {
        var onDeck = new GuessTheThemeOnDeck();
        var song = new GuessTheThemeRound();

        onDeck.Put(AnyTheme, song);

        Assert.Same(song, onDeck.Peek(AnyTheme with { MediaTypes = [] }));
    }

    [Theory]
    [InlineData(2000, null, GameListScope.All, false, GameThemeChoice.All)]
    [InlineData(null, 2000, GameListScope.All, false, GameThemeChoice.All)]
    [InlineData(null, null, GameListScope.OnlyMyList, false, GameThemeChoice.All)]
    [InlineData(null, null, GameListScope.All, true, GameThemeChoice.All)]
    [InlineData(null, null, GameListScope.All, false, GameThemeChoice.Endings)]
    public void EverySettingTellsOneGameFromAnother(
        int? yearFrom, int? yearTo, GameListScope list, bool hideHentai, GameThemeChoice themes)
    {
        var onDeck = new GuessTheThemeOnDeck();
        onDeck.Put(AnyTheme, new GuessTheThemeRound());

        Assert.Null(onDeck.Peek(new GuessTheThemeSettings(yearFrom, yearTo, null, list, hideHentai, themes)));
    }

    [Fact]
    public void TakingASongEmptiesItForOtherSettingsToo()
    {
        var onDeck = new GuessTheThemeOnDeck();
        onDeck.Put(AnyTheme, new GuessTheThemeRound());

        Assert.Null(onDeck.Take(AnyTheme with { Themes = GameThemeChoice.Endings }));

        Assert.Null(onDeck.Peek(AnyTheme));
    }
}
