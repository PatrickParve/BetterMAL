using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeServiceTests;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-anime-themes-and-guess-the-theme tasks 6.4 to 6.7: guessing, skipping, Next song and End
// game on InMemory, and what every way a game can end leaves behind. Reads and Start are in
// GuessTheThemeServiceTests.
public class GuessTheThemeServiceMovesTests
{
    // Unusual ids, so a response that names either one can be found by looking for the number.
    private const int Current = 41467;
    private const int Next = 52390;
    private const int CurrentSong = 77777;
    private const int NextSong = 88888;

    private static async Task<GuessTheThemeRun> StoredRunAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();

    private static async Task<List<GuessTheThemeRound>> RoundsAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRounds.AsNoTracking().OrderBy(r => r.Number).ToListAsync();

    /// <summary>Two anime with one song each, and a game on the first at <paramref name="step"/>. With
    /// the first song played, the only song left to draw is the other's.</summary>
    private static async Task<GuessTheThemeRun> TwoSongGameAsync(
        AnimeTrackerDbContext db, int step = 0, int[]? roundGuesses = null, int moves = 0, int[]? accepted = null,
        GuessTheThemePhase phase = GuessTheThemePhase.Listening)
    {
        db.AnimeMetadata.AddRange(Anime(Current), Anime(Next));
        db.AnimeThemes.AddRange(Song(Current, CurrentSong), Song(Next, NextSong));
        await db.SaveChangesAsync();
        var (run, round) = await AddThemeRunAsync(
            db, Current, CurrentSong, step: step, roundGuesses: roundGuesses, moves: moves, accepted: accepted, phase: phase);
        if (phase == GuessTheThemePhase.Revealed)
        {
            AddRound(db, run.Id, 2, Next, NextSong);
            run.PlayedSongIds = [CurrentSong, NextSong];
            await db.SaveChangesAsync();
        }

        return run;
    }

    // ---- a wrong guess (6.4) ----

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task AWrongGuessBelowTheLastLengthMovesUpTheLadder(int step)
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: step, moves: 3);

        var result = await Make(db).GuessAsync(run.Id, 3, Next);

        Assert.Equal(GuessTheThemeOutcome.Wrong, result.Outcome);
        Assert.Null(result.Reveal);
        var game = result.Game!;
        Assert.Equal(step + 1, game.Step);
        Assert.Equal(4, game.Move);
        Assert.Equal([Next], game.GuessedIds);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        var kept = await StoredRunAsync(db);
        Assert.Equal((step + 1, 4), (kept.Step, kept.Moves));
        Assert.Equal([Next], kept.RoundGuesses);
    }

    [Fact]
    public async Task AWrongGuessAddsToThoseAlreadyMadeInOrder()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(9));
        var run = await TwoSongGameAsync(db, step: 2, roundGuesses: [9], moves: 2);

        var game = (await Make(db).GuessAsync(run.Id, 2, Next)).Game!;

        Assert.Equal([9, Next], game.GuessedIds);
        Assert.Equal([9, Next], game.Guessed.Select(c => c.AnimeId));
    }

    [Fact]
    public async Task AWrongGuessOfAnAnimeNotStoredCountsByItsIdAndFetchesNothing()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db);

        var result = await Make(db).GuessAsync(run.Id, 0, 999_999);

        Assert.Equal(GuessTheThemeOutcome.Wrong, result.Outcome);
        Assert.Equal([999_999], result.Game!.GuessedIds);
        Assert.Empty(result.Game.Guessed);
        Assert.Equal([999_999], (await StoredRunAsync(db)).RoundGuesses);
        Assert.Null(await db.AnimeMetadata.FindAsync(999_999));
    }

    [Fact]
    public async Task AWrongGuessAtTheLastLengthLosesTheGameAndShowsTheAnswer()
    {
        using var db = CreateDb();
        db.UserAnimeEntries.Add(OnList(Current));
        var run = await TwoSongGameAsync(db, step: 5, moves: 6);

        var result = await Make(db).GuessAsync(run.Id, 6, Next);

        Assert.Equal(GuessTheThemeOutcome.Lost, result.Outcome);
        Assert.Null(result.Game);
        var reveal = result.Reveal!;
        Assert.Equal(Current, reveal.AnimeId);
        Assert.Equal("Anime 41467", reveal.Title);
        Assert.Equal("Song 77777", reveal.SongTitle);
        Assert.Equal("OP1", reveal.Label);
        Assert.True(reveal.InMyList);
        // The song was not got, so no length is claimed.
        Assert.Null(reveal.HeardSeconds);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    // ---- a right guess ----

    [Theory]
    [InlineData(0, 0.1)]
    [InlineData(1, 0.5)]
    [InlineData(2, 1.0)]
    [InlineData(3, 3.0)]
    [InlineData(4, 5.0)]
    [InlineData(5, 10.0)]
    public async Task ARightGuessAtAnyLengthRevealsTheSongAndTheGameCarriesOn(int step, double seconds)
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: step);

        var result = await Make(db).GuessAsync(run.Id, 0, Current);

        Assert.Equal(GuessTheThemeOutcome.Right, result.Outcome);
        var game = result.Game!;
        Assert.Equal(GuessTheThemePhase.Revealed, game.Phase);
        Assert.Equal(step, game.Step);
        Assert.Equal(seconds, game.Reveal!.HeardSeconds);
        Assert.Equal(Current, game.Reveal.AnimeId);
        Assert.Equal(2, game.NextRound);
        Assert.Equal(1, game.Move);
        var stored = await StoredRunAsync(db);
        Assert.Equal(GuessTheThemePhase.Revealed, stored.Phase);
        Assert.Equal(1, stored.Round);
    }

    [Fact]
    public async Task ARightGuessDrawsTheNextSongInTheSameStep()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db);

        await Make(db).GuessAsync(run.Id, 0, Current);

        var rounds = await RoundsAsync(db);
        Assert.Equal([1, 2], rounds.Select(r => r.Number));
        Assert.Equal(Next, rounds[1].AnimeId);
        Assert.Equal(NextSong, rounds[1].SongId);
        Assert.Equal([CurrentSong, NextSong], (await StoredRunAsync(db)).PlayedSongIds);
    }

    [Fact]
    public async Task NothingAboutTheNextSongIsInTheResponse()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db);

        var result = await Make(db).GuessAsync(run.Id, 0, Current);

        var json = JsonSerializer.Serialize(result, Wire);
        var next = await db.AnimeMetadata.FindAsync(Next);
        Assert.DoesNotContain(Next.ToString(), json);
        Assert.DoesNotContain(NextSong.ToString(), json);
        Assert.DoesNotContain(next!.Title, json);
        Assert.DoesNotContain(next.PictureUrl!, json);
        Assert.DoesNotContain("T88888", json);
        // Only that it exists, by its number.
        Assert.Equal(2, result.Game!.NextRound);
    }

    [Fact]
    public async Task ARightGuessDoesNotDisturbTheGuessesOfTheRound()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(9));
        var run = await TwoSongGameAsync(db, step: 2, roundGuesses: [9]);

        var game = (await Make(db).GuessAsync(run.Id, 0, Current)).Game!;

        Assert.Equal([9], game.GuessedIds);
    }

    [Fact]
    public async Task AGuessAtASharedSongCountsForAnotherAnimeThatHasIt()
    {
        using var db = CreateDb();
        // 9 isn't stored: the catalogue lists it with the same song.
        var run = await TwoSongGameAsync(db, accepted: [Current, 9]);

        var result = await Make(db).GuessAsync(run.Id, 0, 9);

        Assert.Equal(GuessTheThemeOutcome.Right, result.Outcome);
        // The reveal shows the anime that was drawn.
        Assert.Equal(Current, result.Game!.Reveal!.AnimeId);
    }

    [Fact]
    public async Task AGuessAtAnotherSeasonOfTheAnimeWithADifferentSongIsWrong()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, accepted: [Current]);

        var result = await Make(db).GuessAsync(run.Id, 0, Next);

        Assert.Equal(GuessTheThemeOutcome.Wrong, result.Outcome);
    }

    [Fact]
    public async Task ARightGuessWithNoSongLeftClearsTheGameAndShowsTheLastSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(Current));
        db.AnimeThemes.Add(Song(Current, CurrentSong));
        await db.SaveChangesAsync();
        var (run, _) = await AddThemeRunAsync(db, Current, CurrentSong, step: 3);

        var result = await Make(db).GuessAsync(run.Id, 0, Current);

        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
        Assert.Null(result.Game);
        Assert.Equal(Current, result.Reveal!.AnimeId);
        Assert.Equal(3.0, result.Reveal.HeardSeconds);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task ASmallPoolIsClearedOnTheThirdSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(1, 11, sequence: 2), Song(2, 20));
        await db.SaveChangesAsync();
        var service = Make(db);

        var game = await service.StartAsync(AnyTheme);
        var outcomes = new List<GuessTheThemeOutcome>();
        for (var song = 1; song <= 3; song++)
        {
            var answer = (await RoundsAsync(db)).Single(r => r.Number == game.Round).AnimeId;
            var result = await service.GuessAsync(game.Id, game.Move, answer);
            outcomes.Add(result.Outcome);
            if (result.Game is null)
                break;

            game = await service.NextAsync(game.Id, result.Game.Move);
        }

        Assert.Equal([GuessTheThemeOutcome.Right, GuessTheThemeOutcome.Right, GuessTheThemeOutcome.Cleared], outcomes);
        Assert.Empty(db.GuessTheThemeRuns);
    }

    // ---- skip ----

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public async Task SkipMovesUpTheLadderWithNoGuess(int step)
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: step, moves: 7);

        var result = await Make(db).SkipAsync(run.Id, 7);

        Assert.Equal(GuessTheThemeOutcome.Skipped, result.Outcome);
        Assert.Equal(step + 1, result.Game!.Step);
        Assert.Equal(8, result.Game.Move);
        Assert.Empty(result.Game.GuessedIds);
        Assert.Empty((await StoredRunAsync(db)).RoundGuesses);
    }

    [Fact]
    public async Task SkipAtTheLastLengthLosesTheGame()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: 5);

        var result = await Make(db).SkipAsync(run.Id, 0);

        Assert.Equal(GuessTheThemeOutcome.Lost, result.Outcome);
        Assert.Null(result.Game);
        Assert.Equal(Current, result.Reveal!.AnimeId);
        Assert.Null(result.Reveal.HeardSeconds);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    // ---- what a move is refused for ----

    [Fact]
    public async Task AGuessInTheRevealedPhaseIsRefused()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, phase: GuessTheThemePhase.Revealed);

        await Assert.ThrowsAsync<GuessTheThemeWrongPhaseException>(() => Make(db).GuessAsync(run.Id, 0, Next));
        await Assert.ThrowsAsync<GuessTheThemeWrongPhaseException>(() => Make(db).SkipAsync(run.Id, 0));

        Assert.Equal(GuessTheThemePhase.Revealed, (await StoredRunAsync(db)).Phase);
    }

    [Fact]
    public async Task AnAnimeAlreadyGuessedThisRoundIsRefusedAndNothingChanges()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(9));
        var run = await TwoSongGameAsync(db, step: 1, roundGuesses: [9], moves: 1);

        await Assert.ThrowsAsync<GuessTheThemeAlreadyGuessedException>(() => Make(db).GuessAsync(run.Id, 1, 9));

        var kept = await StoredRunAsync(db);
        Assert.Equal((1, 1), (kept.Step, kept.Moves));
    }

    [Fact]
    public async Task AnAnimeFromAnEarlierRoundCanBeGuessedAgain()
    {
        // Rounds are separate: a Next song clears the list.
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, phase: GuessTheThemePhase.Revealed);
        var service = Make(db);
        var game = await service.NextAsync(run.Id, 0);

        var result = await service.GuessAsync(run.Id, game.Move, Current);

        Assert.Equal(GuessTheThemeOutcome.Wrong, result.Outcome);
    }

    [Fact]
    public async Task AStaleMoveIsRefusedAndNothingChanges()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: 2, moves: 4);

        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(db).GuessAsync(run.Id, 3, Next));
        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(db).SkipAsync(run.Id, 5));

        var kept = await StoredRunAsync(db);
        Assert.Equal((2, 4), (kept.Step, kept.Moves));
        Assert.Empty(kept.RoundGuesses);
    }

    [Fact]
    public async Task ATabBehindTheGameIsToldItIsBehindNotThatItsGuessIsRepeated()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: 1, roundGuesses: [Next], moves: 1);

        // The move is old, and the anime was guessed since: the move is what is wrong.
        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(db).GuessAsync(run.Id, 0, Next));
    }

    [Fact]
    public async Task APressedTwiceCountsOnce()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db);
        var service = Make(db);

        await service.SkipAsync(run.Id, 0);
        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => service.SkipAsync(run.Id, 0));

        Assert.Equal(1, (await StoredRunAsync(db)).Step);
    }

    [Fact]
    public async Task TwoConcurrentMovesFailOnTheConcurrencyToken()
    {
        const string name = "concurrent-theme-move";
        using var tabOne = CreateDb(name);
        var run = await TwoSongGameAsync(tabOne);
        // Tab one has read the game at move 0 when tab two's skip lands, just before tab one saves.
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheThemeRuns.Single();
            stored.Step = 1;
            stored.Moves = 1;
            tabTwo.SaveChanges();
        };

        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(tabOne).SkipAsync(run.Id, 0));

        using var check = CreateDb(name);
        var kept = await StoredRunAsync(check);
        Assert.Equal((1, 1), (kept.Step, kept.Moves));
    }

    /// <summary>Fails the next save with a plain <see cref="DbUpdateException"/> that is not a
    /// concurrency conflict, the way Postgres answers an insert whose key the other press just
    /// took (found on a real Npgsql database on 11/1: InMemory raises a concurrency conflict
    /// instead). Runs <paramref name="beforeFailing"/> first, which is the other press landing.</summary>
    private sealed class KeyClash(Action? beforeFailing) : SaveChangesInterceptor
    {
        private bool _done;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (_done)
                return ValueTask.FromResult(result);
            _done = true;
            beforeFailing?.Invoke();
            throw new DbUpdateException("duplicate key value violates unique constraint \"PK_GuessTheThemeRounds\"");
        }
    }

    private static AnimeTrackerDbContext CreateDbWith(string name, SaveChangesInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseInMemoryDatabase(name).AddInterceptors(interceptor).Options);

    [Fact]
    public async Task TwoNextPressesThatInsertTheSameRoundLeaveTheSlowerOneStaleNotFailed()
    {
        // Two presses of Next song at the same move each add the next round. On Postgres the second
        // insert meets the round's key before the move token is checked, which was a 500.
        const string name = "next-key-clash";
        using var seed = CreateDb(name);
        var run = await TwoSongGameAsync(seed, phase: GuessTheThemePhase.Revealed);
        using var slower = CreateDbWith(name, new KeyClash(() =>
        {
            using var faster = CreateDb(name);
            var stored = faster.GuessTheThemeRuns.Single();
            stored.Round = 2;
            stored.Moves = 1;
            faster.SaveChanges();
        }));

        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(slower).NextAsync(run.Id, 0));

        using var check = CreateDb(name);
        Assert.Equal((2, 1), ((await StoredRunAsync(check)).Round, (await StoredRunAsync(check)).Moves));
    }

    [Fact]
    public async Task AFailedSaveThatIsNotALostRaceIsStillAFailure()
    {
        const string name = "next-real-failure";
        using var seed = CreateDb(name);
        var run = await TwoSongGameAsync(seed, phase: GuessTheThemePhase.Revealed);
        using var tab = CreateDbWith(name, new KeyClash(null));

        await Assert.ThrowsAsync<DbUpdateException>(() => Make(tab).NextAsync(run.Id, 0));
    }

    [Fact]
    public async Task AMoveOnAGameNoLongerInProgressFindsItGone()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).GuessAsync(404, 0, 1));
        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).SkipAsync(404, 0));
        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).NextAsync(404, 0));
        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(db).EndAsync(404));
    }

    [Fact]
    public async Task AMoveAfterAnotherTabEndedTheGameFindsItGone()
    {
        const string name = "move-after-end";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        var run = await TwoSongGameAsync(tabOne);
        await Make(tabTwo).EndAsync(run.Id);

        await Assert.ThrowsAsync<GuessTheThemeRunNotFoundException>(() => Make(tabOne).SkipAsync(run.Id, 0));
    }

    // ---- Next song (6.5) ----

    [Fact]
    public async Task NextMovesToTheSongDrawnAndRemovesTheFinishedOne()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: 3, roundGuesses: [], moves: 1, phase: GuessTheThemePhase.Revealed);

        var game = await Make(db).NextAsync(run.Id, 1);

        Assert.Equal(2, game.Round);
        Assert.Equal(0, game.Step);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Null(game.Reveal);
        Assert.Null(game.NextRound);
        Assert.Empty(game.GuessedIds);
        Assert.Equal(2, game.Move);
        var rounds = await RoundsAsync(db);
        Assert.Equal([2], rounds.Select(r => r.Number));
        Assert.Equal(Next, rounds.Single().AnimeId);
    }

    [Fact]
    public async Task NextClearsTheGuessesOfTheRoundBefore()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(9));
        var run = await TwoSongGameAsync(db, step: 2, roundGuesses: [9], phase: GuessTheThemePhase.Revealed);

        await Make(db).NextAsync(run.Id, 0);

        Assert.Empty((await StoredRunAsync(db)).RoundGuesses);
    }

    [Fact]
    public async Task NextDuringARoundIsRefused()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db);

        await Assert.ThrowsAsync<GuessTheThemeWrongPhaseException>(() => Make(db).NextAsync(run.Id, 0));

        Assert.Single(await RoundsAsync(db));
    }

    [Fact]
    public async Task NextWithAStaleMoveIsRefused()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, moves: 2, phase: GuessTheThemePhase.Revealed);

        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => Make(db).NextAsync(run.Id, 1));

        Assert.Equal(1, (await StoredRunAsync(db)).Round);
    }

    [Fact]
    public async Task NextPressedTwiceMovesOnce()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, phase: GuessTheThemePhase.Revealed);
        var service = Make(db);

        await service.NextAsync(run.Id, 0);
        await Assert.ThrowsAsync<GuessTheThemeStaleMoveException>(() => service.NextAsync(run.Id, 0));

        Assert.Equal(2, (await StoredRunAsync(db)).Round);
    }

    [Fact]
    public async Task NextWithNoSongDrawnIsRefused()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, phase: GuessTheThemePhase.Revealed);
        db.GuessTheThemeRounds.Remove(await db.GuessTheThemeRounds.SingleAsync(r => r.Number == 2));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<GuessTheThemeWrongPhaseException>(() => Make(db).NextAsync(run.Id, 0));
    }

    // ---- End game (6.6) ----

    [Fact]
    public async Task EndGameDuringARoundAnswersWithTheSong()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, step: 2);

        var end = await Make(db).EndAsync(run.Id);

        Assert.Equal(Current, end.Reveal!.AnimeId);
        Assert.Equal("Song 77777", end.Reveal.SongTitle);
        Assert.Null(end.Reveal.HeardSeconds);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task EndGameDuringARevealAnswersWithNothingAndLeavesNoNextSong()
    {
        using var db = CreateDb();
        var run = await TwoSongGameAsync(db, phase: GuessTheThemePhase.Revealed);

        var end = await Make(db).EndAsync(run.Id);

        Assert.Null(end.Reveal);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task EndGameSaysWhetherTheSongIsOnMyList()
    {
        using var db = CreateDb();
        db.UserAnimeEntries.Add(OnList(Current));
        var run = await TwoSongGameAsync(db);

        Assert.True((await Make(db).EndAsync(run.Id)).Reveal!.InMyList);
    }

    [Fact]
    public async Task EndGameRetriesOnceWhenAMoveLandedBetweenTheReadAndTheDelete()
    {
        const string name = "end-after-theme-move";
        using var tabOne = CreateDb(name);
        var run = await TwoSongGameAsync(tabOne);
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheThemeRuns.Single();
            stored.Step = 1;
            stored.Moves = 1;
            tabTwo.SaveChanges();
        };

        var end = await Make(tabOne).EndAsync(run.Id);

        // The game is still in a round, so it is still answered.
        Assert.Equal(Current, end.Reveal!.AnimeId);
        using var check = CreateDb(name);
        Assert.Empty(check.GuessTheThemeRuns);
        Assert.Empty(check.GuessTheThemeRounds);
    }

    [Fact]
    public async Task EndGameThatMeetsARightGuessInBetweenEndsTheRevealWithoutAnswering()
    {
        const string name = "end-after-theme-right";
        using var tabOne = CreateDb(name);
        var run = await TwoSongGameAsync(tabOne);
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheThemeRuns.Single();
            stored.Phase = GuessTheThemePhase.Revealed;
            stored.Moves = 1;
            tabTwo.GuessTheThemeRounds.Add(new GuessTheThemeRound
            {
                RunId = stored.Id, Number = 2, AnimeId = Next, ThemeId = 1, SongId = NextSong,
                AudioUrl = "https://a.animethemes.moe/x.ogg", VideoUrl = "https://v.animethemes.moe/x.webm",
            });
            tabTwo.SaveChanges();
        };

        var end = await Make(tabOne).EndAsync(run.Id);

        // By the time the delete went through the song had been got, so it is on screen already.
        Assert.Null(end.Reveal);
        using var check = CreateDb(name);
        Assert.Empty(check.GuessTheThemeRuns);
        Assert.Empty(check.GuessTheThemeRounds);
    }

    // ---- every ending leaves nothing behind (6.7) ----

    [Theory]
    [InlineData("wrong at the last length")]
    [InlineData("skip at the last length")]
    [InlineData("cleared")]
    [InlineData("end during a round")]
    [InlineData("end during a reveal")]
    public async Task NoRunOrRoundRowOutlivesAnyEnding(string ending)
    {
        using var db = CreateDb();
        var phase = ending == "end during a reveal" ? GuessTheThemePhase.Revealed : GuessTheThemePhase.Listening;
        var run = await TwoSongGameAsync(db, step: ending.Contains("last") ? 5 : 1, phase: phase);
        if (ending == "cleared")
        {
            // Nothing else to draw.
            db.AnimeThemes.Remove(await db.AnimeThemes.SingleAsync(t => t.AnimeId == Next));
            await db.SaveChangesAsync();
        }

        var service = Make(db);
        switch (ending)
        {
            case "wrong at the last length":
                await service.GuessAsync(run.Id, 0, Next);
                break;
            case "skip at the last length":
                await service.SkipAsync(run.Id, 0);
                break;
            case "cleared":
                Assert.Equal(GuessTheThemeOutcome.Cleared, (await service.GuessAsync(run.Id, 0, Current)).Outcome);
                break;
            default:
                await service.EndAsync(run.Id);
                break;
        }

        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task NothingIsKeptAboutAFinishedGameAnywhere()
    {
        // The game keeps no score: after a whole game the only game tables are empty, and no
        // table of this feature has gained a row but the catalogue's own.
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var answer = (await RoundsAsync(db)).Single(r => r.Number == 1).AnimeId;
        var right = await service.GuessAsync(game.Id, game.Move, answer);
        game = await service.NextAsync(game.Id, right.Game!.Move);
        await service.EndAsync(game.Id);

        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
        Assert.Empty(db.HigherOrLowerRuns);
        Assert.Empty(db.GuessTheAnimeRuns);
        Assert.Equal(2, await db.AnimeThemes.CountAsync());
    }

    [Fact]
    public async Task TheSongIsUnchangedWhenACatalogueRefreshRemovesItMidGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var drawn = (await RoundsAsync(db)).Single(r => r.Number == 1);

        // A weekly refresh lands and the catalogue no longer lists either song.
        db.AnimeThemes.RemoveRange(db.AnimeThemes);
        await db.SaveChangesAsync();
        var result = await service.GuessAsync(game.Id, game.Move, drawn.AnimeId);

        // The song is the one that was drawn, and the right guess is still right; the song for
        // after it was drawn at the start, so the refresh doesn't touch it either.
        Assert.Equal(GuessTheThemeOutcome.Right, result.Outcome);
        Assert.Equal(drawn.SongTitle, result.Game!.Reveal!.SongTitle);
        Assert.Equal(drawn.Label, result.Game.Reveal.Label);
        Assert.Equal(drawn.VideoUrl, result.Game.Reveal.VideoUrl);
        Assert.Equal(2, result.Game.NextRound);
    }

    [Fact]
    public async Task ARightGuessIsStillRightWhenARefreshLeavesNothingMoreToDraw()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        db.AnimeThemes.Add(Song(1, 10));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var drawn = (await RoundsAsync(db)).Single();

        db.AnimeThemes.RemoveRange(db.AnimeThemes);
        await db.SaveChangesAsync();
        var result = await service.GuessAsync(game.Id, game.Move, drawn.AnimeId);

        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
        Assert.Equal(drawn.SongTitle, result.Reveal!.SongTitle);
        Assert.Equal(drawn.Label, result.Reveal.Label);
        Assert.Equal(drawn.VideoUrl, result.Reveal.VideoUrl);
    }

    [Fact]
    public async Task ARoundIsStillRightAfterARefreshChangedWhoSharesTheSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 10), Song(1, 11, sequence: 2));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme with { Themes = GameThemeChoice.Openings });
        var drawn = (await RoundsAsync(db)).Single(r => r.Number == 1);
        var accepted = drawn.AcceptedAnimeIds.ToList();

        db.AnimeThemes.RemoveRange(db.AnimeThemes.Where(t => t.AnimeId == 2));
        await db.SaveChangesAsync();

        // Judged against the draw, not against what the catalogue says now.
        Assert.Equal(accepted, (await RoundsAsync(db)).Single(r => r.Number == 1).AcceptedAnimeIds);
        _ = game;
    }

    // ---- a game keeps its settings ----

    [Fact]
    public async Task AGameThatBeganWithHideNsfwOnNeverDrawsAnNsfwSong()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, nsfw: true));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme with { HideHentai = true });

        var result = await service.GuessAsync(game.Id, game.Move, 1);

        // With NSFW hidden there was only one song, so the game is cleared on it.
        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
    }

    [Fact]
    public async Task AGameThatBeganWithHideNsfwOffMayDrawOne()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, nsfw: true));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme with { HideHentai = false });
        var first = (await RoundsAsync(db)).Single(r => r.Number == 1).AnimeId;

        var result = await service.GuessAsync(game.Id, game.Move, first);

        Assert.Equal(GuessTheThemeOutcome.Right, result.Outcome);
        Assert.Equal(2, (await RoundsAsync(db)).Count);
    }

    [Fact]
    public async Task AGameKeepsItsThemeSetting()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2));
        db.AnimeThemes.AddRange(Song(1, 10), Song(2, 20, AnimeThemeKind.Ending), Song(2, 21, sequence: 2));
        await db.SaveChangesAsync();
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme with { Themes = GameThemeChoice.Endings });

        var seen = new List<AnimeThemeKind>();
        while (true)
        {
            var round = (await RoundsAsync(db)).Single(r => r.Number == game.Round);
            seen.Add(round.Kind);
            var result = await service.GuessAsync(game.Id, game.Move, round.AnimeId);
            if (result.Game is null)
                break;

            game = await service.NextAsync(game.Id, result.Game.Move);
        }

        Assert.All(seen, kind => Assert.Equal(AnimeThemeKind.Ending, kind));
    }

    // ---- nothing is asked of MyAnimeList or AniList ----

    [Fact]
    public void TheServiceCannotReachMyAnimeListOrAniList()
    {
        // Structure, not behaviour: every collaborator the service is built from is a local
        // one. The other half of the proof is the refusing proxy in the whole-app check.
        var constructor = typeof(GuessTheThemeService).GetConstructors().Single();
        var dependencies = constructor.GetParameters().Select(p => p.ParameterType).ToList();

        // The relay is AnimeThemes' media hosts and nothing else, the hold reads through the relay
        // and nothing else, and the song kept ready is in memory.
        Assert.Equal(
            [
                typeof(AnimeTrackerDbContext), typeof(GuessTheThemeDraw), typeof(GameLobbyReads), typeof(IAnimeThemesMediaRelay),
                typeof(IAnimeThemesMediaHold), typeof(GuessTheThemeOnDeck),
            ],
            dependencies);
        Assert.Equal(
            [typeof(IAnimeThemesMediaRelay), typeof(TimeProvider)],
            typeof(AnimeThemesMediaHold).GetConstructors().Single().GetParameters().Select(p => p.ParameterType));
        Assert.Empty(typeof(GuessTheThemeDraw).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType)
            .Except([typeof(AnimeTrackerDbContext), typeof(Random)]));
        Assert.Empty(typeof(GameLobbyReads).GetConstructors().Single().GetParameters()
            .Select(p => p.ParameterType)
            .Except([typeof(AnimeTrackerDbContext), typeof(Random)]));
    }

    [Fact]
    public async Task AWholeGameIsPlayedWithNothingButTheDatabase()
    {
        using var db = CreateDb();
        for (var anime = 1; anime <= 6; anime++)
        {
            db.AnimeMetadata.Add(Anime(anime));
            db.AnimeThemes.Add(Song(anime, anime * 10));
        }

        await db.SaveChangesAsync();
        var service = Make(db);

        var game = await service.StartAsync(AnyTheme);
        var songs = 0;
        while (true)
        {
            songs++;
            var round = (await RoundsAsync(db)).Single(r => r.Number == game.Round);
            // A couple of wrong ones, then the right answer.
            var wrong = await service.GuessAsync(game.Id, game.Move, 999);
            var skipped = await service.SkipAsync(game.Id, wrong.Game!.Move);
            var right = await service.GuessAsync(game.Id, skipped.Game!.Move, round.AnimeId);
            if (right.Game is null)
                break;

            game = await service.NextAsync(game.Id, right.Game.Move);
        }

        Assert.Equal(6, songs);
        Assert.Empty(db.GuessTheThemeRuns);
    }
}
