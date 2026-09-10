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
            _snapshot = new TransferImportStatusSnapshot(TransferImportPhase.Running, 0, 0, deviceName, exportedAt, null, null);
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
            _snapshot = _snapshot with { Phase = TransferImportPhase.Complete, Report = report };
    }

    public void Fail(string reason)
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = TransferImportPhase.Failed, Error = reason };
    }
}
