namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Tiered metadata refresh (design.md's "Caching-first data
/// flow" and its "Refresh job hammering the API on a large library" risk).
/// Nightly batches and on-demand refresh both do a full-detail update, one
/// MAL call each — MAL rate-limits per request, not per field, so refreshing
/// everything on the same tiers costs nothing extra.</summary>
public interface IMetadataRefreshService
{
    /// <summary>Refreshes up to <paramref name="batchSize"/> of the stalest due
    /// anime (full detail, one MAL call each). Returns how many were refreshed.</summary>
    Task<int> RefreshStaleBatchAsync(int batchSize, CancellationToken ct = default);

    /// <summary>Full-detail refresh of one anime right now — one API call,
    /// updates the cached record and both sync timestamps.</summary>
    Task RefreshOneAsync(int animeId, CancellationToken ct = default);
}
