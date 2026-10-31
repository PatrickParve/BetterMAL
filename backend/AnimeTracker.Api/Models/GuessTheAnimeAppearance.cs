namespace AnimeTracker.Api.Models;

/// <summary>One anime drawn as the answer of a Guess the Anime game, in the order
/// drawn (add-guess-the-anime design.md D2). It holds only which anime it was: no
/// game id and no result, so it records nothing about how any game went, and an
/// anime only guessed is never recorded. It outlives the game that drew it,
/// because holding recent answers back across games is its whole purpose, and is
/// removed with the anime. It is this game's own list, so an anime shown in
/// Higher or Lower isn't held back here and an answer here isn't held back
/// there.</summary>
public class GuessTheAnimeAppearance
{
    public long Id { get; set; }
    public int AnimeId { get; set; } // MAL id
}
