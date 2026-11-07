namespace AnimeTracker.Api.Services.Search;

/// <summary>Reads what was typed into a search field, the one way every search reads
/// it, so the anime search and the openings-and-endings search can never come to
/// disagree about what a query means (improve-search-page design.md D7).</summary>
internal static class SearchQuery
{
    /// <summary>Trims the query and strips a surrounding pair of double quotes
    /// (length &gt;= 2, first and last char both `"`) to signal an exact match instead
    /// of a substring match. Shared by every search entry point. The returned term is
    /// raw: it is normalized (<see cref="SearchTextMatch.Normalize"/>) where it is
    /// matched, and a term that normalizes to nothing matches nothing.</summary>
    public static (string Term, bool Exact) Parse(string query)
    {
        var trimmed = query.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
            return (trimmed[1..^1], true);

        return (trimmed, false);
    }
}
