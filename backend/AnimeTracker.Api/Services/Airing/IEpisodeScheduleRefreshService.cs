namespace AnimeTracker.Api.Services.Airing;

/// <summary>Refreshes stored per-episode airing rows from AniList — one anime,
/// a batch, or the one-time full-history backfill. Every path replaces the
/// target anime's stored rows wholesale (see IEpisodeAiringRepository) and
/// updates its AnimeAiringSync bookkeeping.</summary>
public interface IEpisodeScheduleRefreshService
{
    /// <summary>Refreshes one anime's airing data now: resolves/reuses its
    /// AniList id, fetches its full schedule, replaces its stored rows, and
    /// recomputes its recheck-due bookkeeping.</summary>
    Task RefreshOneAsync(int animeId, CancellationToken ct = default);

    /// <summary>Refreshes each anime in turn, pacing requests through
    /// IAniListClient, logging and continuing past a per-anime failure.
    /// <see cref="RefreshManyResult.NoData"/> counts anime AniList has no
    /// airing data for — not a failure. <see cref="RefreshManyResult.Failed"/>
    /// counts anime whose refresh threw. onProgress, if given, is invoked
    /// after each anime with the running count processed so far — lets a
    /// caller drive a progress indicator without duplicating the per-anime
    /// failure handling here.</summary>
    Task<RefreshManyResult> RefreshManyAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default, Action<int>? onProgress = null);

    /// <summary>One-time full-history pass over every tracked my-list anime.
    /// Resumable — skips anime already fetched this backfill — and stamps
    /// AiringRefreshState.BackfillCompletedAtUtc only once every target has
    /// been fetched.</summary>
    Task BackfillAsync(CancellationToken ct = default);

    /// <summary>My-list anime whose MAL airing status means their airing data
    /// is still worth tracking: currently airing or not yet aired.</summary>
    Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default);

    /// <summary>Every my-list anime except those already known-complete:
    /// finished-airing anime that have already been successfully queried
    /// against AniList at least once, whose airing history can no longer
    /// change. Still-airing/upcoming anime and finished anime never yet
    /// fetched are always included. Backs the settings page's manual
    /// "refresh all airing data" action.</summary>
    Task<List<int>> GetFullRefreshTargetsAsync(CancellationToken ct = default);
}

/// <summary>RefreshManyAsync's per-run counts. <paramref name="NoData"/> is
/// anime AniList simply has no airing data for — not a failure.
/// <paramref name="Failed"/> is anime whose refresh threw.</summary>
public record RefreshManyResult(int NoData, int Failed);
