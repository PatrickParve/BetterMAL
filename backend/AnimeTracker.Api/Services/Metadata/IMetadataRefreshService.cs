namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Tiered metadata/score refresh (design.md's "Caching-first data
/// flow" and its "Refresh job hammering the API on a large library" risk).
/// Nightly batches refresh only the mean score for the stalest due candidates;
/// on-demand refresh does a full-detail update for one anime.</summary>
public interface IMetadataRefreshService
{
    /// <summary>Refreshes up to <paramref name="batchSize"/> of the stalest due
    /// anime (score-only, one MAL call each). Returns how many were refreshed.</summary>
    Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default);

    /// <summary>Full-detail refresh of one anime right now — one API call,
    /// updates the cached record and both sync timestamps.</summary>
    Task RefreshOneAsync(int animeId, CancellationToken ct = default);
}
