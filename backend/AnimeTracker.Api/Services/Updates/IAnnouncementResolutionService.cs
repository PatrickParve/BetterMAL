using AnimeTracker.Api.Services.Metadata;

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
    /// rows, skipping <paramref name="skipAnimeId"/> when set (the anime
    /// whose call ended the previous pass, design D6): ensures each has a
    /// cached record (fetching it via
    /// <see cref="IMetadataRefreshService.RefreshOneAsync"/> when it
    /// doesn't), records an <c>Announced</c> update for it when it hasn't
    /// finished airing, and marks every discovery it considered as processed —
    /// even when no announcement resulted. Records one attempt on
    /// <paramref name="tally"/> immediately before each MAL call. When MAL
    /// answers that it has no such anime, the discovery is resolved without
    /// an announcement; any other failure leaves it unprocessed for a later
    /// pass. Stops at the first outage-type failure (design D2) and records
    /// it as <see cref="MalCallTally.UnavailableAnimeId"/>, leaving
    /// that group's discoveries unprocessed. Discoveries naming
    /// <paramref name="skipAnimeId"/> wait for a later pass.</summary>
    Task ResolveAsync(int maxAnime, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default);
}
