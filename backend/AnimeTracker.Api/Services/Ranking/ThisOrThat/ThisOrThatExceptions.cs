namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>The score holds fewer than two hand-orderable anime, so there is
/// nothing to rank and it isn't offered. The controller answers 404.</summary>
public class ThisOrThatScoreNotOfferedException(int score)
    : Exception($"Score {score} has nothing to rank.");

/// <summary>Start was asked for a score with fewer than two anime. The controller
/// answers 400.</summary>
public class ThisOrThatNotEnoughAnimeException(int score)
    : Exception($"Score {score} needs at least two anime to rank.");

/// <summary>No ranking is in progress for the score, or it can't be read. The
/// controller answers 404.</summary>
public class ThisOrThatRunNotFoundException(int score)
    : Exception($"No ranking of score {score} is in progress.");

/// <summary>The request was made on a ranking that has since moved on: a version
/// that isn't the current one, a pair that isn't the current pair, or a write
/// that lost a race. Nothing was changed. The controller answers 409.</summary>
public class ThisOrThatStaleException(string message) : Exception(message);

/// <summary>Save before the last anime is placed. The controller answers 409.</summary>
public class ThisOrThatNotFinishedException(int score)
    : Exception($"The ranking of score {score} isn't finished.");

/// <summary>A pick naming a winner that is neither card of its pair. The
/// controller answers 400.</summary>
public class ThisOrThatWinnerNotInPairException(int animeId)
    : Exception($"Anime {animeId} is not one of the two cards.");
