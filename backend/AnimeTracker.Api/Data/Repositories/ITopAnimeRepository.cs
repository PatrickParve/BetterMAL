using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Data.Repositories;

/// <summary>Read-only access to the cached Top Anime ranking snapshot. Backs
/// the top-anime page's render path — never call the MAL client from here.</summary>
public interface ITopAnimeRepository
{
    /// <summary>When the given ranking list was last live-fetched, or null if
    /// it has never been fetched.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(string rankingType, CancellationToken ct = default);

    Task<List<TopAnimeRankingEntry>> GetRankingAsync(string rankingType, CancellationToken ct = default);
}
