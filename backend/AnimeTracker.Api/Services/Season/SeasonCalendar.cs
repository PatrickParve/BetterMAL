namespace AnimeTracker.Api.Services.Season;

/// <summary>MAL's fixed season-quarter convention: winter = Jan-Mar, spring =
/// Apr-Jun, summer = Jul-Sep, fall = Oct-Dec.</summary>
public static class SeasonCalendar
{
    private static readonly string[] Order = ["winter", "spring", "summer", "fall"];

    public static (int Year, string Season) GetSeasonFor(DateOnly localDate) =>
        (localDate.Year, Order[(localDate.Month - 1) / 3]);

    public static int GetSeasonIndex(string season) => Array.IndexOf(Order, season);
}
