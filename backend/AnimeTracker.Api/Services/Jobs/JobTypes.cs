namespace AnimeTracker.Api.Services.Jobs;

// Empty sealed subclasses, one per job, so each gets its own typed
// DI-registered singleton rather than a string-keyed one (design.md D1
// Alternatives) — constructors that need a specific job's tracker just ask
// for its type.

/// <summary>The corrective re-sync from MAL ("Correct imported data").</summary>
public sealed class ResyncProgress : JobProgressTracker { }

/// <summary>The full airing-date refresh ("Airing dates").</summary>
public sealed class AiringFullRefreshProgress : JobProgressTracker { }

/// <summary>The build-all-series run.</summary>
public sealed class SeriesBulkBuildProgress : JobProgressTracker { }

/// <summary>The manual "Sync now" push.</summary>
public sealed class SyncNowProgress : JobProgressTracker { }

/// <summary>A manually started full reconciliation. The weekly run passes no
/// sink and never touches this tracker (design.md D6).</summary>
public sealed class ReconcileProgress : JobProgressTracker { }

public enum HeldDecisionAction { Accept, Decline }

/// <summary>Accept all and decline all share one job (design.md D18):
/// whichever is pressed second gets the running job's state back and starts
/// nothing. <see cref="Action"/> records which of the two is running or most
/// recently ran.</summary>
public sealed class HeldDecisionProgress : JobProgressTracker
{
    public HeldDecisionAction? Action { get; private set; }

    /// <summary>Sets <see cref="Action"/> and starts the run in the same
    /// locked step <see cref="JobProgressTracker.TryBegin"/> uses, so the two
    /// can never be observed out of sync.</summary>
    public bool TryBegin(HeldDecisionAction action) => TryBeginCore(() => Action = action);
}
