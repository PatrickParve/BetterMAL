namespace AnimeTracker.Api.Services.Series;

/// <summary>How a series member relates to the series' story component
/// (rebuild-series-by-story-component design.md decision D2), stored on
/// <see cref="Models.SeriesMember.MembershipKind"/> as this enum's name, the
/// same string round-tripping <see cref="RelationGroup"/> uses.</summary>
public enum MembershipKind
{
    /// <summary>A member of the story component itself.</summary>
    Core,

    /// <summary>A version neighbour with no story relations of its own; folds
    /// in as an ordinary extra whose tile opens its detail page.</summary>
    FoldedVersion,

    /// <summary>A version neighbour that has story relations of its own;
    /// counted as an extra here, but its tile opens its own series page.</summary>
    NeighbourTelling,
}
