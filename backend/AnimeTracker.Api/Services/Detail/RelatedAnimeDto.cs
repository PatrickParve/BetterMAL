using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Detail;

/// <summary>Flat projection of one related-anime edge for the detail page. The
/// server does not group by relation type — which relations get a dedicated
/// button vs. land in the More overlay is a UI decision made client-side.</summary>
public record RelatedAnimeDto(int AnimeId, string Title, string? PictureUrl, string? MediaType, string RelationType)
{
    // MAL's related_anime{node{media_type}} nested field selection reports
    // the related anime's media type directly, so a full-detail fetch stores
    // it on AnimeRelatedAnime.MediaType. The AnimeMetadata cache lookup is a
    // fallback only, for relation rows written before this column existed —
    // those stay null until their owner's next full-detail fetch.
    public static RelatedAnimeDto FromEntity(AnimeRelatedAnime relation, IReadOnlyDictionary<int, string?> mediaTypeByAnimeId) => new(
        relation.RelatedAnimeId,
        relation.Title,
        relation.PictureUrl,
        relation.MediaType ?? mediaTypeByAnimeId.GetValueOrDefault(relation.RelatedAnimeId),
        relation.RelationType);
}
