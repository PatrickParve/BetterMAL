using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Games;

/// <summary>What a game plays with (add-games-higher-or-lower design.md D3/D4).
/// The same record is the body of Start, the query of the pool count, and what a
/// run in progress stores. MediaTypes null means every type; List is serialised
/// by name, like every enum on the wire.</summary>
public record HigherOrLowerSettings(
    int? YearFrom,
    int? YearTo,
    List<string>? MediaTypes,
    HigherOrLowerListScope List,
    bool HideHentai)
{
    // DateOnly's own limits: a bound outside them can't become a date to compare.
    private const int FirstYear = 1;
    private const int LastYear = 9999;

    /// <summary>The settings a run in progress was started with.</summary>
    public static HigherOrLowerSettings From(HigherOrLowerRun run) =>
        new(run.YearFrom, run.YearTo, run.MediaTypes, run.ListScope, run.HideHentai);

    /// <summary>These settings in their one canonical form: an empty list of
    /// types is every type, as removing the last type returns to All, so it is
    /// stored as null and a repeated type is counted once.</summary>
    public HigherOrLowerSettings Normalized() =>
        this with { MediaTypes = MediaTypes is { Count: > 0 } types ? types.Distinct().ToList() : null };

    /// <summary>Throws <see cref="HigherOrLowerSettingsException"/> for settings
    /// that can't describe a game: an unknown list scope, a year that isn't a
    /// year, or a From after the To. Too few anime is another matter, which
    /// only the data can tell.</summary>
    public void Validate()
    {
        if (!Enum.IsDefined(List))
            throw new HigherOrLowerSettingsException($"Unknown list setting '{List}'.");

        foreach (var year in new[] { YearFrom, YearTo })
        {
            if (year is { } y && (y < FirstYear || y > LastYear))
                throw new HigherOrLowerSettingsException($"{y} is not a release year.");
        }

        if (YearFrom is { } from && YearTo is { } to && from > to)
            throw new HigherOrLowerSettingsException($"The release years run backwards: {from} is after {to}.");
    }
}

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

/// <summary>The years the eligible anime cover, for the setup screen's From and
/// To choices.</summary>
public record HigherOrLowerYearsDto(int Min, int Max);

/// <summary>What opening either page needs: the game in progress, if any, the
/// years to offer, and two posters of popular anime for the Games tile and the
/// setup screen. The posters carry no score.</summary>
public record HigherOrLowerStateDto(
    HigherOrLowerGameDto? Game,
    HigherOrLowerYearsDto? Years,
    List<string> Pictures);

/// <summary>How many anime some settings leave, and whether that is enough to
/// play: at least two different scores.</summary>
public record HigherOrLowerPoolDto(int Count, bool Playable);

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
