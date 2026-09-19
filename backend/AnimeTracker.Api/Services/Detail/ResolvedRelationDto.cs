using AnimeTracker.Api.Services.Relations;

namespace AnimeTracker.Api.Services.Detail;

/// <summary>One server-resolved relation reference (prequel, sequel, or
/// parent story) for the detail page's dedicated buttons — the ranked pick
/// among possibly several candidates, replacing the client's old
/// first-by-array-order logic. The full <c>relatedAnime</c> list on
/// <see cref="AnimeDetailDto"/> is unaffected; this is additive.</summary>
public record ResolvedRelationDto(
    int AnimeId, string Title, string? EnglishTitle, string? PictureUrl, string? MediaType, RelationConfidence Confidence, bool IsReverseDerived)
{
    public static ResolvedRelationDto? FromResolved(ResolvedRelationEdge? edge) =>
        edge is null
            ? null
            : new ResolvedRelationDto(edge.AnimeId, edge.Title ?? "", edge.EnglishTitle, edge.PictureUrl, edge.MediaType, edge.Confidence, edge.IsReverseDerived);
}
