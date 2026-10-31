namespace AnimeTracker.Api.Services.Games;

/// <summary>One anime on the table. <see cref="Score"/> is null until it has been
/// revealed: always for the right card, and for the left card in round 1. A
/// hidden score is never in a response at all, so nothing in the page or the
/// network log can spoil a round.</summary>
public record HigherOrLowerCardDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    int? Year,
    bool InMyList,
    double? Score);

public record HigherOrLowerGameDto(
    int Id,
    int Round,
    int Streak,
    HigherOrLowerCardDto Left,
    HigherOrLowerCardDto Right);

/// <summary>What opening either page needs: the game in progress, if any, the
/// years to offer, and two posters of popular anime for the Games tile and the
/// setup screen. The posters carry no score.</summary>
public record HigherOrLowerStateDto(
    HigherOrLowerGameDto? Game,
    GameYearsDto? Years,
    List<string> Pictures);

/// <summary>Why a game ended with a pick: the pick was wrong, or no anime was
/// left to draw, which is a win.</summary>
public enum HigherOrLowerEnd
{
    Wrong,
    Cleared,
}

public record HigherOrLowerPickRequest(int Round, int AnimeId);

/// <summary>The verdict on one pick and, unless the game ended, the whole next
/// round, so the client needs no second request. The scores are those the pick
/// was judged on. <see cref="MilestoneLevel"/> is the streak's place in the
/// milestone sequence when it has just reached one, otherwise null.</summary>
public record HigherOrLowerPickDto(
    bool Correct,
    double LeftScore,
    double RightScore,
    int Streak,
    int? MilestoneLevel,
    HigherOrLowerEnd? End,
    HigherOrLowerGameDto? Next);

/// <summary>The streak of a game that has just ended, the one thing the setup
/// screen shows of it before it is gone.</summary>
public record HigherOrLowerEndDto(int Streak);
