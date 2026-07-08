using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to my list entries. Backs every page read — never
/// call the MAL client on a render path.</summary>
public interface IUserAnimeEntryRepository
{
    Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default);
    Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default);
}
