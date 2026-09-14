namespace AnimeTracker.Api.Services.Season;

/// <summary>Computes the navigable ceiling — the furthest season the user may
/// step or quick-jump to — from MAL's forward season horizon (design.md
/// "fix-season-horizon-and-tile-meta" decision 3). Pure: no DB, no MAL, fully
/// unit-testable.</summary>
public static class SeasonHorizon
{
    // MAL's published forward window: probed against the live API on
    // 2026-08-18 with the current season (summer 2026) — summer, fall 2026
    // and winter 2027 (current+2) all returned 200; spring 2027 (current+3)
    // and beyond returned 404. Expressed as a season count rather than a
    // fixed season, so it rolls forward with the calendar by itself and
    // never needs bumping when a new season opens.
    public const int FutureSeasonWindow = 2;

    /// <summary>The ceiling before today's 404 step-back — the forward window,
    /// raised to the latest cached season if that's further out. This is the
    /// upper end of the range the season and year endpoints accept
    /// (<see cref="SeasonRequestRange"/>): the seasons between it and
    /// <see cref="Resolve"/>'s result were all fetched today, so accepting
    /// them costs no MAL request.</summary>
    public static (int Year, string Season) ResolveOuter(
        (int Year, string Season) current,
        (int Year, string Season)? latestCachedSeason)
    {
        var currentIndex = SeasonCalendar.GetSeasonPointIndex(current.Year, current.Season);

        // MAL's published forward window.
        var candidateIndex = currentIndex + FutureSeasonWindow;

        // A wider window, once actually observed — a season past the default
        // window already has a cached listing — stays reachable instead of
        // being walled off.
        if (latestCachedSeason is { } latest)
            candidateIndex = Math.Max(candidateIndex, SeasonCalendar.GetSeasonPointIndex(latest.Year, latest.Season));

        return SeasonCalendar.FromSeasonPointIndex(candidateIndex);
    }

    public static (int Year, string Season) Resolve(
        (int Year, string Season) current,
        (int Year, string Season)? latestCachedSeason,
        Func<(int Year, string Season), bool> notListedToday)
    {
        var currentIndex = SeasonCalendar.GetSeasonPointIndex(current.Year, current.Season);

        var outer = ResolveOuter(current, latestCachedSeason);
        var candidateIndex = SeasonCalendar.GetSeasonPointIndex(outer.Year, outer.Season);

        // Today's observed horizon: retreat past any trailing season MAL
        // answered 404 for on the current local date. The check is scoped to
        // "today" only (via notListedToday) — an older 404 never constrains
        // the ceiling — so each new local day the ceiling springs back up and
        // re-probes the boundary season on the next visit. That's what keeps
        // this from ever deadlocking: a season walled off permanently could
        // never be re-fetched to discover MAL had opened it.
        while (candidateIndex > currentIndex && notListedToday(SeasonCalendar.FromSeasonPointIndex(candidateIndex)))
            candidateIndex--;

        return SeasonCalendar.FromSeasonPointIndex(candidateIndex);
    }
}
