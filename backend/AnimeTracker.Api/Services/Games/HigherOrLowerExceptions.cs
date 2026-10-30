namespace AnimeTracker.Api.Services.Games;

/// <summary>The settings can't describe a game at all, as with a range running
/// backwards. The controller answers 400.</summary>
public class HigherOrLowerSettingsException(string message) : Exception(message);

/// <summary>The settings are valid but leave fewer than two different scores, so
/// no first pair can be drawn. The controller answers 409.</summary>
public class HigherOrLowerNotPlayableException()
    : Exception("There aren't enough anime for these settings.");

/// <summary>No game in progress has this id: it ended, or was replaced by another
/// tab's Start. The controller answers 404.</summary>
public class HigherOrLowerRunNotFoundException(int runId)
    : Exception($"Game {runId} is not in progress.");

/// <summary>A pick for a round the game has already moved past, as from a tab
/// behind it or a double submit. The controller answers 409.</summary>
public class HigherOrLowerStaleRoundException(int round)
    : Exception($"Round {round} is no longer the current round.");

/// <summary>A pick naming an anime that isn't one of the two on the table. The
/// controller answers 400.</summary>
public class HigherOrLowerCardNotOnTableException(int animeId)
    : Exception($"Anime {animeId} is not one of the two cards on the table.");
