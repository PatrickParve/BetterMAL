namespace AnimeTracker.Api.Services.Games;

/// <summary>How a number compares with the answer's, from the answer's side:
/// <see cref="AnswerHigher"/> means the answer's number is higher than the
/// guess's, so the arrow points up. Serialised by name, like every enum on the
/// wire (add-guess-the-anime design.md D3/D8).</summary>
public enum GuessCompared
{
    Same,
    AnswerHigher,
    AnswerLower,
}

/// <summary>A numeric stat of a guess: year, episode count or MAL score.
/// <see cref="Value"/> is null when it isn't known, and
/// <see cref="Compared"/> is null whenever the two numbers can't be compared,
/// which is when it isn't known on the guess or on the answer, so an unknown is
/// never green and never has an arrow.</summary>
public record GuessNumberDto(double? Value, GuessCompared? Compared);

/// <summary>A one-valued text stat of a guess: season or type. <see cref="Same"/>
/// is false whenever either side doesn't have one.</summary>
public record GuessTextDto(string? Value, bool Same);

/// <summary>One genre or studio of a guess, and whether the answer has it too.</summary>
public record GuessTagDto(string Name, bool Same);

/// <summary>One guess, compared with the answer: its picture and title, then its
/// seven stats (add-guess-the-anime design.md D3). The answer's own row is the
/// same shape, compared with itself.</summary>
public record GuessTheAnimeRowDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    GuessNumberDto Year,
    GuessTextDto Season,
    List<GuessTagDto> Genres,
    GuessNumberDto Episodes,
    List<GuessTagDto> Studios,
    GuessTextDto MediaType,
    GuessNumberDto Score);

/// <summary>The answer once a game has ended: its row, every stat the same, and
/// whether it is on my list, for the row's Add to list. It is in no response
/// before the end.</summary>
public record GuessTheAnimeAnswerDto(GuessTheAnimeRowDto Row, bool InMyList);

/// <summary>A game in progress: the guesses so far, in order, each compared
/// afresh. The answer is nowhere in it, not even its id.</summary>
public record GuessTheAnimeGameDto(int Id, int MaxGuesses, List<GuessTheAnimeRowDto> Rows);

/// <summary>What opening either page needs: the game in progress, if any, the
/// years to offer, and two posters of popular anime for the Games tile and the
/// setup screen.</summary>
public record GuessTheAnimeStateDto(GuessTheAnimeGameDto? Game, GameYearsDto? Years, List<string> Pictures);

/// <summary>Why a game ended with a guess: it was the answer, or it was the
/// tenth and wrong.</summary>
public enum GuessTheAnimeEnd
{
    Guessed,
    OutOfGuesses,
}

/// <summary>A guess: <see cref="Number"/> is 1-based, the guess this is, and does
/// the job a round does in Higher or Lower, so a double press or a tab behind the
/// game fails on it.</summary>
public record GuessTheAnimeGuessRequest(int Number, int AnimeId);

/// <summary>The row of one guess and, when it ended the game, how, with the
/// answer. <see cref="Answer"/> is null unless <see cref="End"/> isn't; on a win
/// it is the guess's own row.</summary>
public record GuessTheAnimeGuessDto(GuessTheAnimeRowDto Row, GuessTheAnimeEnd? End, GuessTheAnimeAnswerDto? Answer);

/// <summary>A game ended with End game: the answer.</summary>
public record GuessTheAnimeEndDto(GuessTheAnimeAnswerDto Answer);
