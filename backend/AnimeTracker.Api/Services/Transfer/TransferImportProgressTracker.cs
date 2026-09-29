using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Transfer;

public class TransferImportProgressTracker : ITransferImportProgressTracker
{
    private readonly Lock _lock = new();
    private TransferImportStatusSnapshot _snapshot = new(TransferImportPhase.NotStarted, 0, 0, null, null, null, null);

    public TransferImportStatusSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    public void MarkPending(string? deviceName, DateTimeOffset exportedAt)
    {
        lock (_lock)
            // Called from the accepting request, the same moment TryBegin
            // stamps StartedAt for every other job (design.md D4).
            _snapshot = new TransferImportStatusSnapshot(TransferImportPhase.Running, 0, 0, deviceName, exportedAt, null, null, StartedAt: DateTimeOffset.UtcNow);
    }

    public void Start(int total)
    {
        lock (_lock)
            _snapshot = _snapshot with { Total = total };
    }

    public void AddToTotal(int n)
    {
        lock (_lock)
            _snapshot = _snapshot with { Total = _snapshot.Total + n };
    }

    public void ReportProgress(int done)
    {
        lock (_lock)
            _snapshot = _snapshot with { Done = done };
    }

    public void Complete(TransferImportReport report)
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = TransferImportPhase.Complete, Report = report, FinishedAt = DateTimeOffset.UtcNow };
    }

    public void Fail(string reason)
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = TransferImportPhase.Failed, Error = reason, FinishedAt = DateTimeOffset.UtcNow };
    }

    /// <summary>Same guard as <see cref="JobProgressTracker.MarkOutcomeSeen"/>
    /// (design.md D4).</summary>
    public void MarkOutcomeSeen(DateTimeOffset finishedAt)
    {
        lock (_lock)
        {
            if ((_snapshot.Phase == TransferImportPhase.Complete || _snapshot.Phase == TransferImportPhase.Failed) && _snapshot.FinishedAt == finishedAt)
                _snapshot = _snapshot with { OutcomeSeen = true };
        }
    }

    /// <summary>Same guard as <see cref="MarkOutcomeSeen"/>; closing counts as
    /// having seen the outcome, so it raises no indicator afterwards.</summary>
    public void Dismiss(DateTimeOffset finishedAt)
    {
        lock (_lock)
        {
            if ((_snapshot.Phase == TransferImportPhase.Complete || _snapshot.Phase == TransferImportPhase.Failed) && _snapshot.FinishedAt == finishedAt)
                _snapshot = _snapshot with { Dismissed = true, OutcomeSeen = true };
        }
    }

    /// <summary>Maps into the shared job shape for the combined status read
    /// (design.md D15). A total of 0 before the file has been read is reported
    /// as unknown (null) rather than zero, so the shared bar moves instead of
    /// sitting on an empty, zero-filled track (design.md D4).</summary>
    public JobSnapshot ToJobSnapshot()
    {
        var snapshot = Snapshot;
        var phase = snapshot.Phase switch
        {
            TransferImportPhase.NotStarted => JobPhase.NotStarted,
            TransferImportPhase.Running => JobPhase.Running,
            TransferImportPhase.Complete => JobPhase.Complete,
            TransferImportPhase.Failed => JobPhase.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(snapshot), snapshot.Phase, null),
        };

        return new JobSnapshot(
            phase, snapshot.Done, snapshot.Total == 0 ? null : snapshot.Total, snapshot.Error,
            snapshot.StartedAt, snapshot.FinishedAt, OutcomeSeen: snapshot.OutcomeSeen);
    }
}
