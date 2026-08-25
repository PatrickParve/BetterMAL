using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Relations;

/// <summary>Owns everything relation-confidence: the union of an anime's
/// outgoing and inverted-incoming edges, confidence classification (MAL
/// agreement plus AniList adjudication), and the ranked prequel/sequel/
/// parent-story pick. The single implementation callers other than the
/// detail page (e.g. series building) will also use, so the "Series" link and
/// the page it leads to can never disagree about which edges are real.</summary>
public interface IRelationResolver
{
    /// <summary>Resolves <paramref name="anime"/>'s prequel, sequel, and
    /// parent-story references. <paramref name="anime"/> must already have
    /// its own outgoing <c>RelatedAnime</c> loaded.</summary>
    Task<RelationResolution> ResolveAsync(AnimeMetadata anime, CancellationToken ct = default);

    /// <summary>The union of <paramref name="anime"/>'s outgoing and inverted
    /// incoming edges, each carrying its confidence — the same union
    /// <see cref="ResolveAsync"/> ranks over. Series building calls this
    /// directly to filter Contradicted edges out of traversal, so the "Series"
    /// link and the page it leads to can never disagree about which edges are
    /// real. <paramref name="anime"/> must already have its own outgoing
    /// <c>RelatedAnime</c> loaded.</summary>
    Task<IReadOnlyList<ResolvedRelationEdge>> GetEdgesAsync(AnimeMetadata anime, CancellationToken ct = default);
}
