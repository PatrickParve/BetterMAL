using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Sync;

/// <summary>Counts from a reconciliation run — Added/Updated/RemovedOnMal
/// describe the diff computed and held for review, not changes already
/// applied (design.md decision: "compute-then-review, not auto-apply").
/// SkippedUnrecognized counts anime MAL listed with a list status this app
/// doesn't recognize — left out of the diff entirely.</summary>
public record ReconciliationResult(int Added, int Updated, int Unchanged, int SkippedPending, int SkippedRemovals, int RemovedOnMal, int SkippedUnrecognized);

public record PendingReconciliationDiffEntryDto(
    int AnimeId,
    string Title,
    string? PictureUrl,
    string ChangeType,
    string Status,
    int EpisodesWatched,
    int? MyScore,
    DateOnly? StartedAt,
    DateOnly? CompletedAt,
    int RewatchCount);

public record PendingReconciliationDiffDto(int Id, DateTimeOffset ComputedAt, List<PendingReconciliationDiffEntryDto> Entries);

/// <summary>Full reconciliation safety net (design.md risk: "Out-of-band edits
/// on MAL's own site cause drift"): pulls the complete MAL list and diffs it
/// against local data. MAL is canonical for entries with no unpushed local
/// edit; entries still pending_sync are excluded from the diff so this never
/// clobbers a change still in flight — it's revisited on a later run once that
/// push lands. A local entry MAL no longer lists is proposed for removal
/// (RemovedOnMal), not silently re-added, and an anime whose MAL list status
/// this app doesn't recognize is skipped entirely rather than guessed at.
/// Scheduled weekly (ReconciliationBackgroundService) and manually
/// triggerable (SyncController). The computed diff is held for review rather
/// than applied — see Accept/Cancel below.</summary>
public interface IReconciliationService
{
    /// <summary>Runs full reconciliation, waiting for any other run already
    /// in progress (design.md D6 — a manual run and the weekly run never
    /// compute a diff at once). Reports the anime read so far through
    /// <paramref name="progress"/> with no total, since MyAnimeList never
    /// says how long the list is (design.md D5); a null
    /// <paramref name="progress"/> makes the run quiet, as the weekly run
    /// always is (design.md D6).</summary>
    Task<ReconciliationResult> RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default);

    /// <summary>The most recent run's diff still awaiting review, or null if
    /// none is pending.</summary>
    Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default);

    /// <summary>Applies every difference in the pending diff to local data and
    /// clears it. An Added/Updated entry is created or overwritten as before;
    /// a RemovedOnMal entry deletes the local entry instead, with nothing
    /// pushed to MyAnimeList. Returns false if there was no pending
    /// diff.</summary>
    Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default);

    /// <summary>Discards the pending diff without applying anything. Returns
    /// false if there was no pending diff.</summary>
    Task<bool> CancelPendingDiffAsync(CancellationToken ct = default);
}
