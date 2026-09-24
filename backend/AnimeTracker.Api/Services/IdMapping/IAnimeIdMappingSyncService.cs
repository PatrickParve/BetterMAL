namespace AnimeTracker.Api.Services.IdMapping;

/// <summary>What one <see cref="IAnimeIdMappingSyncService.SyncIfDueAsync"/>
/// call did. A sync abandoned by the sanity guard is <c>Failed</c> too: both
/// are recorded as a failed attempt and leave the stored mapping untouched.</summary>
public enum AnimeIdMappingSyncOutcome
{
    NotDue,
    Synced,
    Failed,
}

/// <summary>Keeps the stored MAL → TMDB/IMDb mapping in step with the Fribb
/// mapping file, weekly (spec `external-id-mapping`).</summary>
public interface IAnimeIdMappingSyncService
{
    /// <summary>Refreshes the mapping when it is due: never refreshed, or last
    /// refreshed more than 7 days ago, and not within 6 hours of a failed
    /// attempt. Never throws except on cancellation. Any failure is logged,
    /// recorded as a failed attempt and returned as
    /// <see cref="AnimeIdMappingSyncOutcome.Failed"/>, so the hourly airing
    /// tick that calls this can never be stopped by it (design.md D2).</summary>
    Task<AnimeIdMappingSyncOutcome> SyncIfDueAsync(CancellationToken ct = default);
}
