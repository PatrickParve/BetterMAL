using System.Text.Json.Serialization;
using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

/// <summary>What Guess the Opening/Ending plays with: the four settings every game
/// shares, which <see cref="Game"/> hands back as a <see cref="GameSettings"/> so the
/// shared pool and validation do their part unchanged, and the Theme setting that is
/// this game's alone (spec `guess-the-theme-selection`, add-anime-themes-and-guess-the-theme
/// design.md D7). The same record is the body of Start, the query of the pool count and
/// what a run in progress stores. MediaTypes null means every type; List and Themes are
/// serialised by name, like every enum on the wire.</summary>
public record GuessTheThemeSettings(
    int? YearFrom,
    int? YearTo,
    List<string>? MediaTypes,
    GameListScope List,
    bool HideHentai,
    GameThemeChoice Themes)
{
    /// <summary>The shared half, for <see cref="GamePool.Query"/> and the shared
    /// validation. Never serialised: it would only repeat the five fields above.</summary>
    [JsonIgnore]
    public GameSettings Game => new(YearFrom, YearTo, MediaTypes, List, HideHentai);

    /// <summary>The settings a run in progress was started with.</summary>
    public static GuessTheThemeSettings From(GuessTheThemeRun run) =>
        new(run.YearFrom, run.YearTo, run.MediaTypes, run.ListScope, run.HideHentai, run.Themes);

    /// <summary>These settings in their one canonical form, as
    /// <see cref="GameSettings.Normalized"/> has it: an empty list of types is every
    /// type, and a repeated type is counted once.</summary>
    public GuessTheThemeSettings Normalized() => this with { MediaTypes = Game.Normalized().MediaTypes };

    /// <summary>Throws <see cref="GameSettingsException"/> for settings that can't
    /// describe a game: anything the shared settings refuse, or a Theme that isn't one
    /// of the three. Too few songs is another matter, which only the data can tell.</summary>
    public void Validate()
    {
        Game.Validate();

        if (!Enum.IsDefined(Themes))
            throw new GameSettingsException($"Unknown theme setting '{Themes}'.");
    }
}
