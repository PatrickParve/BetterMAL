namespace AnimeTracker.Api.Services.Airing;

/// <summary>Refreshes stored per-episode airing rows from AniList: one anime
/// with single requests, or many with batched ones. Every path replaces the
/// target anime's stored rows wholesale (see IEpisodeAiringRepository) and
/// updates its AnimeAiringSync bookkeeping. What still needs fetching is each
/// anime's own airing-fetched mark (<c>AnimeAiringSync.LastFetchedAt</c>), never
/// a once-ever flag (add-first-run-setup design D15).</summary>
public interface IEpisodeScheduleRefreshService
{
    /// <summary>Refreshes one anime's airing data now, with single requests: resolves/reuses its
    /// AniList id, fetches its full schedule, replaces its stored rows, and
    /// recomputes its recheck-due bookkeeping. For the add trigger and the detail
    /// page; a pass over many anime uses <see cref="RefreshBatchAsync"/>.
    /// <para>An anime AniList was recorded as not knowing is looked up again only
    /// when that record is 90 days old, or when <paramref name="relookupAbsent"/>
    /// is set, which a refresh the user started by hand sets.</para></summary>
    Task RefreshOneAsync(int animeId, bool relookupAbsent = false, CancellationToken ct = default);

    /// <summary>Refreshes many anime with batched requests, the single write path
    /// of every pass over more than one: setup's airing step, the catch-up, the
    /// daily pass, the rechecks, Refresh all and Force all. It stores exactly what
    /// <see cref="RefreshOneAsync"/> would store for each anime, saving each in one
    /// transaction the moment its rows are complete, so a failure part-way leaves
    /// what was saved saved. A failure to fetch some anime is logged and reported
    /// as <see cref="AiringRefreshOutcome.Failed"/> for exactly those anime; the
    /// rest carry on. Only a cancellation of <paramref name="ct"/> throws.</summary>
    Task<RefreshBatchResult> RefreshBatchAsync(
        IReadOnlyList<int> animeIds, RefreshBatchOptions? options = null, CancellationToken ct = default);

    /// <summary>Fetches every my-list anime that has no airing-fetched mark, or that
    /// AniList was recorded as not knowing at least 90 days ago: the hourly airing
    /// work's safety net, which usually finds nothing to do. Logs each anime AniList
    /// has no airing data for by title and id, so coverage gaps stay visible.</summary>
    Task CatchUpAsync(CancellationToken ct = default);

    /// <summary>My-list anime whose MAL airing status means their airing data
    /// is still worth tracking: currently airing or not yet aired.</summary>
    Task<List<int>> GetTrackedAnimeIdsAsync(CancellationToken ct = default);

    /// <summary>The anime "Refresh all airing dates" re-fetches: every my-list anime
    /// except those whose airing history is complete. It is complete only when
    /// the anime has finished airing, AniList knows it and it has been fetched, its
    /// total episode count is known, and its highest stored episode number is at
    /// least that total. The highest number is read, never the row count: an anime
    /// whose first episodes premiered together has fewer rows than episodes
    /// (<i>Frieren</i> holds rows for episodes 5 to 28, since 1 to 4 premiered
    /// together) and is still complete. An anime with no known total is not.
    /// With <paramref name="force"/> ("Force all airing dates"), every my-list anime.</summary>
    Task<List<int>> GetFullRefreshTargetsAsync(bool force, CancellationToken ct = default);
}

/// <summary>How one anime fared in a <see cref="IEpisodeScheduleRefreshService.RefreshBatchAsync"/>.</summary>
public enum AiringRefreshOutcome
{
    /// <summary>AniList knows it and gave at least one airing row.</summary>
    Fetched,

    /// <summary>Nothing was fetched and nothing went wrong: AniList has no entry
    /// for it, or no rows, or it was recorded as unknown and isn't due a new look.
    /// It still carries its airing-fetched mark.</summary>
    NoData,

    /// <summary>Fetching or saving it failed. It has no new mark, so a later pass
    /// picks it up again.</summary>
    Failed,
}

/// <summary>What a batch pass is told to do. <paramref name="RelookupAbsent"/> looks
/// up every anime AniList was recorded as not knowing, whatever the age of that
/// record (a pass the user started by hand). <paramref name="OnAnimeDone"/> is
/// invoked exactly once per anime, as soon as its outcome is known, so a caller can
/// drive a progress indicator or a retry list.</summary>
public record RefreshBatchOptions(bool RelookupAbsent = false, Action<int, AiringRefreshOutcome>? OnAnimeDone = null);

/// <summary>A batch pass's counts, one per anime asked for.</summary>
public record RefreshBatchResult(int Fetched, int NoData, int Failed);
