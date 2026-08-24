using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to cached anime metadata. Backs every page read —
/// never call the MAL client on a render path.</summary>
public interface IAnimeMetadataRepository
{
    Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Lightweight title/picture projection for the type-ahead search's
    /// local-cache stage — avoids loading full metadata rows just to match titles.</summary>
    Task<List<AnimeTitleProjection>> GetSearchIndexAsync(CancellationToken ct = default);

    /// <summary>Same rows as <see cref="GetSearchIndexAsync"/>, widened with the
    /// fields a search results card renders (design.md D7), for the results
    /// page's fallback when the live MAL search fails.</summary>
    Task<List<AnimeSearchFallbackProjection>> GetSearchFallbackIndexAsync(CancellationToken ct = default);
}

public record AnimeTitleProjection(int Id, string Title, string? EnglishTitle, string? PictureUrl, int? PopularityRank);

public record AnimeSearchFallbackProjection(
    int Id, string Title, string? EnglishTitle, string? PictureUrl, int? PopularityRank,
    string? MediaType, int? TotalEpisodes, double? MalScore);
