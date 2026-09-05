namespace AnimeTracker.Api.Services.Search;

/// <summary>Anime search: the navbar type-ahead (<see cref="SearchAsync"/>) merges
/// the local metadata cache with a live MAL search and ranks prefix matches by
/// popularity; the full search page (<see cref="SearchPageAsync"/>) is a
/// paginated, sortable view sourced from MAL's own relevance ordering.</summary>
public interface IAnimeSearchService
{
    /// <summary>Type-ahead search, answered in one of two stages (design.md D1).
    /// With <paramref name="includeLive"/> false, this is the cache-only stage:
    /// the same ranking, the same 5-row budget, and the same 2-series cap as the
    /// merged stage, drawn from stored anime and stored series only — no MAL call
    /// in the path. With it true, MAL candidates are merged in as they are
    /// today.</summary>
    Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, bool includeLive, CancellationToken ct = default);

    Task<SearchPageDto> SearchPageAsync(string query, string sortKey, int offset, int limit, CancellationToken ct = default);
}
