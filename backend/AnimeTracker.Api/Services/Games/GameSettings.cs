using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

/// <summary>The settings can't describe a game at all, as with a range running
/// backwards. The controller answers 400.</summary>
public class GameSettingsException(string message) : Exception(message);

/// <summary>What a game plays with (add-games-higher-or-lower design.md D3/D4),
/// the same for every game on the Games page. The same record is the body of
/// Start, the query of the pool count, and what a run in progress stores.
/// MediaTypes null means every type; List is serialised by name, like every enum
/// on the wire.</summary>
public record GameSettings(
    int? YearFrom,
    int? YearTo,
    List<string>? MediaTypes,
    GameListScope List,
    bool HideHentai)
{
    // DateOnly's own limits: a bound outside them can't become a date to compare.
    private const int FirstYear = 1;
    private const int LastYear = 9999;

    /// <summary>The settings a run in progress was started with.</summary>
    public static GameSettings From(HigherOrLowerRun run) =>
        new(run.YearFrom, run.YearTo, run.MediaTypes, run.ListScope, run.HideHentai);

    public static GameSettings From(GuessTheAnimeRun run) =>
        new(run.YearFrom, run.YearTo, run.MediaTypes, run.ListScope, run.HideHentai);

    /// <summary>These settings in their one canonical form: an empty list of
    /// types is every type, as removing the last type returns to All, so it is
    /// stored as null and a repeated type is counted once.</summary>
    public GameSettings Normalized() =>
        this with { MediaTypes = MediaTypes is { Count: > 0 } types ? types.Distinct().ToList() : null };

    /// <summary>Throws <see cref="GameSettingsException"/> for settings
    /// that can't describe a game: an unknown list scope, a year that isn't a
    /// year, or a From after the To. Too few anime is another matter, which
    /// only the data can tell.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(List))
            throw new GameSettingsException($"Unknown list setting '{List}'.");

        foreach (var year in new[] { YearFrom, YearTo })
        {
            if (year is { } y && (y < FirstYear || y > LastYear))
                throw new GameSettingsException($"{y} is not a release year.");
        }

        if (YearFrom is { } from && YearTo is { } to && from > to)
            throw new GameSettingsException($"The release years run backwards: {from} is after {to}.");
    }
}
