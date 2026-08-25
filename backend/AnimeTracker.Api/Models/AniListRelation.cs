namespace AnimeTracker.Api.Models;

/// <summary>One relation edge AniList reports for a MAL-mapped anime — stored
/// separately from AnimeMetadata.RelatedAnime so a MAL full-detail refresh,
/// which replaces that collection wholesale, never clobbers it (mirrors how
/// AnimeAiringSync keeps AniList state apart from the MAL mirror).
/// RelationType is AniList's raw uppercase enum value (e.g. "SEQUEL"),
/// unmapped to MAL vocabulary here so mapping stays a read-time concern for
/// the relation-confidence resolver. A relation whose far media has no MAL id
/// is never stored — it cannot be matched against a MAL edge and is not
/// evidence about one.</summary>
public class AniListRelation
{
    public int AnimeId { get; set; } // MAL id
    public int RelatedAnimeId { get; set; } // far end's MAL id, as AniList reports it
    public string RelationType { get; set; } = "";
}
