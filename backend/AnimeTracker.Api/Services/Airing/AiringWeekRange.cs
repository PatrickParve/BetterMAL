using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Airing;

/// <summary>The airing schedule's accepted date range: 1 January
/// EarliestArchiveYear through 31 December of the year after the current
/// one (design D8) — the same range the frontend's year selector and week
/// arrows offer. Computed one local day ahead of localToday, for the same
/// reason as <see cref="Recap.RecapRange"/>: the range must cover whatever a
/// browser already calls current.</summary>
public static class AiringWeekRange
{
    /// <summary>Mirrors the frontend's AIRING_YEARS_AHEAD (AiringPage.tsx).</summary>
    public const int YearsAhead = 1;

    public static readonly DateOnly Earliest = new(SeasonCalendar.EarliestArchiveYear, 1, 1);

    public static DateOnly Latest(DateOnly localToday) => new(localToday.AddDays(1).Year + YearsAhead, 12, 31);

    public static bool Contains(DateOnly date, DateOnly localToday) =>
        date >= Earliest && date <= Latest(localToday);
}
