namespace AnimeTracker.Api.Services.Season;

/// <summary>MAL's fixed season-quarter convention: winter = Jan-Mar, spring =
/// Apr-Jun, summer = Jul-Sep, fall = Oct-Dec.</summary>
public static class SeasonCalendar
{
    private static readonly string[] Order = ["winter", "spring", "summer", "fall"];

    public static (int Year, string Season) GetSeasonFor(DateOnly localDate) =>
        (localDate.Year, Order[(localDate.Month - 1) / 3]);

    public static (int Year, string Season) GetNextSeason(int year, string season)
    {
        var index = Array.IndexOf(Order, season);
        var nextIndex = (index + 1) % 4;
        var nextYear = nextIndex == 0 ? year + 1 : year;
        return (nextYear, Order[nextIndex]);
    }
}
