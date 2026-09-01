using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Library;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Services.Search;

public class AnimeSearchService(
    IAnimeMetadataRepository repository,
    IMalClient malClient,
    AnimeTrackerDbContext db,
    SeriesSearchLookup seriesSearchLookup,
    ISeriesBuildTrigger seriesBuildTrigger,
    ILogger<AnimeSearchService> logger) : IAnimeSearchService
{
    // Dropdown's 5-row budget is shared with series (design.md decision 3);
    // the results page prepends up to 3.
    private const int MaxSeriesRowsInDropdown = 2;
    private const int MaxSeriesRowsOnPage = 3;

    // A half-typed query shouldn't queue a build for whatever it happens to
    // prefix-match (design.md decision 6).
    private const int MinQueryLengthForSeriesBuildTrigger = 3;

    /// <summary>Type-ahead candidate: local cache and live MAL results are
    /// merged into this shape before ranking, so the two sources compete on
    /// equal footing (same fields, same popularity ordering).</summary>
    private sealed record SearchCandidate(int Id, string Title, string? EnglishTitle, string? PictureUrl, int? PopularityRank);

    public async Task<List<AnimeSearchResultDto>> SearchAsync(string query, int limit, CancellationToken ct = default)
    {
        var (term, exact) = ParseQuery(query);
        // A term that normalizes to nothing (e.g. "!!!") must match nothing,
        // not every title (design.md D2) — checked before any index load or
        // MAL call. Subsumes the old zero-length check since an empty term
        // normalizes to empty too.
        if (SearchTextMatch.Normalize(term).Length == 0)
            return [];

        var index = await repository.GetSearchIndexAsync(ct);
        var localCandidates = index.Select(a => new SearchCandidate(a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.PopularityRank));

        // A live-search hiccup (network blip, transient MAL error) shouldn't
        // take down the search box — degrade to "local results only" instead.
        // Failed is ignored here: the local index above is already merged in
        // regardless, so a MAL failure silently degrades on its own.
        var (malEdges, _) = await SearchMalAsync(term, 25, ct);
        var malCandidates = malEdges.Select(edge => new SearchCandidate(
            edge.Node.Id,
            edge.Node.Title,
            NullIfWhitespace(edge.Node.AlternativeTitles?.En),
            edge.Node.MainPicture?.Medium ?? edge.Node.MainPicture?.Large,
            edge.Node.Popularity));

        var merged = MergeById(localCandidates, malCandidates);

        // `term` is renormalized inside each SearchTextMatch call below
        // rather than once up front — the cost is a few thousand short
        // strings per request, negligible beside the index round trip this
        // method already pays (design.md D1). `term` itself stays raw so it
        // still reaches SearchMalAsync and ScheduleSeriesBuildForTopMatch
        // unchanged.
        IEnumerable<SearchCandidate> ranked;
        if (exact)
        {
            ranked = merged
                .Where(c => SearchTextMatch.EqualsNormalized(c.Title, term) || SearchTextMatch.EqualsNormalized(c.EnglishTitle, term))
                .OrderBy(c => SearchTextMatch.PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            var matches = merged
                .Where(c => SearchTextMatch.ContainsNormalized(c.Title, term) || SearchTextMatch.ContainsNormalized(c.EnglishTitle, term))
                .ToList();

            var prefix = matches
                .Where(c => SearchTextMatch.StartsWithNormalized(c.Title, term) || SearchTextMatch.StartsWithNormalized(c.EnglishTitle, term))
                .OrderBy(c => SearchTextMatch.PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);
            var rest = matches
                .Where(c => !SearchTextMatch.StartsWithNormalized(c.Title, term) && !SearchTextMatch.StartsWithNormalized(c.EnglishTitle, term))
                .OrderBy(c => SearchTextMatch.PopularityKey(c.PopularityRank))
                .ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase);

            ranked = prefix.Concat(rest);
        }

        var rankedAnime = ranked.ToList();

        var seriesIndex = await seriesSearchLookup.LoadAsync(ct);
        ScheduleSeriesBuildForTopMatch(term, rankedAnime.FirstOrDefault()?.Id, seriesIndex);

        var seriesRows = seriesIndex.Match(term, exact).Take(Math.Min(MaxSeriesRowsInDropdown, limit)).ToList();
        var animeRows = rankedAnime.Take(Math.Max(limit - seriesRows.Count, 0));

        return seriesRows.Select(AnimeSearchResultDto.ForSeries)
            .Concat(animeRows.Select(c => AnimeSearchResultDto.ForAnime(c.Id, c.Title, c.EnglishTitle, c.PictureUrl)))
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
        // See the matching guard in SearchAsync (design.md D2): a
        // punctuation-only term normalizes to nothing and must match
        // nothing.
        if (SearchTextMatch.Normalize(term).Length == 0)
            return new SearchPageDto(query, [], offset, limit, 0, [], false);

        const int MaxResults = 100;
        var (edges, malFailed) = await SearchMalAsync(term, MaxResults, ct);

        List<SearchPageCandidate> filtered;
        if (malFailed)
        {
            // The live search never ran, so — unlike the MAL path below —
            // candidates must be filtered by term locally (design.md D7).
            filtered = await BuildFallbackCandidatesAsync(term, exact, MaxResults, ct);
        }
        else
        {
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
                candidates = candidates.Where(c => SearchTextMatch.EqualsNormalized(c.Title, term) || SearchTextMatch.EqualsNormalized(c.EnglishTitle, term));
            }

            filtered = candidates.ToList();
        }

        // The frontend fetches this page once (offset 0) and reveals it client-side
        // in chunks, never re-fetching for more — so totalCount must reflect what
        // this single response can actually deliver, not MAL's full (up to
        // MaxResults) candidate count, or the displayed total would exceed what
        // scrolling could ever reveal.
        var totalCount = Math.Min(filtered.Count, limit);

        var seriesIndex = await seriesSearchLookup.LoadAsync(ct);
        // "Top-ranked" here means MAL's own relevance order, independent of
        // whichever sort the user has selected for display.
        var topRelevanceMatch = filtered.OrderBy(c => c.RelevanceIndex).FirstOrDefault();
        ScheduleSeriesBuildForTopMatch(term, topRelevanceMatch?.AnimeId, seriesIndex);

        // Series are defined over per-anime figures they don't have, so they
        // only make sense under the default relevance ordering (design.md
        // decision 4).
        var seriesResults = sortKey == "relevance"
            ? seriesIndex.Match(term, exact).Take(MaxSeriesRowsOnPage).ToList()
            : [];

        var ids = filtered.Select(c => c.AnimeId).ToList();
        var myScores = await db.UserAnimeEntries.AsNoTracking()
            .Where(e => ids.Contains(e.AnimeId))
            .Select(e => new { e.AnimeId, e.MyScore })
            .ToDictionaryAsync(e => e.AnimeId, e => e.MyScore, ct);

        IEnumerable<SearchPageCandidate> sorted = sortKey switch
        {
            "popularity" => filtered.OrderBy(c => SearchTextMatch.PopularityKey(c.PopularityRank)).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "malScore" => filtered.OrderByDescending(c => c.MalScore ?? -1).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "alphabetical" => filtered.OrderBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            "myScore" => filtered.OrderByDescending(c => myScores.GetValueOrDefault(c.AnimeId) ?? -1).ThenBy(c => c.Title, StringComparer.OrdinalIgnoreCase),
            _ => filtered.OrderBy(c => c.RelevanceIndex), // "relevance": MAL's own search order.
        };

        var items = sorted
            .Skip(offset)
            .Take(limit)
            .Select(c => new AnimeBrowseItemDto(
                c.AnimeId, c.Title, c.EnglishTitle, c.PictureUrl, c.TotalEpisodes, c.MediaType, c.MalScore, c.PopularityRank,
                myScores.GetValueOrDefault(c.AnimeId), myScores.ContainsKey(c.AnimeId)))
            .ToList();

        return new SearchPageDto(query, items, offset, limit, totalCount, seriesResults, malFailed);
    }

    /// <summary>Local-only substitute for a failed live MAL search (design.md
    /// D7): the same rows the type-ahead's local stage reads, filtered by term
    /// with the same predicates <see cref="SearchAsync"/> uses, capped to
    /// <paramref name="maxResults"/> before ranking since there's no MAL
    /// relevance order to defer to. RelevanceIndex is assigned by a local
    /// three-band ranking — exact title matches, then prefix matches, then
    /// the rest, each band ordered by popularity then title — so the
    /// "relevance" sort in <see cref="SearchPageAsync"/> needs no special
    /// case for where the candidates came from.</summary>
    private async Task<List<SearchPageCandidate>> BuildFallbackCandidatesAsync(string term, bool exact, int maxResults, CancellationToken ct)
    {
        var index = await repository.GetSearchFallbackIndexAsync(ct);

        var matching = exact
            ? index.Where(a => SearchTextMatch.EqualsNormalized(a.Title, term) || SearchTextMatch.EqualsNormalized(a.EnglishTitle, term))
            : index.Where(a => SearchTextMatch.ContainsNormalized(a.Title, term) || SearchTextMatch.ContainsNormalized(a.EnglishTitle, term));

        var truncated = matching.Take(maxResults).ToList();

        bool IsExactMatch(AnimeSearchFallbackProjection a) =>
            SearchTextMatch.EqualsNormalized(a.Title, term) || SearchTextMatch.EqualsNormalized(a.EnglishTitle, term);
        bool IsPrefixMatch(AnimeSearchFallbackProjection a) =>
            SearchTextMatch.StartsWithNormalized(a.Title, term) || SearchTextMatch.StartsWithNormalized(a.EnglishTitle, term);

        var exactBand = truncated.Where(IsExactMatch)
            .OrderBy(a => SearchTextMatch.PopularityKey(a.PopularityRank))
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase);
        var prefixBand = truncated.Where(a => !IsExactMatch(a) && IsPrefixMatch(a))
            .OrderBy(a => SearchTextMatch.PopularityKey(a.PopularityRank))
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase);
        var restBand = truncated.Where(a => !IsExactMatch(a) && !IsPrefixMatch(a))
            .OrderBy(a => SearchTextMatch.PopularityKey(a.PopularityRank))
            .ThenBy(a => a.Title, StringComparer.OrdinalIgnoreCase);

        return exactBand.Concat(prefixBand).Concat(restBand)
            .Select((a, index) => new SearchPageCandidate(
                a.Id, a.Title, a.EnglishTitle, a.PictureUrl, a.TotalEpisodes, a.MediaType, a.MalScore, a.PopularityRank, index))
            .ToList();
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

    /// <summary>Live MAL search. Degrades to an empty list with <c>Failed</c>
    /// true on failure, distinct from MAL legitimately answering with no
    /// matches (design.md D7) — the type-ahead (<see cref="SearchAsync"/>)
    /// ignores the flag since it already merges in the local index either
    /// way, but the results page (<see cref="SearchPageAsync"/>) uses it to
    /// fall back to a local-only search.</summary>
    private async Task<(List<MalAnimeListEdge> Edges, bool Failed)> SearchMalAsync(string term, int limit, CancellationToken ct)
    {
        try
        {
            var response = await malClient.SearchAnimeAsync(term, limit, ct);
            return (response.Data, false);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Live MAL search failed for query {Query}.", term);
            return ([], true);
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

    /// <summary>Fire-and-forget: schedules a background series build for the
    /// query's top-ranked anime match when it isn't already part of a stored
    /// series, so an unbuilt franchise becomes searchable on a later search
    /// (design.md decision 6). Enqueue itself never blocks or throws — it
    /// only queues an id — so nothing here can slow or fail the
    /// response.</summary>
    private void ScheduleSeriesBuildForTopMatch(string term, int? topAnimeId, SeriesSearchIndex seriesIndex)
    {
        if (term.Length < MinQueryLengthForSeriesBuildTrigger)
            return;
        if (topAnimeId is not { } animeId || seriesIndex.HasSeries(animeId))
            return;

        seriesBuildTrigger.Enqueue(animeId);
    }
}
