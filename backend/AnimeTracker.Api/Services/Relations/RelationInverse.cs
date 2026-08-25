namespace AnimeTracker.Api.Services.Relations;

/// <summary>What a stored relation edge means from the far end's side — the
/// only way an existing prequel/sequel reaches the page it belongs on, since
/// MAL's <c>related_anime</c> is not reliably symmetric (relation-confidence
/// spec, "An anime's relations are read in both directions").
/// <c>spin_off</c>, <c>adaptation</c>, and any unrecognized relation string
/// have no confident inverse: MAL has no vocabulary for "this is the original
/// of that spin-off", so such an edge is carried across on the far side with
/// its raw value unchanged, and is never eligible for a dedicated
/// prequel/sequel-style button.</summary>
public static class RelationInverse
{
    private static readonly Dictionary<string, string> Map = new()
    {
        ["sequel"] = "prequel",
        ["prequel"] = "sequel",
        ["parent_story"] = "side_story",
        ["side_story"] = "parent_story",
        ["summary"] = "full_story",
        ["full_story"] = "summary",
        ["alternative_version"] = "alternative_version",
        ["alternative_setting"] = "alternative_setting",
        ["character"] = "character",
        ["other"] = "other",
    };

    /// <summary>The relation type the far end would need to store to mean the
    /// same relationship back, or null when this type has no confident
    /// inverse (<c>spin_off</c>, <c>adaptation</c>, an unrecognized string).</summary>
    public static string? Invert(string relationType) => Map.GetValueOrDefault(relationType);
}
