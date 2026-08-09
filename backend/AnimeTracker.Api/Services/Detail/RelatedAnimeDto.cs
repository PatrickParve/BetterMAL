using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Detail;

/// <summary>Flat projection of one related-anime edge for the detail page. The
/// server does not group by relation type — which relations get a dedicated
/// button vs. land in the More overlay is a UI decision made client-side.</summary>
public record RelatedAnimeDto(int AnimeId, string Title, string? PictureUrl, string? MediaType, string RelationType)
{
    // MAL's related_anime field never returns the related anime's media type
    // (its node shape is a fixed id/title/picture, with no field-selection
    // support), so it isn't stored on AnimeRelatedAnime — it's looked up from
    // our own AnimeMetadata cache instead, and is simply absent for a related
    // anime we haven't cached yet.
    public static RelatedAnimeDto FromEntity(AnimeRelatedAnime relation, IReadOnlyDictionary<int, string?> mediaTypeByAnimeId) => new(
        relation.RelatedAnimeId,
        relation.Title,
        relation.PictureUrl,
        mediaTypeByAnimeId.GetValueOrDefault(relation.RelatedAnimeId),
        relation.RelationType);
}
