namespace AnimeTracker.Api.Services.Series;

/// <summary>The relation types that mean "this is the same story" — a series
/// is the connected component of <c>AnimeRelatedAnime</c> reached by
/// traversing <see cref="TraversalSet"/>, plus the narrow companion-media case
/// of <c>other</c> handled by <see cref="IsTraversableOtherEdge"/> (design.md
/// decision 1; widened to include <c>pv</c> by
/// polish-rewatch-more-and-filters design.md D5a). <c>character</c> is
/// deliberately excluded: it's how MAL links shows that merely share a cast,
/// and traversing it would fuse entire multi-decade franchises — and, through
/// shared crossover shows, occasionally unrelated ones — into a single blob.
/// Every other relation MAL reports (<c>character</c>, <c>adaptation</c>, any
/// unrecognized string, and <c>other</c> outside its companion-media case) is
/// stored on <c>AnimeRelatedAnime</c> as always, just never traversed
/// here.</summary>
public static class SeriesRelations
{
    // Concrete HashSet<string>, not IReadOnlySet<string>: EF Core's query
    // translator recognizes Contains() on concrete collection types (the
    // same pattern as a List<int> membership check) but not on the
    // IReadOnlySet<T> interface, which fails to translate when this field is
    // used inside a LINQ-to-Entities Where clause (SeriesGraphBuilder's
    // incoming-edge query).

    /// <summary>Relations that mean "a different telling of the same
    /// franchise" rather than "the same story" — no longer part of
    /// <see cref="TraversalSet"/>, so they neither grow nor split a
    /// component. Instead they're followed one hop, discovery-only, from a
    /// story component's members to find its version neighbours, which are
    /// classified (design.md decision D2) but never traversed through
    /// (rebuild-series-by-story-component design.md decisions D1/D3).</summary>
    public static readonly HashSet<string> VersionRelations = new() { "alternative_version", "alternative_setting" };

    /// <summary>Relations that mean "the same story, the same telling" — a
    /// series is the connected component reached by traversing this set
    /// alone; <see cref="VersionRelations"/> neither grows nor splits it
    /// (rebuild-series-by-story-component design.md decision D1).</summary>
    public static readonly HashSet<string> StoryTraversalSet = new()
    {
        "sequel",
        "prequel",
        "side_story",
        "parent_story",
        "summary",
        "full_story",
        "spin_off",
    };

    /// <summary>Alias of <see cref="StoryTraversalSet"/> — the name
    /// <c>SeriesGraphBuilder</c>'s traversal and <c>SeriesService</c>'s
    /// related-entry projection already use for "the set a build's component
    /// traversal follows". <see cref="VersionRelations"/> used to be folded
    /// into this set too, so a single build discovered every telling of a
    /// franchise at once; narrowing it back to story relations alone is what
    /// makes a series a story component again (design.md decision
    /// D1).</summary>
    public static readonly HashSet<string> TraversalSet = StoryTraversalSet;

    /// <summary>Union of <see cref="TraversalSet"/> and <see cref="VersionRelations"/>
    /// — what <see cref="TraversalSet"/> itself used to mean before it
    /// narrowed back to story relations alone. <see cref="Services.Metadata.AdjacentAnimeSet"/>
    /// is the one reader that still wants the wider set: metadata-refresh
    /// adjacency must keep reaching a listed anime's alternative versions,
    /// not just its same-story neighbours, so the narrowing above must not
    /// silently stop refreshing them (design.md Risks/Trade-offs).</summary>
    public static readonly HashSet<string> TraversalOrVersionRelations = new(TraversalSet.Concat(VersionRelations));

    // The media types an `other` edge is traversed for (design.md D5a, tasks
    // 4.1): a franchise's theme/image songs and promotional videos, which MAL
    // links to the show with `other` and nothing else. `cm` (commercial) is
    // deliberately excluded — traversing it would pull ads into a series —
    // and this being a named, concrete HashSet<string> (not a computed
    // literal each call) is what lets SeriesGraphBuilder's incoming-edge LINQ
    // query translate a Contains() check the same way TraversalSet does.
    public static readonly HashSet<string> CompanionMediaTypes = new() { "music", "pv" };

    public static bool IsTraversable(string? relationType) =>
        relationType is not null && TraversalSet.Contains(relationType);

    /// <summary>True when an <c>other</c> edge should be traversed because
    /// exactly one of its two ends is a cached companion-media entry (<see
    /// cref="CompanionMediaTypes"/>: <c>music</c> or <c>pv</c>) — MAL's only
    /// way of linking a franchise's theme/image songs and promotional videos
    /// to the show they belong to. Both-companion and neither-companion
    /// <c>other</c> edges are rejected: <c>other</c> is also how MAL links
    /// commercials, pilots, and crossovers, so traversing it unconditionally
    /// would fuse unrelated franchises, and a cover/remix link between two
    /// songs already travels over <c>alternative_version</c> (design.md
    /// decision 1). Stated over the two ends rather than the direction the
    /// relation is stored in, so a series built from the companion entry and
    /// one built from the show produce the same component.</summary>
    public static bool IsTraversableOtherEdge(string? oneEndMediaType, string? otherEndMediaType) =>
        IsCompanionMedia(oneEndMediaType) != IsCompanionMedia(otherEndMediaType);

    private static bool IsCompanionMedia(string? mediaType) =>
        mediaType is not null && CompanionMediaTypes.Contains(mediaType);

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

    /// <summary>The <see cref="RelationGroup"/> an extra belongs to given one
    /// relation edge between it and a main-line member, read directionally
    /// from the extra's own side (split-series-by-version design.md decision
    /// 4): <c>M --summary--> X</c> makes X a <see cref="RelationGroup.Summary"/>,
    /// while <c>X --summary--> M</c> makes X the
    /// <see cref="RelationGroup.FullStory"/> — mirroring how
    /// <see cref="FindRecapIds"/> and <see cref="FindSideContentIds"/> already
    /// read <c>summary</c>/<c>full_story</c> and <c>side_story</c>/<c>parent_story</c>
    /// as directional pairs. <paramref name="extraIsOwner"/> is true when the
    /// stored edge is <c>extra --relationType--> mainLineMember</c>, false when
    /// it's <c>mainLineMember --relationType--> extra</c>. Relations with no
    /// mirror name — the two version relations, <c>spin_off</c>, <c>character</c>,
    /// <c>adaptation</c> — resolve the same group regardless of direction.
    /// Any relation type not recognised here (including a non-companion
    /// <c>other</c>) resolves to <see cref="RelationGroup.Other"/>.</summary>
    public static RelationGroup ResolveDirectional(string relationType, bool extraIsOwner) => relationType switch
    {
        "alternative_version" => RelationGroup.AlternativeVersion,
        "alternative_setting" => RelationGroup.AlternativeSetting,
        "sequel" => extraIsOwner ? RelationGroup.Prequel : RelationGroup.Sequel,
        "prequel" => extraIsOwner ? RelationGroup.Sequel : RelationGroup.Prequel,
        "side_story" => extraIsOwner ? RelationGroup.ParentStory : RelationGroup.SideStory,
        "parent_story" => extraIsOwner ? RelationGroup.SideStory : RelationGroup.ParentStory,
        "summary" => extraIsOwner ? RelationGroup.FullStory : RelationGroup.Summary,
        "full_story" => extraIsOwner ? RelationGroup.Summary : RelationGroup.FullStory,
        "spin_off" => RelationGroup.SpinOff,
        "character" => RelationGroup.Character,
        "adaptation" => RelationGroup.Adaptation,
        _ => RelationGroup.Other,
    };

    /// <summary>The single <see cref="RelationGroup"/> for an id related to
    /// one or more main-line members by <paramref name="edgesToMainLine"/> —
    /// the highest-precedence group among them, in <see cref="SeriesRelationGroupOrder"/>'s
    /// fixed display order (design.md decision 4: "its group SHALL be the
    /// highest-precedence such relation in the order above"). Shared by
    /// <c>SeriesGraphBuilder</c>'s extra-group resolution and
    /// <c>SeriesService</c>'s related-entry projection, so an id reached by
    /// more than one relation resolves the same group on both.</summary>
    public static RelationGroup HighestPrecedenceGroup(IEnumerable<(string RelationType, bool ExtraIsOwner)> edgesToMainLine) =>
        edgesToMainLine
            .Select(e => ResolveDirectional(e.RelationType, e.ExtraIsOwner))
            .OrderBy(SeriesRelationGroupOrder.GroupOf)
            .First();

    /// <summary>The single <see cref="RelationGroup"/> for an id related to
    /// one or more other members of its telling by a version relation —
    /// <see cref="VersionRelations"/> — alone (fix-alternative-version-grouping
    /// design.md decision D4, spec's extras-grouping rule 2): the
    /// highest-precedence group among just <paramref name="edgesToMember"/>'s
    /// version-relation edges, by the same <see cref="SeriesRelationGroupOrder"/>
    /// <see cref="HighestPrecedenceGroup"/> uses — Alternative version (order
    /// 0) precedes Alternative setting (order 1), so an id carrying both
    /// resolves Alternative version. Returns null when none of
    /// <paramref name="edgesToMember"/> is a version relation, so a caller
    /// can fall through to its next tier rather than resolving Other by way
    /// of this helper. <see cref="ResolveDirectional"/> needs no direction
    /// here either — the two version relations already resolve the same
    /// group regardless of which end declared the edge.</summary>
    public static RelationGroup? HighestPrecedenceVersionGroup(IEnumerable<(string RelationType, bool ExtraIsOwner)> edgesToMember)
    {
        var versionGroups = edgesToMember
            .Where(e => VersionRelations.Contains(e.RelationType))
            .Select(e => ResolveDirectional(e.RelationType, e.ExtraIsOwner))
            .ToList();

        return versionGroups.Count > 0 ? versionGroups.OrderBy(SeriesRelationGroupOrder.GroupOf).First() : null;
    }
}
