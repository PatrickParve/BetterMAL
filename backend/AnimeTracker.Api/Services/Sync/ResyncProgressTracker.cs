namespace AnimeTracker.Api.Services.Sync;

public class ResyncProgressTracker : IResyncProgressTracker
{
    private readonly Lock _lock = new();
    private ResyncStatusSnapshot _snapshot = new(ResyncPhase.NotStarted, 0, 0);

    public ResyncStatusSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    public void Start(int total)
    {
        lock (_lock)
            _snapshot = new ResyncStatusSnapshot(ResyncPhase.Running, 0, total);
    }

    public void ReportProgress(int synced)
    {
        lock (_lock)
            _snapshot = _snapshot with { Synced = synced };
    }

    public void Complete()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = ResyncPhase.Complete };
    }
}
