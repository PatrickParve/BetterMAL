namespace AnimeTracker.Api.Services.Series;

/// <summary>The relation types that mean "this is the same story" — a series
/// is the connected component of <c>AnimeRelatedAnime</c> reached by
/// traversing only these, plus the narrow music-linking case of <c>other</c>
/// handled by <see cref="IsTraversableMusicEdge"/> (design.md decision 1).
/// <c>alternative_setting</c> and <c>character</c> are deliberately excluded:
/// they're how MAL links shows that merely share a universe or a cast, and
/// traversing them would fuse entire multi-decade franchises — and, through
/// shared crossover shows, occasionally unrelated ones — into a single blob.
/// Every other relation MAL reports (those two, plus <c>adaptation</c> and any
/// unrecognized string, and <c>other</c> outside its music case) is stored on
/// <c>AnimeRelatedAnime</c> as always, just never traversed here.</summary>
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

    /// <summary>True when an <c>other</c> edge should be traversed because
    /// exactly one of its two ends is a cached <c>music</c> entry — MAL's
    /// only way of linking a franchise's theme/image songs to the show they
    /// belong to. Both-music and neither-music <c>other</c> edges are
    /// rejected: <c>other</c> is also how MAL links commercials, promotional
    /// videos, pilots, and crossovers, so traversing it unconditionally would
    /// fuse unrelated franchises, and a cover/remix link between two songs
    /// already travels over <c>alternative_version</c> (design.md decision
    /// 1). Stated over the two ends rather than the direction the relation is
    /// stored in, so a series built from the song and one built from the show
    /// produce the same component.</summary>
    public static bool IsTraversableMusicEdge(string? oneEndMediaType, string? otherEndMediaType) =>
        (oneEndMediaType == "music") != (otherEndMediaType == "music");
}
