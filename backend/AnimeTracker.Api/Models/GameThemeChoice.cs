namespace AnimeTracker.Api.Models;

/// <summary>Which songs Guess the Opening/Ending may play: any, only openings or
/// only endings (spec `guess-the-theme-selection`). Stored by name, as
/// <see cref="GameListScope"/> is.</summary>
public enum GameThemeChoice
{
    All,
    Openings,
    Endings,
}
