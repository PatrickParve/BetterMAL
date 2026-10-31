using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeServiceTests;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme tasks 7.2 and 7.3 (the service half): a song whose
// file has gone is replaced for its round, and the relay is handed a round's link by the
// service alone.
public class GuessTheThemeServiceReplaceTests
{
    private const int Gone = 41467;
    private const int GoneSong = 77777;

    private static async Task<List<GuessTheThemeRound>> RoundsAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRounds.AsNoTracking().OrderBy(r => r.Number).ToListAsync();

    /// <summary>A game on <see cref="Gone"/>'s song at <paramref name="step"/> with one other song in
    /// the catalogue to be drawn instead.</summary>
    private static async Task<GuessTheThemeRun> GameWithASpareAsync(
        AnimeTrackerDbContext db, int step = 0, int[]? roundGuesses = null, int moves = 0,
        GuessTheThemePhase phase = GuessTheThemePhase.Listening, GuessTheThemeSettings? settings = null)
    {
        db.AnimeMetadata.AddRange(Anime(Gone), Anime(2));
        db.AnimeThemes.AddRange(Song(Gone, GoneSong), Song(2, 20), Song(2, 21, AnimeThemeKind.Ending, sequence: 2));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(
            db, Gone, GoneSong, step: step, roundGuesses: roundGuesses, moves: moves, phase: phase, settings: settings);
        return run;
    }

    // ---- a song whose file has gone ----

    [Fact]
    public async Task AGoneFileIsReplacedByAnotherSongForTheSameRound()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db);

        var result = await Make(db).ReplaceAsync(run.Id, 1);

        Assert.Null(result.Outcome);
        var game = result.Game!;
        Assert.Equal(1, game.Round);
        Assert.Equal(0, game.Step);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Null(game.Reveal);
        var round = Assert.Single(await RoundsAsync(db));
        Assert.Equal(1, round.Number);
        Assert.Equal(2, round.AnimeId);
        Assert.Contains(round.SongId, new[] { 20, 21 });
    }

    [Fact]
    public async Task TheNewSongStartsAtTheFirstLengthWithNothingGuessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(9));
        var run = await GameWithASpareAsync(db, step: 3, roundGuesses: [9], moves: 4);

        var game = (await Make(db).ReplaceAsync(run.Id, 1)).Game!;

        Assert.Equal(0, game.Step);
        Assert.Empty(game.GuessedIds);
        Assert.Empty(game.Guessed);
        var stored = await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();
        Assert.Equal(0, stored.Step);
        Assert.Empty(stored.RoundGuesses);
    }

    [Fact]
    public async Task ReplacingMovesTheGameOnSoAStaleGuessIsNotAppliedToTheNewSong()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db, moves: 3);
        var service = Make(db);

        var game = (await service.ReplaceAsync(run.Id, 1)).Game!;

        Assert.Equal(4, game.Move);
        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => service.GuessAsync(run.Id, 3, 2));
        // The move the replace returned is the one to use.
        Assert.Equal(GuessTheThemeOutcome.Skipped, (await service.SkipAsync(run.Id, game.Move)).Outcome);
    }

    [Fact]
    public async Task TheFileThatWentIsAskedAfterAndTheSongThatWentIsNotDrawnAgain()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db);
        var relay = new FakeMediaRelay();

        for (var seed = 0; seed < 25; seed++)
        {
            // Put the game back on the song that has gone, as a fresh game would have it.
            var stored = await db.GuessTheThemeRuns.SingleAsync();
            var round = await db.GuessTheThemeRounds.SingleAsync();
            round.AnimeId = Gone;
            round.SongId = GoneSong;
            round.AudioUrl = $"https://a.animethemes.moe/T{GoneSong}.ogg";
            stored.PlayedSongIds = [GoneSong];
            await db.SaveChangesAsync();

            await Make(db, seed, relay).ReplaceAsync(run.Id, 1);

            Assert.NotEqual(GoneSong, (await db.GuessTheThemeRounds.AsNoTracking().SingleAsync()).SongId);
        }

        Assert.All(relay.Probed, url => Assert.Equal($"https://a.animethemes.moe/T{GoneSong}.ogg", url));
    }

    [Fact]
    public async Task TheSongThatWentStaysOutForTheRestOfTheGame()
    {
        using var db = CreateDb();
        // Two songs of one anime besides the one that has gone, and none besides.
        db.AnimeMetadata.AddRange(Anime(Gone), Anime(2));
        db.AnimeThemes.AddRange(Song(Gone, GoneSong), Song(2, 20), Song(2, 21, sequence: 2));
        await db.SaveChangesAsync();
        var service = Make(db);
        var started = await service.StartAsync(AnyTheme);
        var seen = new List<int>();
        var round = (await RoundsAsync(db)).Single(r => r.Number == 1);
        seen.Add(round.SongId);

        // Whatever came first has "gone": replace, then guess every song until the game ends.
        var game = (await service.ReplaceAsync(started.Id, started.Round)).Game!;
        while (true)
        {
            round = (await RoundsAsync(db)).Single(r => r.Number == game.Round);
            seen.Add(round.SongId);
            var result = await service.GuessAsync(game.Id, game.Move, round.AnimeId);
            if (result.Game is null)
                break;

            game = await service.NextAsync(game.Id, result.Game.Move);
        }

        // The song that went is the first entry, and it shows up nowhere after it.
        Assert.DoesNotContain(seen[0], seen.Skip(1));
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task TheNewSongsAcceptedAnimeReplaceTheOldOnes()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db);
        db.AnimeThemes.Add(Song(88, 20)); // also lists song 20
        db.AnimeThemes.Add(Song(88, 21, AnimeThemeKind.Ending));
        await db.SaveChangesAsync();

        await Make(db).ReplaceAsync(run.Id, 1);

        var round = (await RoundsAsync(db)).Single();
        Assert.Equal([2, 88], round.AcceptedAnimeIds);
    }

    [Fact]
    public async Task TheReplacementFollowsTheSettingsTheGameBeganWith()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db, settings: AnyTheme with { Themes = GameThemeChoice.Endings });
        // The song that has gone was an opening, which a game of Endings would not have drawn;
        // the replacement is an ending all the same.
        var service = Make(db);

        await service.ReplaceAsync(run.Id, 1);

        var round = (await RoundsAsync(db)).Single();
        Assert.Equal(AnimeThemeKind.Ending, round.Kind);
        Assert.Equal(21, round.SongId);
    }

    [Fact]
    public async Task NothingAboutTheNewSongIsInTheResponse()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(Gone), Anime(52390));
        db.AnimeThemes.AddRange(Song(Gone, GoneSong), Song(52390, 88888));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, Gone, GoneSong);

        var result = await Make(db).ReplaceAsync(run.Id, 1);

        var json = JsonSerializer.Serialize(result, Wire);
        Assert.DoesNotContain("52390", json);
        Assert.DoesNotContain("88888", json);
        Assert.DoesNotContain("Anime 52390", json);
        Assert.DoesNotContain("Song", json);
        Assert.DoesNotContain("animethemes", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("41467", json);
    }

    // ---- refused ----

    [Fact]
    public async Task AFileThatIsStillThereIsRefusedAndNothingChanges()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db, step: 2, roundGuesses: [2], moves: 3);
        var relay = new FakeMediaRelay { ProbeResult = AnimeThemesMediaState.Present };

        await Assert.ThrowsAsync<GuessTheThemeFileStillThereException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 1));

        var kept = await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();
        Assert.Equal((2, 3), (kept.Step, kept.Moves));
        Assert.Equal([2], kept.RoundGuesses);
        Assert.Equal([GoneSong], kept.PlayedSongIds);
        Assert.Equal(GoneSong, (await RoundsAsync(db)).Single().SongId);
    }

    [Fact]
    public async Task AnAnswerThatCannotBeToldIsRefusedAndNothingChanges()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db, step: 1, moves: 2);
        var relay = new FakeMediaRelay { ProbeResult = AnimeThemesMediaState.Unreachable };

        await Assert.ThrowsAsync<GuessTheThemeCannotCheckException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 1));

        var kept = await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();
        Assert.Equal((1, 2), (kept.Step, kept.Moves));
        Assert.Equal(GoneSong, (await RoundsAsync(db)).Single().SongId);
    }

    [Fact]
    public async Task ASongThatIsNotTheOneBeingPlayedIsRefusedWithoutAskingTheHost()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db);
        var relay = new FakeMediaRelay();

        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 2));
        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 0));

        Assert.Empty(relay.Probed);
    }

    [Fact]
    public async Task DuringARevealNeitherSongIsReplaced()
    {
        using var db = CreateDb();
        var run = await GameWithASpareAsync(db, phase: GuessTheThemePhase.Revealed);
        AddRound(db, run.Id, 2, animeId: 2, songId: 20);
        await db.SaveChangesAsync();
        var relay = new FakeMediaRelay();

        // The one just revealed needs no clip, and the one drawn for after it is looked at again
        // when it becomes the one being played.
        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 1));
        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db, relay: relay).ReplaceAsync(run.Id, 2));

        Assert.Empty(relay.Probed);
        Assert.Equal(2, (await RoundsAsync(db)).Count);
    }

    [Fact]
    public async Task ReplacingInAGameNoLongerInProgressFindsItGone()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).ReplaceAsync(404, 1));
    }

    [Fact]
    public async Task AnotherTabsMoveLandingDuringTheCheckMakesTheReplaceStale()
    {
        const string name = "replace-while-moving";
        using var tabOne = CreateDb(name);
        var run = await GameWithASpareAsync(tabOne);
        var relay = new FakeMediaRelay
        {
            OnProbe = () =>
            {
                using var tabTwo = CreateDb(name);
                var stored = tabTwo.GuessTheThemeRuns.Single();
                stored.Step = 1;
                stored.Moves = 1;
                tabTwo.SaveChanges();
                return Task.CompletedTask;
            },
        };

        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(tabOne, relay: relay).ReplaceAsync(run.Id, 1));

        using var check = CreateDb(name);
        var kept = await check.GuessTheThemeRuns.AsNoTracking().SingleAsync();
        Assert.Equal((1, 1), (kept.Step, kept.Moves));
        Assert.Equal(GoneSong, (await RoundsAsync(check)).Single().SongId);
    }

    // ---- nothing left to draw ----

    [Fact]
    public async Task WithNoOtherSongTheGameEndsAsAWin()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(Gone));
        db.AnimeThemes.Add(Song(Gone, GoneSong));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, Gone, GoneSong, step: 2);

        var result = await Make(db).ReplaceAsync(run.Id, 1);

        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
        Assert.Null(result.Game);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task ASongOnlyAnotherAnimeShareIsNotADifferentSong()
    {
        // The only other anime's only song is the one that has gone.
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(Gone), Anime(2));
        db.AnimeThemes.AddRange(Song(Gone, GoneSong), Song(2, GoneSong));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, Gone, GoneSong);

        var result = await Make(db).ReplaceAsync(run.Id, 1);

        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
    }

    // ---- the link the relay is handed ----

    [Fact]
    public async Task TheClipSourceIsTheLinkHeldOnTheRound()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();
        var (run, round) = await AddThemeRunAsync(db, 1, 500);

        var source = await Make(db).GetClipSourceAsync(run.Id, 1);

        Assert.Equal(round.AudioUrl, source);
        Assert.StartsWith("https://a.animethemes.moe/", source);
    }

    [Fact]
    public async Task TheSongDrawnForAfterARevealHasAClipToo()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, 1, 500, phase: GuessTheThemePhase.Revealed);
        var next = AddRound(db, run.Id, 2, animeId: 2, songId: 600);
        await db.SaveChangesAsync();

        Assert.Equal(next.AudioUrl, await Make(db).GetClipSourceAsync(run.Id, 2));
    }

    [Fact]
    public async Task ASongTheGameDoesNotHoldHasNoClip()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, 1, 500);

        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db).GetClipSourceAsync(run.Id, 2));
        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => Make(db).GetClipSourceAsync(run.Id, 0));
        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).GetClipSourceAsync(404, 1));
    }

    [Fact]
    public async Task AFinishedSongHasNoClipOnceTheGameMovedOn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, 1, 500, phase: GuessTheThemePhase.Revealed);
        AddRound(db, run.Id, 2, animeId: 2, songId: 600);
        await db.SaveChangesAsync();
        var service = Make(db);

        await service.NextAsync(run.Id, 0);

        await Assert.ThrowsAsync<GuessTheThemeRoundNotFoundException>(() => service.GetClipSourceAsync(run.Id, 1));
        Assert.NotNull(await service.GetClipSourceAsync(run.Id, 2));
    }
}
