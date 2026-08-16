namespace AnimeTracker.Api.Services.Series;

public class SeriesBulkBuildProgressTracker : ISeriesBulkBuildProgressTracker
{
    private readonly Lock _lock = new();
    private SeriesBulkBuildStatusSnapshot _snapshot = new(SeriesBulkBuildPhase.NotStarted, 0, 0);

    public SeriesBulkBuildStatusSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return _snapshot;
        }
    }

    public void MarkPending()
    {
        lock (_lock)
            _snapshot = new SeriesBulkBuildStatusSnapshot(SeriesBulkBuildPhase.Running, 0, 0);
    }

    public void Start(int total)
    {
        lock (_lock)
            _snapshot = new SeriesBulkBuildStatusSnapshot(SeriesBulkBuildPhase.Running, 0, total);
    }

    public void ReportProgress(int built)
    {
        lock (_lock)
            _snapshot = _snapshot with { Built = built };
    }

    public void Complete()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = SeriesBulkBuildPhase.Complete };
    }

    public void Fail()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = SeriesBulkBuildPhase.Failed };
    }
}
