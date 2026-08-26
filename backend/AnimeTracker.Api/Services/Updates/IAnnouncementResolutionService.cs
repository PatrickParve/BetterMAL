namespace AnimeTracker.Api.Services.Updates;

/// <summary>Resolves newly-discovered relation edges (<c>RelationDiscovery</c>)
/// into cached anime and, where warranted, an <c>Announced</c> update
/// (design.md D6/D11) — the read side of <c>RelationDiscovery.ProcessedAt</c>.
/// Runs at the start of each metadata-refresh tick, ahead of the staleness
/// batch, and its MAL calls are billed against that tick's own daily cap by
/// the caller.</summary>
public interface IAnnouncementResolutionService
{
    /// <summary>Resolves up to <paramref name="maxAnime"/> distinct
    /// newly-related anime from the oldest unprocessed <c>RelationDiscovery</c>
    /// rows: ensures each has a cached record (fetching it via
    /// <see cref="Metadata.IMetadataRefreshService.RefreshOneAsync"/> when it
    /// doesn't), records an <c>Announced</c> update for it when it hasn't
    /// finished airing, and marks every discovery it considered as processed —
    /// even when no announcement resulted. A discovery whose fetch failed is
    /// left unprocessed so a later pass retries it. Returns the number of MAL
    /// calls made, for the caller to bill against its own daily cap.</summary>
    Task<int> ResolveAsync(int maxAnime, CancellationToken ct = default);
}
