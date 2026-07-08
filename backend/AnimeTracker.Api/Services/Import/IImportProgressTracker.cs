namespace AnimeTracker.Api.Services.Import;

public enum ImportPhase
{
    NotStarted,
    Running,
    Complete,
}

public record ImportStatusSnapshot(ImportPhase Phase, int Synced, int Total);

/// <summary>In-memory import progress, read by the status endpoint. Not
/// persisted — on restart the import re-derives accurate progress by re-paging
/// the MAL list and counting anime already present locally.</summary>
public interface IImportProgressTracker
{
    ImportStatusSnapshot Snapshot { get; }
    void Start(int total);
    void ReportProgress(int synced);
    void Complete();
}
