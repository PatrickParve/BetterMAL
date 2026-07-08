using System.Text.RegularExpressions;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Mal;

namespace AnimeTracker.Api.Services.Search;

public partial class AnimeSearchService(
    IAnimeMetadataRepository repository,
    IMalClient malClient,
    ILogger<AnimeSearchService> logger) : IAnimeSearchService
{
    public async Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        query = query.Trim();
        if (query.Length == 0)
            return [];

        var index = await repository.GetSearchIndexAsync(ct);
        var localMatches = index
            .Where(a => MatchesWordBoundaryPrefix(a.Title, query))
            .OrderByDescending(a => a.Title.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .Select(a => new AnimeSearchResultDto(a.Id, a.Title, a.EnglishTitle, a.PictureUrl))
            .ToList();

        if (localMatches.Count > 0)
            return localMatches;

        // Nothing cached locally — fall back to MAL's own (fuzzier) live search.
        // A live-search hiccup (network blip, transient MAL error) shouldn't
        // take down the search box — degrade to "no matches" instead.
        try
        {
            var live = await malClient.SearchAnimeAsync(query, limit, ct);
            return live.Data
                .Take(limit)
                .Select(edge => new AnimeSearchResultDto(
                    edge.Node.Id,
                    edge.Node.Title,
                    edge.Node.AlternativeTitles?.En,
                    edge.Node.MainPicture?.Medium ?? edge.Node.MainPicture?.Large))
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Live MAL search fallback failed for query {Query}.", query);
            return [];
        }
    }

    /// <summary>Matches if the query is a prefix of the title itself or of any
    /// word within it (so "titan" matches "Attack on Titan," not just "attack").</summary>
    private static bool MatchesWordBoundaryPrefix(string title, string query) =>
        WordSplitter().Split(title).Any(word => word.StartsWith(query, StringComparison.OrdinalIgnoreCase));

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+")]
    private static partial Regex WordSplitter();
}
