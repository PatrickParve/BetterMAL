namespace AnimeTracker.Api.Services.Import;

public class ImportProgressTracker : IImportProgressTracker
{
    private readonly Lock _lock = new();
    private ImportStatusSnapshot _snapshot = new(ImportPhase.NotStarted, 0, 0);

    public ImportStatusSnapshot Snapshot
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
            _snapshot = new ImportStatusSnapshot(ImportPhase.Running, 0, total);
    }

    public void ReportProgress(int synced)
    {
        lock (_lock)
            _snapshot = _snapshot with { Synced = synced };
    }

    public void Complete()
    {
        lock (_lock)
            _snapshot = _snapshot with { Phase = ImportPhase.Complete };
    }
}
