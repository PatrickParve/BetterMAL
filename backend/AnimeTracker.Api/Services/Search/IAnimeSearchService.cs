namespace AnimeTracker.Api.Services.Search;

/// <summary>Type-ahead search backing the navbar search box: local cache first
/// (word-boundary prefix match), live MAL search only when nothing matches locally.</summary>
public interface IAnimeSearchService
{
    Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, CancellationToken ct = default);
}
