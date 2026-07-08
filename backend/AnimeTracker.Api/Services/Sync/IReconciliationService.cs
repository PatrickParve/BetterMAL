namespace AnimeTracker.Api.Services.Sync;

/// <summary>Counts from a reconciliation run — Added/Updated describe the diff
/// computed and held for review, not changes already applied (design.md
/// decision: "compute-then-review, not auto-apply").</summary>
public record ReconciliationResult(int Added, int Updated, int Unchanged, int SkippedPending);

public record PendingReconciliationDiffEntryDto(
    int AnimeId,
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
/// push lands. Scheduled weekly (ReconciliationBackgroundService) and manually
/// triggerable (SyncController). The computed diff is held for review rather
/// than applied — see Accept/Cancel below.</summary>
public interface IReconciliationService
{
    Task<ReconciliationResult> RunAsync(CancellationToken ct = default);

    /// <summary>The most recent run's diff still awaiting review, or null if
    /// none is pending.</summary>
    Task<PendingReconciliationDiffDto?> GetPendingDiffAsync(CancellationToken ct = default);

    /// <summary>Applies every difference in the pending diff to local data and
    /// clears it. Returns false if there was no pending diff.</summary>
    Task<bool> AcceptPendingDiffAsync(CancellationToken ct = default);

    /// <summary>Discards the pending diff without applying anything. Returns
    /// false if there was no pending diff.</summary>
    Task<bool> CancelPendingDiffAsync(CancellationToken ct = default);
}
