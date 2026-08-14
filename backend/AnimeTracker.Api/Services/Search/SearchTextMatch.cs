namespace AnimeTracker.Api.Services.Search;

/// <summary>Title-matching and popularity-ranking helpers shared by anime and
/// series search, so both match a query the same way (design.md decision
/// 1).</summary>
internal static class SearchTextMatch
{
    public static bool EqualsIgnoreCase(string? value, string term) =>
        value is not null && string.Equals(value, term, StringComparison.OrdinalIgnoreCase);

    public static bool ContainsIgnoreCase(string? value, string term) =>
        value is not null && value.Contains(term, StringComparison.OrdinalIgnoreCase);

    public static bool StartsWithIgnoreCase(string? value, string term) =>
        value is not null && value.StartsWith(term, StringComparison.OrdinalIgnoreCase);

    // MAL popularity is a rank (1 = most popular); 0 or null means "unranked"
    // and must sort last rather than ahead of rank 1.
    public static int PopularityKey(int? rank) => rank is null or 0 ? int.MaxValue : rank.Value;
}
