using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Search;

public class AnimeSearchService(
    IAnimeMetadataRepository repository,
    IMalClient malClient,
    AnimeTrackerDbContext db,
    ILogger<AnimeSearchService> logger) : IAnimeSearchService
{
    /// <summary>Type-ahead candidate: local cache and live MAL results are
    /// merged into this shape before ranking, so the two sources compete on
    /// equal footing (same fields, same popularity ordering).</summary>
    private sealed record SearchCandidate(int Id, string Title, string? EnglishTitle, string? PictureUrl, int? PopularityRank);

    // MAL popularity is a rank (1 = most popular); 0 or null means "unranked" and
    // must sort last rather than ahead of rank 1.
    private static int PopularityKey(int? rank) => rank is null or 0 ? int.MaxValue : rank.Value;

    public async Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var (term, exact) = ParseQuery(query);
        if (term.Length == 0)
            return [];

        var index = await repository.GetSearchIndexAsync(ct);
        var localCandidates = index.Select(a => new SearchCandidate(a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.PopularityRank));

        // A live-search hiccup (network blip, transient MAL error) shouldn't
        // take down the search box — degrade to "local results only" instead.
        var malEdges = await SearchMalAsync(term, 25, ct);
        var malCandidates = malEdges.Select(edge => new SearchCandidate(
            edge.Node.Id,
            edge.Node.Title,
            NullIfWhitespace(edge.Node.AlternativeTitles?.En),
            edge.Node.MainPicture?.Medium ?? edge.Node.MainPicture?.Large,
            edge.Node.Popularity));

        var merged = MergeById(localCandidates, malCandidates);

        IEnumerable<SearchCandidate> ranked;
        if (exact)
        {
            ranked = merged
                .Where(c => EqualsIgnoreCase(c.Title, term) || EqualsIgnoreCase(c.EnglishTitle, term))
                .OrderBy(c => PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var matches = merged
                .Where(c => ContainsIgnoreCase(c.Title, term) || ContainsIgnoreCase(c.EnglishTitle, term))
                .ToList();

            var prefix = matches
                .Where(c => StartsWithIgnoreCase(c.Title, term) || StartsWithIgnoreCase(c.EnglishTitle, term))
                .OrderBy(c => PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);
            var rest = matches
                .Where(c => !StartsWithIgnoreCase(c.Title, term) && !StartsWithIgnoreCase(c.EnglishTitle, term))
                .OrderBy(c => PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);

            ranked = prefix.Concat(rest);
        }

        return ranked
            .Take(limit)
            .Select(c => new AnimeSearchResultDto(c.Id, c.Title, c.EnglishTitle, c.PictureUrl))
            .ToList();
    }

    /// <summary>Full search-page candidate: carries MAL's relevance index so
    /// the default sort can preserve MAL's own search ordering.</summary>
    private sealed record SearchPageCandidate(
        int AnimeId,
        string Title,
        string? EnglishTitle,
        string? PictureUrl,
        int? TotalEpisodes,
        string? MediaType,
        double? MalScore,
        int? PopularityRank,
        int RelevanceIndex);

    public async Task<SearchPageDto> SearchPageAsync(string query, string sortKey, int offset, int limit, CancellationToken ct = default)
    {
        var (term, exact) = ParseQuery(query);
        if (term.Length == 0)
            return new SearchPageDto(query, [], offset, limit, 0);

        const int MaxResults = 100;
        var edges = await SearchMalAsync(term, MaxResults, ct);

        var candidates = edges
            .Select((edge, index) => new SearchPageCandidate(
                edge.Node.Id,
                edge.Node.Title,
                NullIfWhitespace(edge.Node.AlternativeTitles?.En),
                edge.Node.MainPicture?.Medium ?? edge.Node.MainPicture?.Large,
                edge.Node.NumEpisodes is null or 0 ? null : edge.Node.NumEpisodes,
                edge.Node.MediaType,
                edge.Node.Mean,
                edge.Node.Popularity,
                index))
            .AsEnumerable();

        if (exact)
        {
            candidates = candidates.Where(c => EqualsIgnoreCase(c.Title, term) || EqualsIgnoreCase(c.EnglishTitle, term));
        }

        var filtered = candidates.ToList();
        var totalCount = filtered.Count;

        var ids = filtered.Select(c => c.AnimeId).ToList();
        var myScores = await db.UserAnimeEntries.AsNoTracking()
            .Where(e => ids.Contains(e.AnimeId))
            .Select(e => new { e.AnimeId, e.MyScore })
            .ToDictionaryAsync(e => e.AnimeId, e => e.MyScore, ct);

        IEnumerable<SearchPageCandidate> sorted = sortKey switch
        {
            "popularity" => filtered.OrderBy(c => PopularityKey(c.PopularityRank)).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "malScore" => filtered.OrderByDescending(c => c.MalScore ?? -1).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "alphabetical" => filtered.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "myScore" => filtered.OrderByDescending(c => myScores.GetValueOrDefault(c.AnimeId) ?? -1).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(c => c.RelevanceIndex), // "relevance": MAL's own search order.
        };

        var items = sorted
            .Skip(offset)
            .Take(limit)
            .Select(c => new SearchAnimeItemDto(
                c.AnimeId, c.Title, c.EnglishTitle, c.PictureUrl, c.TotalEpisodes, c.MediaType, c.MalScore, c.PopularityRank,
                myScores.GetValueOrDefault(c.AnimeId)))
            .ToList();

        return new SearchPageDto(query, items, offset, limit, totalCount);
    }

    /// <summary>Trims the query and strips a surrounding pair of double quotes
    /// (length &gt;= 2, first and last char both `"`) to signal an exact-title
    /// match instead of a substring match. Shared by both search entry points.</summary>
    private static (string Term, bool Exact) ParseQuery(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return (trimmed[1..^1], true);

        return (trimmed, false);
    }

    /// <summary>Live MAL search, degraded to an empty list on failure — a MAL
    /// hiccup (network blip, transient error) should never take down search.</summary>
    private async Task<List<MalAnimeListEdge>> SearchMalAsync(string term, int limit, CancellationToken ct)
    {
        try
        {
            var response = await malClient.SearchAnimeAsync(term, limit, ct);
            return response.Data;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live MAL search failed for query {Query}.", term);
            return [];
        }
    }

    /// <summary>De-dupes local + MAL candidates by anime id, preferring
    /// whichever side has the more complete data for each field.</summary>
    private static List<SearchCandidate> MergeById(IEnumerable<SearchCandidate> local, IEnumerable<SearchCandidate> mal)
    {
        var merged = new Dictionary<int, SearchCandidate>();
        foreach (var candidate in local.Concat(mal))
        {
            merged[candidate.Id] = merged.TryGetValue(candidate.Id, out var existing)
                ? existing with
                {
                    EnglishTitle = existing.EnglishTitle ?? candidate.EnglishTitle,
                    PictureUrl = existing.PictureUrl ?? candidate.PictureUrl,
                    PopularityRank = existing.PopularityRank ?? candidate.PopularityRank,
                }
                : candidate;
        }

        return merged.Values.ToList();
    }

    private static string? NullIfWhitespace(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool EqualsIgnoreCase(string? value, string term) =>
        value is not null && string.Equals(value, term, StringComparison.OrdinalIgnoreCase);

    private static bool ContainsIgnoreCase(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool StartsWithIgnoreCase(string? value, string term) =>
        value is not null && value.StartsWith(term, StringComparison.OrdinalIgnoreCase);
}
