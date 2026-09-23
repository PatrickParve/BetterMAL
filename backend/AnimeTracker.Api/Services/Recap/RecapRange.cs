using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Recap;

/// <summary>The recap's accepted period range: winter EarliestArchiveYear
/// through the current season (design D6). Computed one local day ahead of
/// localToday, because the accepted range must cover whatever a browser
/// already calls current — every inhabited time zone is at most about 12
/// hours ahead of Helsinki, so a client's clock can already be a calendar
/// day past the server's before the server's own "today" catches up.</summary>
public static class RecapRange
{
    public static SeasonRequestRange For(DateOnly localToday)
    {
        var (year, season) = SeasonCalendar.GetSeasonFor(localToday.AddDays(1));
        return new SeasonRequestRange(SeasonCalendar.EarliestArchiveYear, year, season);
    }
}
