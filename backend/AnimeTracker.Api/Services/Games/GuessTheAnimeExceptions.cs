namespace AnimeTracker.Api.Services.Games;

/// <summary>The settings are valid but leave no anime at all, so no answer can be
/// drawn. The controller answers 409.</summary>
public class GuessTheAnimeNotPlayableException()
    : Exception("There aren't enough anime for these settings.");

/// <summary>No answer could be settled: every anime the settings allow was passed
/// over or has never had a full-detail fetch, and MyAnimeList wouldn't give one.
/// The message says why, for the player's notice. The controller answers
/// 502.</summary>
public class GuessTheAnimeNoAnswerException(string reason) : Exception(reason);

/// <summary>No game in progress has this id: it ended, or was replaced by another
/// tab's Start. The controller answers 404.</summary>
public class GuessTheAnimeRunNotFoundException(int runId)
    : Exception($"Game {runId} is not in progress.");

/// <summary>A guess for a number the game has already passed, as from a tab behind
/// the game or a double press. The controller answers 409.</summary>
public class GuessTheAnimeStaleGuessException(int number)
    : Exception($"Guess {number} is no longer the current guess.");

/// <summary>A guess naming an anime this game has already had. The controller
/// answers 400.</summary>
public class GuessTheAnimeAlreadyGuessedException(int animeId)
    : Exception($"Anime {animeId} has already been guessed in this game.");

/// <summary>A guess that can't go ahead because the anime has never had a
/// full-detail fetch and MyAnimeList wouldn't give one: its row would have no
/// genres or studio, which tells nothing and would cost a guess. Nothing is
/// counted. The message says why. The controller answers 502.</summary>
public class GuessTheAnimeDetailsUnavailableException(string reason, Exception inner)
    : Exception(reason, inner);
