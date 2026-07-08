using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to cached anime metadata. Backs every page read —
/// never call the MAL client on a render path.</summary>
public interface IAnimeMetadataRepository
{
    Task<AnimeMetadata?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<List<AnimeMetadata>> GetAllAsync(CancellationToken ct = default);
}
