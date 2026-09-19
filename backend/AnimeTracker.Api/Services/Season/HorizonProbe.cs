namespace AnimeTracker.Api.Services.Season;

/// <summary>Decides whether to probe the one season MAL's default forward
/// window (<see cref="SeasonHorizon.FutureSeasonWindow"/>) doesn't reach —
/// current + 3 — so the navigable ceiling can climb on its own as MAL opens a
/// further season, instead of waiting for someone to think of visiting a
/// future year (design D10). Pure and stateless like SeasonHorizon /
/// SeasonRefreshCadence beside it: today is always a parameter, never read
/// from the clock.</summary>
public static class HorizonProbe
{
    // One past SeasonHorizon.FutureSeasonWindow — the season the default
    // window doesn't reach at all.
    private const int TargetOffset = SeasonHorizon.FutureSeasonWindow + 1;

    /// <summary>The one season this probe ever asks about — fixed relative to
    /// the current season, not to wherever the ceiling now sits, so a
    /// successful probe ends the probing instead of moving the target one
    /// further out (design D10).</summary>
    public static (int Year, string Season) Target((int Year, string Season) current) =>
        SeasonCalendar.Shift(current.Year, current.Season, TargetOffset);

    /// <summary>Whether a probe should run now — three conditions, all
    /// required: the target is still past the outer ceiling (a probe that
    /// would learn nothing new is skipped, and this is also what makes a 404
    /// probe unable to lower either ceiling, since the target sits outside the
    /// range the ceiling's own step-back considers); today falls in the final
    /// month of the current season, unless <paramref name="onDemand"/> — a
    /// URL addressing the target directly is an explicit question, not a
    /// background guess, so it skips the window while still honouring the
    /// once-per-day gate (design D10a); and the target hasn't already been
    /// asked about today.</summary>
    public static bool ShouldProbe(
        (int Year, string Season) current,
        DateOnly today,
        (int Year, string Season) outerCeiling,
        DateOnly? targetLastFetchedLocalDate,
        bool onDemand = false)
    {
        var target = Target(current);
        var targetIndex = SeasonCalendar.GetSeasonPointIndex(target.Year, target.Season);
        var outerCeilingIndex = SeasonCalendar.GetSeasonPointIndex(outerCeiling.Year, outerCeiling.Season);

        return targetIndex > outerCeilingIndex
            && (onDemand || IsFinalMonthOfCurrentSeason(current, today))
            && targetLastFetchedLocalDate != today;
    }

    // MAL opens the next season roughly a month before it starts, so the
    // final calendar month of a season-quarter (March, June, September,
    // December) is when a probe past the default window is worth the ask.
    private static bool IsFinalMonthOfCurrentSeason((int Year, string Season) current, DateOnly today) =>
        today.Year == current.Year && today.Month == 3 * (SeasonCalendar.GetSeasonIndex(current.Season) + 1);
}
