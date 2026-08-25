namespace AnimeTracker.Api.Services.Relations;

/// <summary>How much a stored relation edge can be trusted (relation-confidence
/// spec). Ranked highest-to-lowest for the purposes of the ranked prequel/sequel
/// pick: Confirmed above Corroborated, Unconfirmed, and Unknown; Contradicted
/// is never a rank candidate at all — it's discarded before ranking runs.</summary>
public enum RelationConfidence
{
    /// <summary>Both ends agree — MAL's own two sides matching, or AniList
    /// reporting the same mapped relation type between the pair.</summary>
    Confirmed,

    /// <summary>AniList relates the pair, but under a different mapped type.
    /// Traversed for series membership; never given a dedicated button.</summary>
    Corroborated,

    /// <summary>The far end has been full-fetched and does not reciprocate,
    /// and no AniList adjudication has settled it either way yet.</summary>
    Unconfirmed,

    /// <summary>AniList resolves both ends to media and reports no edge
    /// between them at all. Remains stored and visible in the More overlay,
    /// but is never traversed and never given a button.</summary>
    Contradicted,

    /// <summary>The far end has never been full-fetched (its silence carries
    /// no information), or AniList doesn't know one/both ends, or the lookup
    /// failed. Behaves exactly as it did before this change.</summary>
    Unknown,
}
