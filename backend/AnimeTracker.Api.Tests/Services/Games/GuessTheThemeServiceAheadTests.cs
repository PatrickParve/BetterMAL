using System.Text.Json;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeServiceTests;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheThemeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// The song after the one being played is drawn when a round starts, so the board has the whole
// round to load its audio from a media host that can be slow to give it. These are the draws
// that makes at Start and at Next song, and what a right guess, a replacement and the end of a
// game do with it.
public class GuessTheThemeServiceAheadTests
{
    private static async Task<List<GuessTheThemeRound>> RoundsAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRounds.AsNoTracking().OrderBy(r => r.Number).ToListAsync();

    private static async Task<GuessTheThemeRun> StoredRunAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheThemeRuns.AsNoTracking().SingleAsync();

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

    // ---- Start ----

    [Fact]
    public async Task AGameStartsWithTheNextSongAlreadyDrawn()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);

        var game = await Make(db).StartAsync(AnyTheme);

        var rounds = await RoundsAsync(db);
        Assert.Equal([1, 2], rounds.Select(r => r.Number));
        Assert.NotEqual(rounds[0].SongId, rounds[1].SongId);
        Assert.Equal(2, game.NextRound);
        Assert.Equal(1, game.Round);
        Assert.Equal(GuessTheThemePhase.Listening, game.Phase);
        Assert.Equal([rounds[0].SongId, rounds[1].SongId], (await StoredRunAsync(db)).PlayedSongIds);
    }

    [Fact]
    public async Task AGameWithOneSongInItsPoolHasNoNextSong()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);

        var game = await Make(db).StartAsync(AnyTheme);

        Assert.Single(await RoundsAsync(db));
        Assert.Null(game.NextRound);
    }

    [Fact]
    public async Task NothingAboutTheNextSongIsInTheGameBeforeItsTurn()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);

        var game = await service.StartAsync(AnyTheme);
        var state = await service.GetStateAsync(false);

        var ahead = (await RoundsAsync(db)).Single(r => r.Number == 2);
        var current = (await RoundsAsync(db)).Single(r => r.Number == 1);
        foreach (var text in new[] { JsonSerializer.Serialize(game), JsonSerializer.Serialize(state.Game) })
        {
            Assert.DoesNotContain(ahead.SongTitle!, text);
            Assert.DoesNotContain(ahead.AudioUrl, text);
            Assert.DoesNotContain(ahead.VideoUrl, text);
            Assert.DoesNotContain(current.SongTitle!, text);
            Assert.DoesNotContain(current.AudioUrl, text);
        }
    }

    [Fact]
    public async Task TheNextSongsAudioCanBeAskedForWhileTheRoundIsPlayed()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);

        var source = await service.GetClipSourceAsync(game.Id, game.NextRound!.Value);

        Assert.Equal((await RoundsAsync(db)).Single(r => r.Number == 2).AudioUrl, source);
    }

    [Fact]
    public async Task ThePostersOfBothDrawnSongsStayOutOfTheGamesTile()
    {
        using var db = CreateDb();
        await SeedAsync(db, 4);
        var service = Make(db);
        await service.StartAsync(AnyTheme);

        var state = await service.GetStateAsync(false);

        var drawn = (await RoundsAsync(db)).Select(r => $"https://img/{r.AnimeId}.jpg").ToList();
        Assert.Equal(2, drawn.Count);
        Assert.NotEmpty(state.Pictures);
        Assert.Empty(state.Pictures.Intersect(drawn));
    }

    // ---- a right guess ----

    [Fact]
    public async Task ARightGuessDrawsNothingWhenTheNextSongIsThere()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var before = await StoredRunAsync(db);
        var answer = (await RoundsAsync(db)).Single(r => r.Number == 1).AnimeId;

        var result = await service.GuessAsync(game.Id, game.Move, answer);

        Assert.Equal(GuessTheThemeOutcome.Right, result.Outcome);
        Assert.Equal(GuessTheThemePhase.Revealed, result.Game!.Phase);
        Assert.Equal(2, result.Game.NextRound);
        Assert.Equal(2, (await RoundsAsync(db)).Count);
        Assert.Equal(before.PlayedSongIds, (await StoredRunAsync(db)).PlayedSongIds);
    }

    // ---- Next song ----

    [Fact]
    public async Task NextSongDrawsTheOneAfterTheSongItMovesTo()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var right = await service.GuessAsync(game.Id, game.Move, (await RoundsAsync(db)).Single(r => r.Number == 1).AnimeId);

        var next = await service.NextAsync(game.Id, right.Game!.Move);

        var rounds = await RoundsAsync(db);
        Assert.Equal([2, 3], rounds.Select(r => r.Number));
        Assert.Equal(2, next.Round);
        Assert.Equal(3, next.NextRound);
        var played = (await StoredRunAsync(db)).PlayedSongIds;
        Assert.Equal(3, played.Count);
        Assert.Equal(3, played.Distinct().Count());
    }

    [Fact]
    public async Task NextSongWithNothingLeftToDrawLeavesNoNextSongAndTheLastRightGuessClears()
    {
        using var db = CreateDb();
        await SeedAsync(db, 2);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var right = await service.GuessAsync(game.Id, game.Move, (await RoundsAsync(db)).Single(r => r.Number == 1).AnimeId);

        var next = await service.NextAsync(game.Id, right.Game!.Move);

        Assert.Null(next.NextRound);
        Assert.Single(await RoundsAsync(db));
        var last = await service.GuessAsync(next.Id, next.Move, (await RoundsAsync(db)).Single().AnimeId);
        Assert.Equal(GuessTheThemeOutcome.Cleared, last.Outcome);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    // ---- a song whose file has gone ----

    [Fact]
    public async Task ReplacingTheSongLeavesTheOneDrawnAfterIt()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var before = await RoundsAsync(db);

        var result = await service.ReplaceAsync(game.Id, game.Round);

        var after = await RoundsAsync(db);
        Assert.Equal([1, 2], after.Select(r => r.Number));
        Assert.Equal(before[1].SongId, after[1].SongId);
        Assert.NotEqual(before[0].SongId, after[0].SongId);
        Assert.NotEqual(after[1].SongId, after[0].SongId);
        Assert.Equal(2, result.Game!.NextRound);
        Assert.Equal(3, (await StoredRunAsync(db)).PlayedSongIds.Count);
    }

    [Fact]
    public async Task ReplacingWithNothingElseToDrawTakesTheNextSongInItsPlace()
    {
        using var db = CreateDb();
        await SeedAsync(db, 2);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);
        var before = await RoundsAsync(db);

        var result = await service.ReplaceAsync(game.Id, game.Round);

        // Not a cleared game: a song is left, and it is now the one being played.
        Assert.Null(result.Outcome);
        var round = Assert.Single(await RoundsAsync(db));
        Assert.Equal(1, round.Number);
        Assert.Equal(before[1].SongId, round.SongId);
        Assert.Equal(before[1].AnimeId, round.AnimeId);
        Assert.Equal(before[1].AudioUrl, round.AudioUrl);
        Assert.Null(result.Game!.NextRound);
        Assert.Equal(game.Move + 1, result.Game.Move);
        Assert.Equal(2, (await StoredRunAsync(db)).PlayedSongIds.Count);

        // And the game goes on to its end on that song.
        var last = await service.GuessAsync(game.Id, result.Game.Move, round.AnimeId);
        Assert.Equal(GuessTheThemeOutcome.Cleared, last.Outcome);
    }

    [Fact]
    public async Task ReplacingTheOnlySongClearsTheGame()
    {
        using var db = CreateDb();
        await SeedAsync(db, 1);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);

        var result = await service.ReplaceAsync(game.Id, game.Round);

        Assert.Equal(GuessTheThemeOutcome.Cleared, result.Outcome);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    // ---- the game ending ----

    [Fact]
    public async Task ALostGameLeavesNoSongDrawnAheadBehind()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);

        GuessTheThemeMoveDto result = null!;
        for (var i = 0; i < GuessTheThemeLadder.Seconds.Count; i++)
        {
            result = await service.SkipAsync(game.Id, game.Move);
            if (result.Game is not null)
                game = result.Game;
        }

        Assert.Equal(GuessTheThemeOutcome.Lost, result.Outcome);
        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task EndingAGameLeavesNoSongDrawnAheadBehind()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);
        var service = Make(db);
        var game = await service.StartAsync(AnyTheme);

        await service.EndAsync(game.Id);

        Assert.Empty(db.GuessTheThemeRuns);
        Assert.Empty(db.GuessTheThemeRounds);
    }

    [Fact]
    public async Task StartingAgainReplacesBothSongsOfTheGameBefore()
    {
        using var db = CreateDb();
        await SeedAsync(db, 4);
        var service = Make(db);
        await service.StartAsync(AnyTheme);

        var second = await service.StartAsync(AnyTheme);

        var rounds = await RoundsAsync(db);
        Assert.Equal(2, rounds.Count);
        Assert.All(rounds, r => Assert.Equal(second.Id, r.RunId));
    }
}
