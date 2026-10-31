using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Games;

/// <summary>How one guess compares with the answer (add-guess-the-anime design.md
/// D8). A pure function of two <see cref="AnimeStats"/> to one row, so the rules
/// are one tested place and the client only draws.
///
/// A stat that isn't known, on the guess or on the answer, is never compared: it
/// is never green and has no arrow. That lets an anime MyAnimeList has no episode
/// total for, such as One Piece, be the answer; requiring every stat would rule
/// out exactly the long-runners a player would think of.</summary>
public static class GuessComparison
{
    /// <summary>The row of <paramref name="guess"/>, compared with
    /// <paramref name="answer"/>. A number's comparison points from the guess
    /// towards the answer: <see cref="GuessCompared.AnswerHigher"/> when the
    /// answer's number is the bigger. Season is compared on its own, without its
    /// year, and comes from the start date through
    /// <see cref="SeasonCalendar.GetSeasonFor"/> in lower case, like the Season
    /// page's; type is MyAnimeList's own word for it. The row's picture and title
    /// are the guess's.</summary>
    public static GuessTheAnimeRowDto Compare(AnimeStats answer, AnimeStats guess) => new(
        guess.Id,
        guess.Title,
        guess.EnglishTitle,
        guess.PictureUrl,
        Number(answer.AiredFrom?.Year, guess.AiredFrom?.Year),
        Text(SeasonOf(answer.AiredFrom), SeasonOf(guess.AiredFrom)),
        Tags(answer.Genres, guess.Genres),
        Number(answer.Episodes, guess.Episodes),
        Tags(answer.Studios, guess.Studios),
        Text(answer.MediaType, guess.MediaType),
        Score(answer.MalScore, guess.MalScore));

    private static string? SeasonOf(DateOnly? airedFrom) =>
        airedFrom is { } date ? SeasonCalendar.GetSeasonFor(date).Season : null;

    private static GuessNumberDto Number(int? answer, int? guess) =>
        new(guess, answer is { } a && guess is { } g ? Order(a, g) : null);

    /// <summary>Two scores are the same at the two decimals shown, so
    /// floating-point noise can't make two shown 7.85s differ. Compared as whole
    /// hundredths, as <see cref="GamePicker.SameScore"/> does.</summary>
    private static GuessNumberDto Score(double? answer, double? guess) =>
        new(guess,
            answer is { } a && guess is { } g ? Order(GamePicker.Hundredths(a), GamePicker.Hundredths(g)) : null);

    private static GuessCompared Order(long answer, long guess) =>
        answer == guess ? GuessCompared.Same
        : answer > guess ? GuessCompared.AnswerHigher
        : GuessCompared.AnswerLower;

    private static GuessTextDto Text(string? answer, string? guess) =>
        new(guess, guess is not null && answer is not null && string.Equals(answer, guess, StringComparison.Ordinal));

    /// <summary>Each of the guess's genres or studios, in MyAnimeList's order so a
    /// match doesn't move anything, with whether the answer has it too. No list,
    /// or an empty one, is no tags, and the answer having none matches nothing.</summary>
    private static List<GuessTagDto> Tags(List<string>? answer, List<string>? guess) =>
        guess is null ? [] : [.. guess.Select(name => new GuessTagDto(name, answer?.Contains(name) ?? false))];
}
