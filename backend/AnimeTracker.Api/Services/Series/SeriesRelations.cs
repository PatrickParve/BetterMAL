namespace AnimeTracker.Api.Services.Series;

/// <summary>The relation types that mean "this is the same story" — a series
/// is the connected component of <c>AnimeRelatedAnime</c> reached by
/// traversing only these (design.md decision 1). <c>alternative_setting</c>
/// and <c>character</c> are deliberately excluded: they're how MAL links
/// shows that merely share a universe or a cast, and traversing them would
/// fuse entire multi-decade franchises — and, through shared crossover
/// shows, occasionally unrelated ones — into a single blob. Every other
/// relation MAL reports (those two, plus <c>other</c>, <c>adaptation</c>, and
/// any unrecognized string) is stored on <c>AnimeRelatedAnime</c> as always,
/// just never traversed here.</summary>
public static class SeriesRelations
{
    // Concrete HashSet<string>, not IReadOnlySet<string>: EF Core's query
    // translator recognizes Contains() on concrete collection types (the
    // same pattern as a List<int> membership check) but not on the
    // IReadOnlySet<T> interface, which fails to translate when this field is
    // used inside a LINQ-to-Entities Where clause (SeriesGraphBuilder's
    // incoming-edge query).
    public static readonly HashSet<string> TraversalSet = new()
    {
        "sequel",
        "prequel",
        "side_story",
        "parent_story",
        "summary",
        "full_story",
        "spin_off",
        "alternative_version",
    };

    public static bool IsTraversable(string? relationType) =>
        relationType is not null && TraversalSet.Contains(relationType);
}
