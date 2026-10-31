namespace AnimeTracker.Api.Services.Games;

/// <summary>Guess the Opening/Ending, run on the server so its rules are tested and nothing that
/// names a song reaches the browser before it is revealed (add-anime-themes-and-guess-the-theme
/// design.md D7). Unlike Guess the Anime it never calls MyAnimeList or AniList: songs are drawn,
/// judged and revealed from stored data.</summary>
public interface IGuessTheThemeService
{
    /// <summary>The game in progress if there is one, the release years the eligible anime
    /// cover, two posters of popular anime, and whether the catalogue has ever finished a
    /// refresh. Stored data only. Nothing that names an unrevealed song is in it, and the
    /// posters leave out the anime of every song drawn and not yet revealed. A run with no
    /// current song counts as no game. Hide NSFW shapes the years and posters; a game in
    /// progress keeps the setting it started with.</summary>
    Task<GuessTheThemeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default);

    /// <summary>How many anime these settings leave, and whether that is enough to play, which
    /// is any at all. Throws <see cref="GameSettingsException"/> for settings that can't describe
    /// a game.</summary>
    Task<GamePoolDto> GetPoolAsync(GuessTheThemeSettings settings, CancellationToken ct = default);

    /// <summary>Draws the first song and starts a game, replacing any Guess the Opening/Ending
    /// game in progress and leaving a game of either other game alone. Throws
    /// <see cref="GameSettingsException"/> for settings that can't describe a game and
    /// <see cref="GuessTheThemeNotPlayableException"/> for ones that leave no song. Everything
    /// that can refuse comes before anything is deleted.</summary>
    Task<GuessTheThemeGameDto> StartAsync(GuessTheThemeSettings settings, CancellationToken ct = default);

    /// <summary>One guess at the current length. Throws
    /// <see cref="GuessTheThemeRunNotFoundException"/> for a game no longer in progress,
    /// <see cref="GuessTheThemeStaleMoveException"/> for a move the game has passed,
    /// <see cref="GuessTheThemeWrongPhaseException"/> during a reveal and
    /// <see cref="GuessTheThemeAlreadyGuessedException"/> for an anime already guessed this
    /// round.</summary>
    Task<GuessTheThemeMoveDto> GuessAsync(int runId, int move, int animeId, CancellationToken ct = default);

    /// <summary>A guess of nothing: what a wrong guess does, with no anime. Throws what
    /// <see cref="GuessAsync"/> does, bar the already-guessed refusal.</summary>
    Task<GuessTheThemeMoveDto> SkipAsync(int runId, int move, CancellationToken ct = default);

    /// <summary>Moves from a reveal to the song drawn for after it, which has been ready since
    /// the right guess. Throws <see cref="GuessTheThemeRunNotFoundException"/>,
    /// <see cref="GuessTheThemeStaleMoveException"/>, and
    /// <see cref="GuessTheThemeWrongPhaseException"/> outside a reveal.</summary>
    Task<GuessTheThemeGameDto> NextAsync(int runId, int move, CancellationToken ct = default);

    /// <summary>Ends a game early. During a round it answers with the song, set apart; during a
    /// reveal, where the song is already on screen, with nothing. Throws
    /// <see cref="GuessTheThemeRunNotFoundException"/> for a game no longer in progress.</summary>
    Task<GuessTheThemeEndDto> EndAsync(int runId, CancellationToken ct = default);

    /// <summary>The audio link of one of the game's songs, for the relay to read: the one being
    /// played or the one drawn ready for after it. It is never sent to the browser. Throws
    /// <see cref="GuessTheThemeRunNotFoundException"/> and
    /// <see cref="GuessTheThemeRoundNotFoundException"/> for a song the game doesn't hold.</summary>
    Task<string> GetClipSourceAsync(int runId, int round, CancellationToken ct = default);

    /// <summary>The link of a theme's video, for the relay to read, or null for a theme the catalogue
    /// doesn't have with one. A reveal has already named the song, so this is by the song itself and
    /// outlives the game, as a reveal does.</summary>
    Task<string?> GetVideoSourceAsync(int animeId, int themeId, CancellationToken ct = default);

    /// <summary>Has a song ready for a game with these settings, so that game's first length can start
    /// as Play is pressed. The setup screen asks while it's open. It reads the song's audio ahead and
    /// nothing else, keeps a song already ready for these settings, and does nothing while a game is
    /// in progress. Throws <see cref="GameSettingsException"/> for settings that can't describe a
    /// game.</summary>
    Task PrepareAsync(GuessTheThemeSettings settings, CancellationToken ct = default);

    /// <summary>Draws another song for the round being played, whose file has gone. The file is
    /// checked first with a one-byte read, so this can't be used to skip a hard song. The new song
    /// starts at the first length with no guesses, and the song that went is not drawn again in this
    /// game. With no other song to draw, the game ends as a win. Throws
    /// <see cref="GuessTheThemeRunNotFoundException"/> and
    /// <see cref="GuessTheThemeRoundNotFoundException"/> for any song but the one being played,
    /// <see cref="GuessTheThemeFileStillThereException"/> and
    /// <see cref="GuessTheThemeCannotCheckException"/>.</summary>
    Task<GuessTheThemeReplaceDto> ReplaceAsync(int runId, int round, CancellationToken ct = default);
}
