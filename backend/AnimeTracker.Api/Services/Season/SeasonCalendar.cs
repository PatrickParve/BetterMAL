namespace AnimeTracker.Api.Services.Season;

/// <summary>MAL's fixed season-quarter convention: winter = Jan-Mar, spring =
/// Apr-Jun, summer = Jul-Sep, fall = Oct-Dec.</summary>
public static class SeasonCalendar
{
    private static readonly string[] Order = ["winter", "spring", "summer", "fall"];

    /// <summary>The first year in MyAnimeList's season archive
    /// (myanimelist.net/anime/season/archive, checked 2026-09-14) — the lower
    /// end of the range the API accepts, and the same floor the frontend's
    /// season and year pages' arrows and dropdowns use.</summary>
    public const int EarliestArchiveYear = 1917;

    public static (int Year, string Season) GetSeasonFor(DateOnly localDate) =>
        (localDate.Year, Order[(localDate.Month - 1) / 3]);

    public static int GetSeasonIndex(string season) => Array.IndexOf(Order, season);

    /// <summary>The first day of a season's quarter — the date every
    /// season-age rule (SeasonRefreshCadence) measures from. The inverse-facing
    /// companion of <see cref="GetSeasonFor"/>, which maps a date to its
    /// quarter rather than a quarter to its date.</summary>
    public static DateOnly SeasonStart(int year, string season) => new(year, 3 * GetSeasonIndex(season) + 1, 1);

    /// <summary>A single monotonically increasing integer for a (year, season)
    /// point, so seasons can be compared and offset with plain integer
    /// arithmetic instead of juggling year and season-quarter together. Backs
    /// SeasonHorizon.Resolve.</summary>
    public static int GetSeasonPointIndex(int year, string season) => year * 4 + GetSeasonIndex(season);

    /// <summary>The inverse of <see cref="GetSeasonPointIndex"/>.</summary>
    public static (int Year, string Season) FromSeasonPointIndex(int pointIndex)
    {
        var year = pointIndex / 4;
        var seasonIndex = pointIndex % 4;
        // C#'s integer division/modulo truncate toward zero rather than
        // floor, so a point index whose modulo comes out negative needs
        // correcting back into [0, 4) to stay the exact inverse.
        if (seasonIndex < 0)
        {
            seasonIndex += 4;
            year -= 1;
        }
        return (year, Order[seasonIndex]);
    }

    /// <summary>Shifts a (year, season) point by delta seasons — negative
    /// steps backward — wrapping across year boundaries in either
    /// direction.</summary>
    public static (int Year, string Season) Shift(int year, string season, int delta) =>
        FromSeasonPointIndex(GetSeasonPointIndex(year, season) + delta);
}
