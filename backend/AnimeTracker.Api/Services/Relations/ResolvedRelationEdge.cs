namespace AnimeTracker.Api.Services.Relations;

/// <summary>One relation edge as it applies to the anime being viewed —
/// already inverted onto that anime's side when derived from an incoming
/// edge, and carrying the confidence classification consumers need to decide
/// whether to traverse it or give it a button.</summary>
public record ResolvedRelationEdge(
    int AnimeId,
    string RelationType,
    string? Title,
    string? PictureUrl,
    string? MediaType,
    DateOnly? AiredFrom,
    bool IsReverseDerived,
    bool HasConfidentInverse,
    RelationConfidence Confidence);

/// <summary>The ranked-pick result for one anime's detail page: the resolved
/// prequel, sequel, and parent-story references. The full related-anime list
/// (More overlay) is unaffected by this — it keeps its existing source.</summary>
public record RelationResolution(
    ResolvedRelationEdge? Prequel,
    ResolvedRelationEdge? Sequel,
    ResolvedRelationEdge? ParentStory);
