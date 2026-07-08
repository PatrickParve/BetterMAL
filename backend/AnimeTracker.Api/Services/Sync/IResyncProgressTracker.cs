namespace AnimeTracker.Api.Services.Sync;

public enum ResyncPhase
{
    NotStarted,
    Running,
    Complete,
}

public record ResyncStatusSnapshot(ResyncPhase Phase, int Synced, int Total);

/// <summary>In-memory corrective re-sync progress, read by the settings-page
/// status endpoint. Not persisted — a re-sync is a manually triggered, one-time
/// action, not something that needs to resume across restarts.</summary>
public interface IResyncProgressTracker
{
    ResyncStatusSnapshot Snapshot { get; }
    void Start(int total);
    void ReportProgress(int synced);
    void Complete();
}
