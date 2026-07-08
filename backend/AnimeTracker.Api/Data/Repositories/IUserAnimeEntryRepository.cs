using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to my list entries. Backs every page read — never
/// call the MAL client on a render path.</summary>
public interface IUserAnimeEntryRepository
{
    Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default);
    Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Pending-push count and the most recent successful push time
    /// across all entries, for the settings page's sync status display.</summary>
    Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default);
}
