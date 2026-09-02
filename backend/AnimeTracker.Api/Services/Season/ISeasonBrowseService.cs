namespace AnimeTracker.Api.Services.Season;

/// <summary>Backs the Season page: all anime airing in a given season (not
/// just my list). Reads are cache-first and never touch MAL; a refresh is
/// triggered only by a client visiting a season, never by a schedule or a
/// user-facing control, and runs at most once per season per local day.</summary>
public interface ISeasonBrowseService
{
    /// <summary>Repository-only read — never calls MAL. Returns the season's
    /// whole listing plus when it was last fetched (null if never).</summary>
    Task<SeasonPageDto> GetPageAsync(int year, string season, bool hideHentai, CancellationToken ct = default);

    /// <summary>Fetches this season from MAL if it hasn't already been fetched
    /// successfully today, subject to a per-season single-flight guard.
    /// Failures are swallowed and logged; the cached listing is left as-is.</summary>
    Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default);

    /// <summary>The furthest season navigable from the current one — see
    /// SeasonHorizon.Resolve. Repository-only read, never calls MAL.</summary>
    Task<SeasonBoundsDto> GetBoundsAsync(CancellationToken ct = default);

    /// <summary>Repository-only read of a year — the union of its four
    /// seasons' whole listings, as one. Never calls MAL.</summary>
    Task<YearPageDto> GetYearPageAsync(int year, bool hideHentai, CancellationToken ct = default);

    /// <summary>Refreshes a year's four seasons from MAL, sequentially and
    /// subject to each season's own once-per-day/single-flight rules (design
    /// D2), and folds their four outcomes into one.</summary>
    Task<YearRefreshResultDto> RefreshYearAsync(int year, CancellationToken ct = default);
}
