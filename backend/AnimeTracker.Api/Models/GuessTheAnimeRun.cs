namespace AnimeTracker.Api.Models;

/// <summary>The one Guess the Anime game in progress, and nothing else about it
/// (add-guess-the-anime design.md D2). Deleted when the game ends, however it
/// ends, so "in progress" is simply "a row exists" and no win count, best result
/// or history lives anywhere. Stats aren't stored here: a guessed anime's row is
/// compared with the answer afresh on every read, so a score that moves mid-game
/// moves its row too and there is no snapshot to go stale. The settings are the
/// ones the game started with, so a later Hide NSFW change doesn't alter a game
/// under way.</summary>
public class GuessTheAnimeRun
{
    public int Id { get; set; }

    /// <summary>The hidden anime. It never leaves the server before the game
    /// ends.</summary>
    public int AnswerAnimeId { get; set; }

    /// <summary>Every anime guessed so far, in the order guessed. No foreign key
    /// (an array can't have one): nothing deletes a metadata row today, and
    /// building a row skips an id it can't find.</summary>
    public List<int> GuessedAnimeIds { get; set; } = [];

    /// <summary>How many guesses have been made, always the length of
    /// <see cref="GuessedAnimeIds"/>. The concurrency token: a double submit or a
    /// guess from a tab behind the game can't be counted twice.</summary>
    public int GuessCount { get; set; }

    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }

    /// <summary>Null means every type.</summary>
    public List<string>? MediaTypes { get; set; }

    public GameListScope ListScope { get; set; }
    public bool HideHentai { get; set; }
}
