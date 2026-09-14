namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Tiered metadata refresh (design.md's "Caching-first data
/// flow" and its "Refresh job hammering the API on a large library" risk).
/// Nightly batches and on-demand refresh both do a full-detail update, one
/// MAL call each — MAL rate-limits per request, not per field, so refreshing
/// everything on the same tiers costs nothing extra.</summary>
public interface IMetadataRefreshService
{
    /// <summary>Refreshes up to <paramref name="batchSize"/> of the stalest due
    /// anime (full detail, one MAL call each), skipping <paramref name="skipAnimeId"/>
    /// when set (the anime whose call ended the previous pass, design D6).
    /// Records one attempt on <paramref name="tally"/> immediately before
    /// each MAL call, so a call counts even if this method throws
    /// afterwards. Stops at the first outage-type failure (design D2) and
    /// records it as <see cref="MalCallTally.UnavailableAnimeId"/>; a
    /// not-found answer stamps <see cref="Models.AnimeMetadata.LastRefreshFailedAt"/>
    /// on that anime and continues with the next candidate. Work completed
    /// before a stop is still saved.</summary>
    Task RefreshStaleBatchAsync(int batchSize, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default);

    /// <summary>Full-detail refresh of one anime right now — one API call,
    /// updates the cached record and both sync timestamps.</summary>
    Task RefreshOneAsync(int animeId, CancellationToken ct = default);
}
