using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Games;

/// <summary>Runs Guess the Opening/Ending on stored data (add-anime-themes-and-guess-the-theme
/// design.md D7-D10). The game in progress is one <see cref="GuessTheThemeRun"/> row and the
/// songs it has drawn are <see cref="GuessTheThemeRound"/> rows, all deleted however the game
/// ends, so nothing about a finished game survives, and since the game keeps no score nothing
/// outlives it as a count either.
///
/// <para>A game holds the song being played and the one after it, drawn when the round
/// starts (<see cref="StartAsync"/>, <see cref="NextAsync"/>), and tells
/// <see cref="IAnimeThemesMediaHold"/> to read them ahead, because the media host takes seconds to
/// start any read: the round is what pays that wait, so a reveal's video and the next song are
/// there when they're wanted. A right guess then has nothing to draw unless the earlier draw found
/// nothing left. How a game ends decides what stays held: End game keeps only the video on screen,
/// and a loss also keeps the song that was next, for Play again (<see cref="GuessTheThemeOnDeck"/>).</para>
///
/// <para>The song is copied onto its round at the draw, so a catalogue refresh landing
/// mid-game can't change or remove it. A song never leaves in any response before it is
/// revealed: not its anime, its title, its label or a link. The game makes no request to
/// MyAnimeList or AniList. The one thing it asks of AnimeThemes itself is a one-byte read of a
/// file that failed to load, before replacing the song (<see cref="ReplaceAsync"/>); the media's
/// bytes go through the hold and the relay, not through here.</para></summary>
public class GuessTheThemeService(
    AnimeTrackerDbContext db,
    GuessTheThemeDraw draw,
    GameLobbyReads lobby,
    IAnimeThemesMediaRelay relay,
    IAnimeThemesMediaHold media,
    GuessTheThemeOnDeck onDeck) : IGuessTheThemeService
{
    public async Task<GuessTheThemeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
    {
        var run = await db.GuessTheThemeRuns.AsNoTracking().OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct);
        var rounds = run is null ? [] : await LoadRoundsAsync(run.Id, ct);

        // A run with no current song is not a game: reads don't write, and Start replaces it.
        var game = run is not null && rounds.Any(r => r.Number == run.Round)
            ? await BuildGameAsync(run, rounds, ct)
            : null;

        var years = await lobby.YearsAsync(hideHentai, ct);

        // The posters leave out every song drawn and not yet revealed: the one being played
        // and the one after it. The current one's poster is left out too during a reveal,
        // where it makes no difference: it is on screen anyway.
        var pictures = await lobby.PopularPostersAsync(hideHentai, rounds.Select(r => r.AnimeId).ToList(), ct);

        return new GuessTheThemeStateDto(game, years, pictures, await AnimeThemesReads.IsCatalogueReadyAsync(db, ct));
    }

    public async Task<GamePoolDto> GetPoolAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        var count = await GuessTheThemePool.Query(db, settings, DateTimeOffset.UtcNow).CountAsync(ct);
        return new GamePoolDto(count, count > 0);
    }

    public async Task<GuessTheThemeGameDto> StartAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        // Everything that can refuse comes before anything is deleted, so a refused Start
        // leaves a game in progress as it was. The song already ready, if there is one, is the first
        // song, so its first length can play as this answer lands.
        var first = await TakeReadySongAsync(settings, ct)
            ?? await draw.DrawAsync(settings, [], ct)
            ?? throw new GuessTheThemeNotPlayableException();

        // The song after it, so its audio can load while the first is played. A pool with one
        // song left has none, and the first right guess then clears the game.
        var ahead = await draw.DrawAsync(settings, [first.SongId], ct);

        GuessTheThemeRun run;
        for (var attempt = 1; ; attempt++)
        {
            // Only a second tab can reach Start with a game in progress. If that tab's
            // guess lands while the old row is being deleted, the delete finds the move
            // count gone on: delete what's there now instead. Only this game's own tables
            // are touched.
            var old = await db.GuessTheThemeRuns.ToListAsync(ct);
            var oldIds = old.Select(r => r.Id).ToList();
            var oldRounds = await db.GuessTheThemeRounds.Where(r => oldIds.Contains(r.RunId)).ToListAsync(ct);
            db.GuessTheThemeRuns.RemoveRange(old);
            db.GuessTheThemeRounds.RemoveRange(oldRounds);

            run = new GuessTheThemeRun
            {
                Moves = 0,
                Round = 1,
                Step = 0,
                Phase = GuessTheThemePhase.Listening,
                RoundGuesses = [],
                PlayedSongIds = ahead is null ? [first.SongId] : [first.SongId, ahead.SongId],
                YearFrom = settings.YearFrom,
                YearTo = settings.YearTo,
                MediaTypes = settings.MediaTypes,
                ListScope = settings.List,
                HideHentai = settings.HideHentai,
                Themes = settings.Themes,
                // The run's id is generated by the database, so the first song is attached
                // through the navigation and EF gives it the real id when it saves.
                // Fresh rounds each attempt: one left over from an attempt that failed part way
                // may carry a key from it.
                Rounds = ahead is null ? [Numbered(first, 1)] : [Numbered(first, 1), Numbered(ahead, 2)],
            };
            db.GuessTheThemeRuns.Add(run);

            try
            {
                await db.SaveChangesAsync(ct);
                break;
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                db.ChangeTracker.Clear();
            }
        }

        media.Keep(ahead is null ? [Whole(first)] : [Whole(first), Whole(ahead)]);

        return await BuildGameAsync(run, run.Rounds, ct);
    }

    public async Task PrepareAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
    {
        settings = settings.Normalized();
        settings.Validate();

        // A game has songs of its own held, and a setup screen left open in another tab must not
        // take them away.
        if (await db.GuessTheThemeRuns.AnyAsync(ct))
            return;

        var song = onDeck.Peek(settings) ?? await draw.DrawAsync(settings, [], ct);
        if (song is null)
        {
            onDeck.Clear();
            media.Keep([]);
            return;
        }

        onDeck.Put(settings, song);

        // The audio alone: this may never become a game, and the video waits until it does.
        media.Keep([new HeldSong(song.AudioUrl, null)]);
    }

    /// <summary>The song ready for these settings, unless it has stopped fitting them since it was
    /// drawn: the catalogue has dropped it, or its anime has left the pool, as one just added to my
    /// list does under Not on my list. Taking it spends it, whichever way that goes.</summary>
    private async Task<GuessTheThemeRound?> TakeReadySongAsync(GuessTheThemeSettings settings, CancellationToken ct)
    {
        var song = onDeck.Take(settings);
        if (song is null)
            return null;

        var animeInPool = await GuessTheThemePool.Query(db, settings, DateTimeOffset.UtcNow).AnyAsync(a => a.Id == song.AnimeId, ct);
        var songUsable = await GuessTheThemePool.SongsLeft(db, settings, [])
            .AnyAsync(t => t.AnimeId == song.AnimeId && t.SongId == song.SongId, ct);
        return animeInPool && songUsable ? song : null;
    }

    private static HeldSong Whole(GuessTheThemeRound round) => new(round.AudioUrl, round.VideoUrl);

    /// <summary>What a reveal on screen still needs, once its round is over.</summary>
    private static HeldSong VideoOf(GuessTheThemeRound round) => new(null, round.VideoUrl);

    /// <summary>A drawn song as the game's round of that number, as a fresh row to add.</summary>
    private static GuessTheThemeRound Numbered(GuessTheThemeRound drawn, int number, int runId = 0) => new()
    {
        RunId = runId,
        Number = number,
        AnimeId = drawn.AnimeId,
        ThemeId = drawn.ThemeId,
        Kind = drawn.Kind,
        Label = drawn.Label,
        SongId = drawn.SongId,
        SongTitle = drawn.SongTitle,
        Artists = [.. drawn.Artists],
        AudioUrl = drawn.AudioUrl,
        VideoUrl = drawn.VideoUrl,
        AcceptedAnimeIds = [.. drawn.AcceptedAnimeIds],
    };

    public async Task<GuessTheThemeMoveDto> GuessAsync(int runId, int move, int animeId, CancellationToken ct = default)
    {
        var (run, round) = await LoadAsync(runId, ct);
        CheckMove(run, move, GuessTheThemePhase.Listening);

        // After the move and the phase: a tab behind the game names an anime that has since
        // been guessed, and should be told it is behind, not that it is wrong.
        if (run.RoundGuesses.Contains(animeId))
            throw new GuessTheThemeAlreadyGuessedException(animeId);

        // Judged by id alone: a guess of an anime the app has never stored is fetched
        // from nowhere. The drawn anime, or any other the catalogue lists with the same
        // song, is right (the drawn song's AcceptedAnimeIds).
        return round.AcceptedAnimeIds.Contains(animeId)
            ? await RightAsync(run, round, ct)
            : await MissAsync(run, round, GuessTheThemeOutcome.Wrong, animeId, ct);
    }

    public async Task<GuessTheThemeMoveDto> SkipAsync(int runId, int move, CancellationToken ct = default)
    {
        var (run, round) = await LoadAsync(runId, ct);
        CheckMove(run, move, GuessTheThemePhase.Listening);

        return await MissAsync(run, round, GuessTheThemeOutcome.Skipped, null, ct);
    }

    public async Task<GuessTheThemeGameDto> NextAsync(int runId, int move, CancellationToken ct = default)
    {
        var (run, current) = await LoadAsync(runId, ct);
        CheckMove(run, move, GuessTheThemePhase.Revealed);

        // The next song was drawn by the right guess, so it is here. Were it not, there is no
        // game to go on with, and the board reads again.
        var next = await db.GuessTheThemeRounds.AsNoTracking()
            .FirstOrDefaultAsync(r => r.RunId == run.Id && r.Number == run.Round + 1, ct)
            ?? throw new GuessTheThemeWrongPhaseException("The next song hasn't been drawn.");

        db.GuessTheThemeRounds.Remove(current);
        run.Round = next.Number;
        run.Step = 0;
        run.Phase = GuessTheThemePhase.Listening;
        run.RoundGuesses = [];
        run.Moves++;

        // The song after the one now being played, drawn now for the same reason as the first
        // game's: its audio has this whole round to load.
        var ahead = await draw.DrawAsync(GuessTheThemeSettings.From(run), run.PlayedSongIds, ct);
        if (ahead is not null)
        {
            db.GuessTheThemeRounds.Add(Numbered(ahead, next.Number + 1, run.Id));
            run.PlayedSongIds = [.. run.PlayedSongIds, ahead.SongId];
        }

        await SaveAsync(run, move, ct);

        media.Keep(ahead is null ? [Whole(next)] : [Whole(next), Whole(ahead)]);

        return await BuildGameAsync(run, await LoadRoundsAsync(run.Id, ct), ct);
    }

    public async Task<GuessTheThemeEndDto> EndAsync(int runId, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            var run = await db.GuessTheThemeRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
                ?? throw new GuessTheThemeRunNotFoundException(runId);
            var rounds = await db.GuessTheThemeRounds.Where(r => r.RunId == run.Id).ToListAsync(ct);

            // During a round the answer is shown, set apart as after a loss. During a reveal the
            // song is on screen already, and the song drawn after it has not been heard.
            var current = rounds.FirstOrDefault(r => r.Number == run.Round);
            var reveal = run.Phase == GuessTheThemePhase.Listening && current is not null
                ? await RevealAsync(current, heardSeconds: null, ct)
                : null;

            // The run first: its move count is the concurrency token, so a refused delete
            // is refused before anything else is touched.
            db.GuessTheThemeRuns.Remove(run);
            db.GuessTheThemeRounds.RemoveRange(rounds);
            try
            {
                await db.SaveChangesAsync(ct);

                // I've ended it, so nothing is kept for a game to come. The video stays while it's on screen.
                onDeck.Clear();
                media.Keep(current is null ? [] : [VideoOf(current)]);

                return new GuessTheThemeEndDto(reveal);
            }
            catch (DbUpdateConcurrencyException) when (attempt == 1)
            {
                // A move landed between the read and the delete. Ending the game is still
                // wanted: read what's there now, which may be a reveal where there was a
                // round, and end that.
                db.ChangeTracker.Clear();
            }
        }
    }

    public async Task<string> GetClipSourceAsync(int runId, int round, CancellationToken ct = default)
    {
        var run = await db.GuessTheThemeRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new GuessTheThemeRunNotFoundException(runId);

        // The song being played, or the one drawn ready for after it. A finished song's row is
        // gone, so its clip is too.
        return await db.GuessTheThemeRounds.AsNoTracking()
                .Where(r => r.RunId == run.Id && r.Number == round)
                .Select(r => r.AudioUrl)
                .FirstOrDefaultAsync(ct)
            ?? throw new GuessTheThemeRoundNotFoundException(round);
    }

    public Task<string?> GetVideoSourceAsync(int animeId, int themeId, CancellationToken ct = default) =>
        db.AnimeThemes.AsNoTracking()
            .Where(t => t.AnimeId == animeId && t.ThemeId == themeId)
            .Select(t => t.VideoUrl)
            .FirstOrDefaultAsync(ct);

    public async Task<GuessTheThemeReplaceDto> ReplaceAsync(int runId, int round, CancellationToken ct = default)
    {
        var (run, current) = await LoadAsync(runId, ct);

        // Only the song being played has a clip that can have failed to load. The one drawn for
        // after it is looked at again when it becomes the one being played.
        if (round != run.Round || run.Phase != GuessTheThemePhase.Listening)
            throw new GuessTheThemeRoundNotFoundException(round);

        // The server does the checking, so this can't be used to skip a hard song: a file that is
        // there is refused, and so is one that couldn't be checked, which changes nothing.
        switch (await relay.ProbeAsync(current.AudioUrl, ct))
        {
            case AnimeThemesMediaState.Present:
                throw new GuessTheThemeFileStillThereException();
            case AnimeThemesMediaState.Unreachable:
                throw new GuessTheThemeCannotCheckException();
        }

        var move = run.Moves;

        // The song that has gone is already in PlayedSongIds from its draw, so it isn't drawn
        // again, and neither is anything else played, the song drawn for after it included.
        var next = await draw.DrawAsync(GuessTheThemeSettings.From(run), run.PlayedSongIds, ct);

        // Nothing else to draw. If the song drawn for after this one is there, it takes this
        // one's place and the round goes on with it; otherwise the pool is spent, a win.
        var following = next is null
            ? await db.GuessTheThemeRounds.FirstOrDefaultAsync(r => r.RunId == run.Id && r.Number == run.Round + 1, ct)
            : null;
        if (next is null && following is null)
        {
            await RemoveRunAsync(run, ct);
            await SaveAsync(run, move, ct);
            onDeck.Clear();
            media.Keep([]);
            return new GuessTheThemeReplaceDto(GuessTheThemeOutcome.Cleared, null);
        }

        next ??= following!;
        if (following is not null)
            db.GuessTheThemeRounds.Remove(following);

        // The same round, on another song, at the first length with nothing guessed. That moves
        // the game on, so the move count does too: a guess in flight from the song that went, or
        // a tab still showing it, is stale and not applied to this one.
        current.AnimeId = next.AnimeId;
        current.ThemeId = next.ThemeId;
        current.Kind = next.Kind;
        current.Label = next.Label;
        current.SongId = next.SongId;
        current.SongTitle = next.SongTitle;
        current.Artists = next.Artists;
        current.AudioUrl = next.AudioUrl;
        current.VideoUrl = next.VideoUrl;
        current.AcceptedAnimeIds = next.AcceptedAnimeIds;

        if (following is null)
            run.PlayedSongIds = [.. run.PlayedSongIds, next.SongId];
        run.Step = 0;
        run.RoundGuesses = [];
        run.Moves++;
        await SaveAsync(run, move, ct);

        var rounds = await LoadRoundsAsync(run.Id, ct);
        media.Keep(rounds.Select(Whole));

        return new GuessTheThemeReplaceDto(null, await BuildGameAsync(run, rounds, ct));
    }

    /// <summary>A right guess. The next song was drawn when this round started, so its clip has
    /// been loading; one that wasn't, because nothing was left to draw then, is tried for again
    /// here. With none, the game ends here as a win.</summary>
    private async Task<GuessTheThemeMoveDto> RightAsync(GuessTheThemeRun run, GuessTheThemeRound round, CancellationToken ct)
    {
        var move = run.Moves;
        var heardSeconds = GuessTheThemeLadder.LengthOf(run.Step);

        GuessTheThemeRound? drawnNow = null;
        var drawnAlready = await db.GuessTheThemeRounds.AnyAsync(r => r.RunId == run.Id && r.Number == run.Round + 1, ct);
        if (!drawnAlready)
        {
            var next = await draw.DrawAsync(GuessTheThemeSettings.From(run), run.PlayedSongIds, ct);
            if (next is null)
            {
                var reveal = await RevealAsync(round, heardSeconds, ct);
                await RemoveRunAsync(run, ct);
                await SaveAsync(run, move, ct);
                onDeck.Clear();
                media.Keep([VideoOf(round)]);
                return new GuessTheThemeMoveDto(GuessTheThemeOutcome.Cleared, null, reveal);
            }

            next.RunId = run.Id;
            next.Number = run.Round + 1;
            db.GuessTheThemeRounds.Add(next);
            run.PlayedSongIds = [.. run.PlayedSongIds, next.SongId];
            drawnNow = next;
        }

        run.Phase = GuessTheThemePhase.Revealed;
        run.Moves++;
        await SaveAsync(run, move, ct);

        if (drawnNow is not null)
            media.Keep([Whole(round), Whole(drawnNow)]);

        return new GuessTheThemeMoveDto(
            GuessTheThemeOutcome.Right, await BuildGameAsync(run, await LoadRoundsAsync(run.Id, ct), ct), null);
    }

    /// <summary>A wrong guess or a skip. Below the last length it moves up the ladder; at the
    /// last it loses the game.</summary>
    private async Task<GuessTheThemeMoveDto> MissAsync(
        GuessTheThemeRun run, GuessTheThemeRound round, GuessTheThemeOutcome outcome, int? animeId, CancellationToken ct)
    {
        var move = run.Moves;

        if (run.Step >= GuessTheThemeLadder.LastStep)
        {
            var reveal = await RevealAsync(round, heardSeconds: null, ct);
            var ahead = await db.GuessTheThemeRounds.AsNoTracking()
                .FirstOrDefaultAsync(r => r.RunId == run.Id && r.Number == run.Round + 1, ct);
            await RemoveRunAsync(run, ct);
            await SaveAsync(run, move, ct);

            // The song that was next has been read for the game that just ended, so Play again
            // can start on it at once. The next press decides whether it's heard.
            if (ahead is null)
            {
                onDeck.Clear();
                media.Keep([VideoOf(round)]);
            }
            else
            {
                onDeck.Put(GuessTheThemeSettings.From(run), ahead);
                media.Keep([VideoOf(round), Whole(ahead)]);
            }

            return new GuessTheThemeMoveDto(GuessTheThemeOutcome.Lost, null, reveal);
        }

        run.Step++;
        if (animeId is { } guessed)
            run.RoundGuesses = [.. run.RoundGuesses, guessed];
        run.Moves++;
        await SaveAsync(run, move, ct);

        return new GuessTheThemeMoveDto(outcome, await BuildGameAsync(run, await LoadRoundsAsync(run.Id, ct), ct), null);
    }

    /// <summary>The run, tracked, and its current song. A run with no current song is not a
    /// game, as <see cref="GetStateAsync"/> reads it.</summary>
    private async Task<(GuessTheThemeRun Run, GuessTheThemeRound Round)> LoadAsync(int runId, CancellationToken ct)
    {
        var run = await db.GuessTheThemeRuns.FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw new GuessTheThemeRunNotFoundException(runId);
        var round = await db.GuessTheThemeRounds.FirstOrDefaultAsync(r => r.RunId == run.Id && r.Number == run.Round, ct)
            ?? throw new GuessTheThemeRunNotFoundException(runId);
        return (run, round);
    }

    /// <summary>A move is for the move the game is on and for the phase it is in. The move comes
    /// first: a tab behind the game is told it is behind, not that its move is out of place.</summary>
    private static void CheckMove(GuessTheThemeRun run, int move, GuessTheThemePhase phase)
    {
        if (move != run.Moves)
            throw new GuessTheThemeStaleMoveException(move);
        if (run.Phase != phase)
        {
            throw new GuessTheThemeWrongPhaseException(phase == GuessTheThemePhase.Listening
                ? "There is no song to guess: this one has been revealed."
                : "There is no reveal to move on from: this song hasn't been guessed.");
        }
    }

    /// <summary>Saves a move's changes. If the move count has moved since the run was read, a
    /// double press or a tab behind the game, the move is stale like any other. The concurrency
    /// token usually says so (<see cref="DbUpdateConcurrencyException"/>), but two presses of the
    /// same move that each add the next song insert the same round, and on a real database the
    /// second insert meets the round's key before the token is ever checked. That is the same race
    /// and the same answer, so a failed save is looked into: when the run has moved on from this
    /// move, or is gone, it was a lost race; any other failure is still a failure.</summary>
    private async Task SaveAsync(GuessTheThemeRun run, int move, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new GuessTheThemeStaleMoveException(move);
        }
        catch (DbUpdateException)
        {
            var stillOnThisMove = await db.GuessTheThemeRuns.AsNoTracking()
                .AnyAsync(r => r.Id == run.Id && r.Moves == move, ct);
            if (!stillOnThisMove)
                throw new GuessTheThemeStaleMoveException(move);
            throw;
        }
    }

    /// <summary>Marks the run, and every song it holds, for deletion. A cascade would do it on
    /// Postgres, but only for rows the context has not already got open.</summary>
    private async Task RemoveRunAsync(GuessTheThemeRun run, CancellationToken ct)
    {
        var rounds = await db.GuessTheThemeRounds.Where(r => r.RunId == run.Id).ToListAsync(ct);
        db.GuessTheThemeRuns.Remove(run);
        db.GuessTheThemeRounds.RemoveRange(rounds);
    }

    private Task<List<GuessTheThemeRound>> LoadRoundsAsync(int runId, CancellationToken ct) =>
        db.GuessTheThemeRounds.AsNoTracking().Where(r => r.RunId == runId).OrderBy(r => r.Number).ToListAsync(ct);

    /// <summary>The game as the board is sent it. The song being played is nowhere in it. Only
    /// a reveal carries one, and a reload during a reveal shows that reveal again, which gives
    /// nothing away.</summary>
    private async Task<GuessTheThemeGameDto> BuildGameAsync(
        GuessTheThemeRun run, List<GuessTheThemeRound> rounds, CancellationToken ct)
    {
        var revealed = run.Phase == GuessTheThemePhase.Revealed;
        var current = rounds.First(r => r.Number == run.Round);

        var reveal = revealed ? await RevealAsync(current, GuessTheThemeLadder.LengthOf(run.Step), ct) : null;
        int? nextRound = rounds.Any(r => r.Number == run.Round + 1) ? run.Round + 1 : null;

        // A guessed id that has no stored anime is kept in the list of ids and has no card:
        // storing it would mean a fetch from MyAnimeList for nothing.
        var guessedIds = run.RoundGuesses.ToList();
        var stored = guessedIds.Count == 0
            ? []
            : await db.AnimeMetadata.AsNoTracking()
                .Where(a => guessedIds.Contains(a.Id))
                .Select(a => new GuessTheThemeCardDto(a.Id, a.Title, a.EnglishTitle, a.PictureUrl))
                .ToListAsync(ct);
        var cards = guessedIds
            .Select(id => stored.FirstOrDefault(c => c.AnimeId == id))
            .OfType<GuessTheThemeCardDto>()
            .ToList();

        return new GuessTheThemeGameDto(
            run.Id,
            run.Moves,
            GuessTheThemeLadder.Seconds,
            run.Round,
            run.Step,
            run.Phase,
            reveal,
            nextRound,
            guessedIds,
            cards);
    }

    private async Task<GuessTheThemeRevealDto> RevealAsync(GuessTheThemeRound round, double? heardSeconds, CancellationToken ct)
    {
        var anime = await db.AnimeMetadata.AsNoTracking()
            .Where(a => a.Id == round.AnimeId)
            .Select(a => new { a.Title, a.EnglishTitle, a.PictureUrl })
            .FirstAsync(ct);
        var inMyList = await db.UserAnimeEntries.AsNoTracking().AnyAsync(e => e.AnimeId == round.AnimeId, ct);

        return new GuessTheThemeRevealDto(
            round.AnimeId,
            anime.Title,
            anime.EnglishTitle,
            anime.PictureUrl,
            round.Kind,
            round.Label,
            round.SongTitle,
            [.. round.Artists],
            round.VideoUrl,
            inMyList,
            heardSeconds,
            $"/api/games/guess-the-theme/video?anime={round.AnimeId}&theme={round.ThemeId}");
    }
}
