namespace AnimeTracker.Api.Models;

/// <summary>One anime's membership in a <see cref="Series"/> — per (series,
/// anime) rather than per anime, since split-series-by-version lets an anime
/// belong to more than one series: its own telling, plus any other telling
/// it's a shared or boundary member of (split-series-by-version design.md
/// decisions 1-2). Keyed on <c>(SeriesId, AnimeId)</c>. <c>SeriesId</c> holds
/// the series' root entry's MAL id, so a membership row names the franchise
/// it belongs to without a join.</summary>
public class SeriesMember
{
    public int SeriesId { get; set; } // PK (part 1)
    public int AnimeId { get; set; } // PK (part 2)
    public bool IsMainLine { get; set; }
    public int Order { get; set; } // position within the main line, or within its extras relation group

    /// <summary>The extra's relationship to this series' main line —
    /// <see cref="Services.Series.RelationGroup"/>'s name, e.g. "AlternativeVersion",
    /// "SideStory" (split-series-by-version design.md decision 4). Null for a
    /// main-line member, which has no group of its own.</summary>
    public string? RelationGroup { get; set; }

    /// <summary>True on exactly one of an anime's memberships — the series
    /// every "the series for this anime" lookup resolves to (split-series-by-version
    /// design.md decision 2). The primary is the series whose anchor this
    /// membership is nearest to; a boundary member's primary is always its
    /// own series, never a series it is only a boundary of.</summary>
    public bool IsPrimary { get; set; }

    /// <summary>"Core", "FoldedVersion" or "NeighbourTelling" — this
    /// membership's relationship to the series' story component
    /// (rebuild-series-by-story-component design.md decision D2), round-tripping
    /// <see cref="Services.Series.MembershipKind"/> the same way
    /// <see cref="RelationGroup"/> round-trips its own enum. Defaults to
    /// "Core" since every membership recorded before this column existed
    /// was, in fact, a core one (design.md Migration Plan step 1).</summary>
    public string MembershipKind { get; set; } = "Core";

    /// <summary>The version slot this main-line member is an alternative of,
    /// keyed on the lowest MAL id among the slot's alternatives; null for a
    /// trunk entry or an extra (design.md decision D4).</summary>
    public int? VersionSlotKey { get; set; }

    /// <summary>The alternative anime id whose branch this main-line member
    /// belongs to — set on every alternative of a slot and its branch's
    /// members, null on trunk and on extras (design.md decision D4).</summary>
    public int? BranchHeadAnimeId { get; set; }

    public AnimeMetadata Anime { get; set; } = null!;
}
