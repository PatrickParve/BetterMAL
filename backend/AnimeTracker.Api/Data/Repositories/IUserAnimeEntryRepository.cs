using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to my list entries. Backs every page read — never
/// call the MAL client on a render path.</summary>
public interface IUserAnimeEntryRepository
{
    Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default);
    Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Pending-and-unheld count, held-for-review count, and the most
    /// recent successful push time across all entries, for the settings
    /// page's sync status display (design.md D14 — PendingCount excludes
    /// held rows so the two figures never conflate "in flight" with "waiting
    /// on me").</summary>
    Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default);
}
