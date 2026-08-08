namespace AnimeTracker.Api.Services.Airing;

public class AiringFullRefreshProgressTracker : IAiringFullRefreshProgressTracker
{
    private readonly Lock _lock = new();
    private AiringFullRefreshStatusSnapshot _snapshot = new(AiringFullRefreshPhase.NotStarted, 0, 0);

    public AiringFullRefreshStatusSnapshot Snapshot
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
            _snapshot = new AiringFullRefreshStatusSnapshot(AiringFullRefreshPhase.Running, 0, total);
    }

    public void ReportProgress(int synced)
    {
        lock (_lock)
            _snapshot = _snapshot with { Synced = synced };
    }

    public void Complete()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = AiringFullRefreshPhase.Complete };
    }
}
