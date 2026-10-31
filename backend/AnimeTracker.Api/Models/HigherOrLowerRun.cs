namespace AnimeTracker.Api.Models;

/// <summary>The one Higher or Lower game in progress, and nothing else about it
/// (add-games-higher-or-lower design.md D2). Deleted when the game ends, however
/// it ends, so "in progress" is simply "a row exists" and no best score, past
/// streak or history lives anywhere. Streak is the game's live state, kept so a
/// reload resumes it, not a saved result. The settings are the ones the game
/// started with, so a later Hide NSFW change doesn't alter a game under way.</summary>
public class HigherOrLowerRun
{
    public int Id { get; set; }

    /// <summary>1-based. The concurrency token: a double submit or a tab behind
    /// the game can't advance it twice.</summary>
    public int Round { get; set; } = 1;

    /// <summary>Correct picks so far.</summary>
    public int Streak { get; set; }

    public int LeftAnimeId { get; set; }
    public int RightAnimeId { get; set; }

    /// <summary>How many rounds the left anime has stayed. The right anime that
    /// takes over starts at 1 (design.md D8).</summary>
    public int LeftStays { get; set; }

    /// <summary>Every anime this game has shown, so none repeats within it. Kept
    /// here rather than on the appearance rows so deleting the run leaves that
    /// list alone.</summary>
    public List<int> ShownAnimeIds { get; set; } = [];

    public int? YearFrom { get; set; }
    public int? YearTo { get; set; }

    /// <summary>Null means every type.</summary>
    public List<string>? MediaTypes { get; set; }

    public GameListScope ListScope { get; set; }
    public bool HideHentai { get; set; }
}
