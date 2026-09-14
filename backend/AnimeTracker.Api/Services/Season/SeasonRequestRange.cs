namespace AnimeTracker.Api.Services.Season;

/// <summary>The [EarliestYear, LatestYear/LatestSeason] range of seasons and
/// years the season and year endpoints accept — winter EarliestYear to the
/// outer ceiling. Both ends are accepted. The earliest end is always winter,
/// so a year at or above EarliestYear needs no season test there. The caller
/// must already have checked the season name passed to
/// <see cref="ContainsSeason"/>, since SeasonCalendar.GetSeasonIndex returns
/// -1 for an unknown one.</summary>
public sealed record SeasonRequestRange(int EarliestYear, int LatestYear, string LatestSeason)
{
    public bool ContainsYear(int year) => year >= EarliestYear && year <= LatestYear;

    // Year bounds first: GetSeasonPointIndex is year * 4, which wraps in the
    // project's default unchecked arithmetic for a large enough {year:int}
    // (1073743741 * 4 lands on winter 1917's index), so a point-index
    // comparison alone could accept a bogus year.
    public bool ContainsSeason(int year, string season) =>
        ContainsYear(year) &&
        (year < LatestYear || SeasonCalendar.GetSeasonIndex(season) <= SeasonCalendar.GetSeasonIndex(LatestSeason));
}
