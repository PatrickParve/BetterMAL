namespace AnimeTracker.Api.Services.Search;

/// <summary>Anime search: the navbar type-ahead (<see cref="SearchAsync"/>) merges
/// the local metadata cache with a live MAL search and ranks prefix matches by
/// popularity; the full search page (<see cref="SearchPageAsync"/>) is a
/// paginated, sortable view sourced from MAL's own relevance ordering.</summary>
public interface IAnimeSearchService
{
    Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, CancellationToken ct = default);

    Task<SearchPageDto> SearchPageAsync(string query, string sortKey, int offset, int limit, CancellationToken ct = default);
}
