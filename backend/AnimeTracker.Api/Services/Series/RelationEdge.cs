namespace AnimeTracker.Api.Services.Series;

/// <summary>One relation edge between two members of a build's traversed
/// component, as declared by <paramref name="OwnerId"/>'s own
/// <c>AnimeRelatedAnime</c> row (split-series-by-version design.md decision
/// 1, tasks 3.2/4.*). Recorded once, by <c>SeriesGraphBuilder.TraverseAsync</c>,
/// as each member is dequeued and its own declared relations are read — so
/// the version partition and the relation-group resolution can both read
/// every edge in the component without re-querying it. <see cref="RelationType"/>
/// is the relation exactly as MAL/<c>AnimeRelatedAnime</c> stores it,
/// including <c>"other"</c> for a traversed companion-media edge.</summary>
internal readonly record struct RelationEdge(int OwnerId, int RelatedAnimeId, string RelationType);
