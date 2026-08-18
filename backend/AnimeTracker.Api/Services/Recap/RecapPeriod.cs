using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The three recap period modes, as plain validated strings (like
/// <c>TopAnimeRankingType</c>) rather than a C# enum, since a controller
/// receives and echoes this as a query-string token, not a wire enum.</summary>
public static class RecapMode
{
    public const string MultiYear = "multiYear";
    public const string Yearly = "yearly";
    public const string Season = "season";

    public static readonly IReadOnlyList<string> SupportedValues = [MultiYear, Yearly, Season];

    public static bool IsSupported(string? mode) => mode is not null && SupportedValues.Contains(mode);
}

/// <summary>A recap's period: a mode plus the parameters that resolve it to
/// an inclusive date range and to the years/seasons it covers. Multi-year
/// bounds are normalised to ascending order on construction (tasks.md 2.1),
/// so a reversed range behaves exactly like the same range the right way
/// round rather than producing an empty period.</summary>
public sealed class RecapPeriod
{
    private static readonly string[] SeasonOrder = ["winter", "spring", "summer", "fall"];

    public string Mode { get; }
    public int StartYear { get; }
    public int EndYear { get; }
    public string? Season { get; }

    private RecapPeriod(string mode, int startYear, int endYear, string? season)
    {
        Mode = mode;
        StartYear = startYear;
        EndYear = endYear;
        Season = season;
    }

    public static RecapPeriod MultiYear(int startYear, int endYear) =>
        new(RecapMode.MultiYear, Math.Min(startYear, endYear), Math.Max(startYear, endYear), null);

    public static RecapPeriod Yearly(int year) => new(RecapMode.Yearly, year, year, null);

    public static RecapPeriod OfSeason(int year, string season) => new(RecapMode.Season, year, year, season);

    /// <summary>The inclusive calendar-date range this period resolves to:
    /// a season's own quarter, or 1 Jan of StartYear through 31 Dec of
    /// EndYear for the other two modes (which coincide for a yearly
    /// period, where StartYear == EndYear).</summary>
    public (DateOnly Start, DateOnly End) DateRange => Mode == RecapMode.Season
        ? SeasonDateRange(StartYear, Season!)
        : (new DateOnly(StartYear, 1, 1), new DateOnly(EndYear, 12, 31));

    /// <summary>Every calendar year the period spans, ascending.</summary>
    public List<int> Years => Enumerable.Range(StartYear, EndYear - StartYear + 1).ToList();

    /// <summary>Every (year, season) point the period spans, ascending — the
    /// single season for a season recap, or the four seasons of every year
    /// covered otherwise. Drives the season ranking's candidate groups
    /// (tasks.md 4.1), so a season with nothing scored is considered and
    /// then correctly omitted rather than never being looked at.</summary>
    public List<(int Year, string Season)> SeasonPoints => Mode == RecapMode.Season
        ? [(StartYear, Season!)]
        : Years.SelectMany(year => SeasonOrder.Select(season => (year, season))).ToList();

    private static (DateOnly Start, DateOnly End) SeasonDateRange(int year, string season)
    {
        var startMonth = SeasonCalendar.GetSeasonIndex(season) * 3 + 1;
        var endMonth = startMonth + 2;
        return (new DateOnly(year, startMonth, 1), new DateOnly(year, endMonth, DateTime.DaysInMonth(year, endMonth)));
    }
}
