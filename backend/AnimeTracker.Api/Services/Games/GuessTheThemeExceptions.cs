namespace AnimeTracker.Api.Services.Games;

/// <summary>The settings are valid but leave no song to play: no anime has a usable one, or the
/// catalogue hasn't been loaded yet. The controller answers 409.</summary>
public class GuessTheThemeNotPlayableException()
    : Exception("There aren't enough songs for these settings.");

/// <summary>No game in progress has this id: it ended, or was replaced by another tab's
/// Start. The controller answers 404.</summary>
public class GuessTheThemeRunNotFoundException(int runId)
    : Exception($"Game {runId} is not in progress.");

/// <summary>The game has no such song to play: it has been finished with, or was never drawn.
/// The controller answers 404.</summary>
public class GuessTheThemeRoundNotFoundException(int round)
    : Exception($"This game has no song {round} to play.");

/// <summary>A move for a number the game has passed, as from a tab behind the game or a double
/// press. The controller answers 409, and the board reads the game again with no notice.</summary>
public class GuessTheThemeStaleMoveException(int move)
    : Exception($"Move {move} is no longer the current move.");

/// <summary>A move the game isn't in a state for, such as a guess during a reveal or Next song
/// during a round. Only a stale tab or a bug can send one. The controller answers 409.</summary>
public class GuessTheThemeWrongPhaseException(string message) : Exception(message);

/// <summary>A guess naming an anime already guessed this round. The controller answers 400.</summary>
public class GuessTheThemeAlreadyGuessedException(int animeId)
    : Exception($"Anime {animeId} has already been guessed this round.");

/// <summary>A request to replace a song whose file is still there when checked. Replacing
/// would let a hard song be skipped, so only a file that has really gone can be. The
/// controller answers 409.</summary>
public class GuessTheThemeFileStillThereException()
    : Exception("The song's file is still there.");

/// <summary>A request to replace a song whose file couldn't be checked, because AnimeThemes
/// didn't answer. Nothing changes. The message says why, for the player's notice. The
/// controller answers 502.</summary>
public class GuessTheThemeCannotCheckException()
    : Exception("AnimeThemes didn't answer, so the song couldn't be checked.");
