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

    /// <summary>Ids within <paramref name="candidateIds"/> that another id in
    /// the same set tags as a recap/condensed retelling of — the
    /// <c>summary</c>/<c>full_story</c> pair (<c>A --summary--> B</c> means B
    /// recaps A; <c>B --full_story--> A</c> says the same from B's side).
    /// Shared by <c>SeriesGraphBuilder</c>'s main-line classification and the
    /// relation-confidence ranked prequel/sequel pick — same rule, same
    /// reason: MAL routinely also gives a recap a <c>sequel</c>/<c>prequel</c>
    /// edge to the season it bridges into, which would otherwise pull it into
    /// a sequel/prequel candidate set alongside the season it recaps. Only
    /// edges between two ids already in <paramref name="candidateIds"/> count.
    /// <paramref name="edges"/> need only include each candidate's own
    /// outgoing relations — an edge stored the other way round is covered
    /// because its owner is a candidate too.</summary>
    public static HashSet<int> FindRecapIds(
        IEnumerable<(int OwnerId, int RelatedAnimeId, string RelationType)> edges, HashSet<int> candidateIds)
    {
        var recapIds = new HashSet<int>();
        foreach (var (ownerId, relatedAnimeId, relationType) in edges)
        {
            if (!candidateIds.Contains(relatedAnimeId))
                continue;

            switch (relationType)
            {
                case "full_story":
                    recapIds.Add(ownerId); // this id is the recap of relatedAnimeId
                    break;
                case "summary":
                    recapIds.Add(relatedAnimeId); // the related id is the recap of this one
                    break;
            }
        }

        return recapIds;
    }

    /// <summary>Ids within <paramref name="candidateIds"/> that another id in
    /// the same set tags as side content of — the <c>side_story</c>/
    /// <c>parent_story</c> pair (<c>A --side_story--> B</c> means B is a side
    /// story of A; <c>B --parent_story--> A</c> says the same from B's side).
    /// Mirrors <see cref="FindRecapIds"/> one-for-one.</summary>
    public static HashSet<int> FindSideContentIds(
        IEnumerable<(int OwnerId, int RelatedAnimeId, string RelationType)> edges, HashSet<int> candidateIds)
    {
        var sideContentIds = new HashSet<int>();
        foreach (var (ownerId, relatedAnimeId, relationType) in edges)
        {
            if (!candidateIds.Contains(relatedAnimeId))
                continue;

            switch (relationType)
            {
                case "parent_story":
                    sideContentIds.Add(ownerId); // this id is side content of relatedAnimeId
                    break;
                case "side_story":
                    sideContentIds.Add(relatedAnimeId); // the related id is side content of this one
                    break;
            }
        }

        return sideContentIds;
    }
}
