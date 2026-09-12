namespace AnimeTracker.Api.Services.Jobs;

public enum JobPhase
{
    NotStarted,
    Running,
    Complete,
    Failed,
}

/// <summary>One background job's state, in the shape every job shares
/// (design.md D1). <see cref="Total"/> is null while the job doesn't yet know
/// how much work there is — never zero, which is reserved for a run that
/// really has nothing to do. <see cref="RetryAt"/> is set only by jobs that
/// plan their own retry (the MAL list import).</summary>
public record JobSnapshot(
    JobPhase Phase, int Done, int? Total, string? Error,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    DateTimeOffset? RetryAt = null,
    // Only meaningful once a run has ended (Complete or Failed); a run that
    // hasn't ended yet is always unseen (design.md D2).
    bool OutcomeSeen = false);

/// <summary>The narrow surface a service reports progress through, without
/// the power to start or end a run itself — a service never knows which job
/// it is reporting into, and a null sink makes a run quiet (design.md
/// D1/D6).</summary>
public interface IJobProgressSink
{
    void SetTotal(int total);
    void ReportProgress(int done);
}

/// <summary>One shared lifecycle for a background job (design.md D1-D3):
/// not-started/running/complete/failed, done-of-total with an unknown total
/// represented as null, and when it started/ended. Every method takes the
/// tracker's own lock, so a snapshot read is always internally consistent.</summary>
public class JobProgressTracker : IJobProgressSink
{
    private readonly Lock _lock = new();
    private JobSnapshot _snapshot = new(JobPhase.NotStarted, 0, null, null, null, null);

    public virtual JobSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    /// <summary>Starts a run in one step: false while a run is already Running
    /// (nothing changes), otherwise moves to Running with a clean slate and
    /// returns true (design.md D2 — the tracker is the start gate).</summary>
    public bool TryBegin() => TryBeginCore(null);

    /// <summary>Lets a subclass fold extra state into the same locked
    /// transition <see cref="TryBegin"/> uses, so nothing can observe the
    /// tracker as Running before that extra state is set (used by
    /// HeldDecisionProgress.TryBegin(action)).</summary>
    protected bool TryBeginCore(Action? onBeginning)
    {
        lock (_lock)
        {
            if (_snapshot.Phase == JobPhase.Running)
                return false;

            onBeginning?.Invoke();
            // A fresh JobSnapshot defaults OutcomeSeen to false, so starting a
            // run clears it for free (design.md D2).
            _snapshot = new JobSnapshot(JobPhase.Running, 0, null, null, DateTimeOffset.UtcNow, null);
            return true;
        }
    }

    /// <summary>Records that this run's ended outcome has been shown to the
    /// user, guarded by the exact time it ended: it only takes effect while
    /// the snapshot is still Complete or Failed with a matching
    /// <see cref="JobSnapshot.FinishedAt"/>, so a report meant for a run that
    /// has since moved on records nothing (design.md D2).</summary>
    public virtual void MarkOutcomeSeen(DateTimeOffset finishedAt)
    {
        lock (_lock)
        {
            if ((_snapshot.Phase == JobPhase.Complete || _snapshot.Phase == JobPhase.Failed) && _snapshot.FinishedAt == finishedAt)
                _snapshot = _snapshot with { OutcomeSeen = true };
        }
    }

    public void SetTotal(int total)
    {
        lock (_lock)
            _snapshot = _snapshot with { Total = total };
    }

    public void AddToTotal(int n)
    {
        lock (_lock)
            _snapshot = _snapshot with { Total = (_snapshot.Total ?? 0) + n };
    }

    public void ReportProgress(int done)
    {
        lock (_lock)
            _snapshot = _snapshot with { Done = done };
    }

    public void Complete()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = JobPhase.Complete, FinishedAt = DateTimeOffset.UtcNow };
    }

    public void Fail(string reason)
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = JobPhase.Failed, Error = reason, FinishedAt = DateTimeOffset.UtcNow };
    }
}
