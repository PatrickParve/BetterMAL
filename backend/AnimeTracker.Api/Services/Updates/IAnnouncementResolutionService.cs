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
    /// cached record carrying an airing status (fetching it via
    /// <see cref="IMetadataRefreshService.RefreshOneAsync"/> only when it has
    /// none, or only a lean listing one), records an <c>Announced</c> update
    /// for it when it hasn't finished airing, and marks every discovery it
    /// considered as processed — even when no announcement resulted. Records
    /// one attempt on <paramref name="tally"/> immediately before each MAL
    /// call. When MAL answers that it has no such anime, the discovery is
    /// resolved without an announcement; any other failure leaves it
    /// unprocessed for a later pass. Stops at the first outage-type failure
    /// (design D2) and records it as
    /// <see cref="MalCallTally.UnavailableAnimeId"/>, leaving that group's
    /// discoveries unprocessed. Discoveries naming
    /// <paramref name="skipAnimeId"/> wait for a later pass.
    ///
    /// <para>Whether an anime was never fully fetched comes from what its
    /// discoveries recorded when the edge appeared
    /// (<c>RelationDiscovery.RelatedAnimeHadFullDetail</c>; any one of a
    /// group's discoveries saying so is enough), and never from its cached
    /// record now — the series rebuild the same detection queues fetches it
    /// within seconds. Only a discovery recorded before that column existed
    /// falls back to the cached record. Whether the user holds a list entry
    /// for it stays a check made here, at resolution time. A discovery
    /// resolved without a MAL call — cached in full already, or not an
    /// announcement candidate — is subject to none of the failure paths above
    /// and cannot end the pass.</para></summary>
    Task ResolveAsync(int maxAnime, int? skipAnimeId, MalCallTally tally, CancellationToken ct = default);
}
