namespace AnimeTracker.Api.Services.Ranking.ThisOrThat;

/// <summary>One offered score on the page's tabs: a score with at least two
/// hand-orderable anime, how many, the most picks a ranking of them can take, and
/// whether a ranking of it is in progress.</summary>
public record ThisOrThatScoreSummary(int Score, int Count, int MaxPicks, bool InProgress);

public record ThisOrThatScoresDto(List<ThisOrThatScoreSummary> Scores);

/// <summary>One anime on a card. It carries no score of any kind: the two cards
/// share my score, and MyAnimeList's would only colour the pick.</summary>
public record ThisOrThatCard(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    int? Year);

/// <summary>The two cards, as they are laid out: the anime being placed is on the
/// side its seed gives it, for every pick of its placement.</summary>
public record ThisOrThatPairDto(ThisOrThatCard Left, ThisOrThatCard Right);

/// <summary>One row of a finished ranking. Positions are 1-based;
/// <see cref="PreviousPosition"/> is the anime's place in the score's
/// hand-ordered order as it stood when the result was read.</summary>
public record ThisOrThatResultRow(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int Position,
    int PreviousPosition);

/// <summary>A ranking in progress, rebuilt from its stored facts. Exactly one of
/// <see cref="Pair"/> and <see cref="Result"/> is set.
/// <see cref="UpcomingPictureUrls"/> are the pictures of the anime the next pair
/// could hold whichever way the pick goes (at most four), for the page to start
/// downloading.</summary>
public record ThisOrThatRunState(
    int Version,
    int Placed,
    int Total,
    int PicksMade,
    int PicksLeftAtMost,
    bool CanUndo,
    ThisOrThatPairDto? Pair,
    List<ThisOrThatResultRow>? Result,
    List<string> UpcomingPictureUrls);

public record ThisOrThatScoreState(int Score, int Count, int MaxPicks, ThisOrThatRunState? Run);

/// <summary>Starts a ranking, or starts it over: <see cref="ReplacingVersion"/>
/// is the version of the ranking being thrown away, absent for a first
/// Start.</summary>
public record ThisOrThatStartRequest(int? ReplacingVersion);

/// <summary>A pick, naming the pair it was made on. A null
/// <see cref="WinnerId"/> is Can't decide.</summary>
public record ThisOrThatPickRequest(int LeftId, int RightId, int? WinnerId);

/// <summary>Undo and Save act on the whole ranking, so they name the version they
/// were made on.</summary>
public record ThisOrThatVersionRequest(int Version);
