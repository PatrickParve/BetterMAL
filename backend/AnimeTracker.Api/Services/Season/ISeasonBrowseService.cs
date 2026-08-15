namespace AnimeTracker.Api.Services.Season;

/// <summary>Backs the Season page: all anime airing in a given season (not
/// just my list). Reads are cache-first and never touch MAL; a refresh is
/// triggered only by a client visiting a season, never by a schedule or a
/// user-facing control, and runs at most once per season per local day.</summary>
public interface ISeasonBrowseService
{
    /// <summary>Repository-only read — never calls MAL. Returns the page plus
    /// when this season was last fetched (null if never).</summary>
    Task<SeasonPageDto> GetPageAsync(int year, string season, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default);

    /// <summary>Fetches this season from MAL if it hasn't already been fetched
    /// successfully today, subject to a per-season single-flight guard.
    /// Failures are swallowed and logged; the cached listing is left as-is.</summary>
    Task<SeasonRefreshResultDto> RefreshAsync(int year, string season, CancellationToken ct = default);
}
