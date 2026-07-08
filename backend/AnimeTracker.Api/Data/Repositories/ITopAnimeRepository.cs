using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to the cached Top Anime ranking snapshot. Backs
/// the top-anime page's render path — never call the MAL client from here.</summary>
public interface ITopAnimeRepository
{
    /// <summary>When the ranking was last live-fetched, or null if it has
    /// never been fetched.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(CancellationToken ct = default);

    Task<List<TopAnimeRankingEntry>> GetRankingAsync(CancellationToken ct = default);
}
