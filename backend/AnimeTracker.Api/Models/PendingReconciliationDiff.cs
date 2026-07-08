namespace AnimeTracker.Api.Models;

public enum ReconciliationDiffChangeType
{
    Added,
    Updated,
}

/// <summary>Header row for the most recent full-reconciliation run's computed
/// differences, held for review until Accept or Cancel (design.md decision:
/// "compute-then-review, not auto-apply"). At most one row exists at a time —
/// a new reconciliation run replaces any previous unreviewed diff.</summary>
public class PendingReconciliationDiff
{
    public int Id { get; set; }
    public DateTimeOffset ComputedAt { get; set; }

    public List<PendingReconciliationDiffEntry> Entries { get; set; } = [];
}

/// <summary>One anime's pending change within a PendingReconciliationDiff — the
/// remote (MAL) values to apply to the local UserAnimeEntry if the diff is
/// accepted. For an Added entry, applying it means creating the UserAnimeEntry;
/// for Updated, it means overwriting the local entry's tracked fields.</summary>
public class PendingReconciliationDiffEntry
{
    public int Id { get; set; }
    public int PendingReconciliationDiffId { get; set; }
    public PendingReconciliationDiff Diff { get; set; } = null!;

    public int AnimeId { get; set; }
    public ReconciliationDiffChangeType ChangeType { get; set; }

    public WatchStatus Status { get; set; }
    public int EpisodesWatched { get; set; }
    public int? MyScore { get; set; }
    public DateOnly? StartedAt { get; set; }
    public DateOnly? CompletedAt { get; set; }
    public int RewatchCount { get; set; }
}
