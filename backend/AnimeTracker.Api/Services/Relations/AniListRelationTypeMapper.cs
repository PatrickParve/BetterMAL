namespace AnimeTracker.Api.Services.Relations;

/// <summary>Maps AniList's raw uppercase relation enum to the MAL vocabulary
/// it corresponds to, for adjudicating a contested MAL edge (relation-confidence
/// spec, "AniList adjudicates contested relation edges"). <c>ADAPTATION</c> is
/// manga-side and is never stored. Every other AniList type — mapped or not —
/// still counts as "an edge exists between this pair" even when it can't be
/// matched to a specific MAL type, since silence about the type is not
/// evidence of no relation.</summary>
public static class AniListRelationTypeMapper
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        ["PREQUEL"] = ["prequel"],
        ["SEQUEL"] = ["sequel"],
        ["PARENT"] = ["parent_story"],
        ["SIDE_STORY"] = ["side_story"],
        ["SUMMARY"] = ["summary", "full_story"],
        ["SPIN_OFF"] = ["spin_off"],
        ["CHARACTER"] = ["character"],
        ["OTHER"] = ["other"],
        ["ALTERNATIVE"] = ["alternative_version", "alternative_setting"],
    };

    /// <summary>True when AniList's raw type is manga-side and should never be
    /// persisted as an <c>AniListRelation</c> row at all.</summary>
    public static bool IsIgnored(string aniListRelationType) => aniListRelationType == "ADAPTATION";

    /// <summary>Whether AniList's raw relation type maps to (and therefore
    /// confirms) the given MAL relation type. An AniList type with no mapping
    /// at all never confirms anything, but the edge between the pair still
    /// exists (Corroborated, not Contradicted).</summary>
    public static bool Matches(string aniListRelationType, string malRelationType) =>
        Map.TryGetValue(aniListRelationType, out var malTypes) && malTypes.Contains(malRelationType);
}
