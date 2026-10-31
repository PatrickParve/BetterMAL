namespace AnimeTracker.Api.Services.Games;

/// <summary>The six lengths a song is played at, shortest first (spec `guess-the-theme`,
/// add-anime-themes-and-guess-the-theme design.md D7). The one place the numbers live: the
/// board reads them from the game it is sent. A run's <c>Step</c> is a position in this list,
/// 0 to <see cref="LastStep"/>.</summary>
public static class GuessTheThemeLadder
{
    public static readonly IReadOnlyList<double> Seconds = [0.1, 0.5, 1, 3, 5, 10];

    public static int Steps => Seconds.Count;

    /// <summary>The longest length. A wrong guess or a skip here loses the game; a right
    /// one carries on like any other.</summary>
    public static int LastStep => Seconds.Count - 1;

    /// <summary>Whether <paramref name="step"/> is a rung of the ladder.</summary>
    public static bool IsStep(int step) => step >= 0 && step <= LastStep;

    /// <summary>How long a rung plays for, in seconds. Throws for a step off the ladder,
    /// which only a bug could hand it.</summary>
    public static double LengthOf(int step) =>
        IsStep(step) ? Seconds[step] : throw new ArgumentOutOfRangeException(nameof(step), step, "Not a rung of the ladder.");
}
