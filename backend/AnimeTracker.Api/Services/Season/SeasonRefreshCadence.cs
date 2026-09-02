namespace AnimeTracker.Api.Services.Season;

/// <summary>How often a season may be re-fetched from MyAnimeList, keyed by
/// the season's own age: an old season's listing does not change, so it does
/// not need re-asking as often as a current one. Pure and stateless like
/// <see cref="SeasonCalendar"/>/<see cref="SeasonHorizon"/> beside it — today
/// is always a parameter, never read from the clock, so the boundary
/// behaviour is testable rather than incidental.</summary>
public static class SeasonRefreshCadence
{
    /// <summary>Whole elapsed years from <paramref name="start"/> to
    /// <paramref name="today"/>, clamped at zero. The clamp is load-bearing
    /// (design D8): a season that has not started yet must land in the daily
    /// tier, because the forward-horizon ceiling (SeasonHorizon) re-probes a
    /// future season daily, and a longer interval here would freeze it.</summary>
    private static int WholeYearsSince(DateOnly start, DateOnly today)
    {
        var years = today.Year - start.Year;
        if (today < start.AddYears(years))
            years--;
        return Math.Max(years, 0);
    }

    public static int MinimumDaysBetweenFetches(int year, string season, DateOnly todayLocalDate)
    {
        var wholeYears = WholeYearsSince(SeasonCalendar.SeasonStart(year, season), todayLocalDate);

        // A value of 1 here is exactly the previous once-per-local-day rule,
        // so a current season's behaviour is unchanged.
        return wholeYears switch
        {
            0 => 1,
            1 => 3,
            2 or 3 or 4 => 5,
            _ => 10,
        };
    }

    public static bool IsFresh(int year, string season, DateOnly lastFetchedLocalDate, DateOnly todayLocalDate) =>
        // Day-number arithmetic, not TimeSpan: "a day" here means a local
        // calendar day, as it already does everywhere else in the season path.
        todayLocalDate.DayNumber - lastFetchedLocalDate.DayNumber < MinimumDaysBetweenFetches(year, season, todayLocalDate);
}
