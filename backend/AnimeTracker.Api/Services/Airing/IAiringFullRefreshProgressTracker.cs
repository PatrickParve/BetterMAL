namespace AnimeTracker.Api.Services.Airing;

public enum AiringFullRefreshPhase
{
    NotStarted,
    Running,
    Complete,
}

public record AiringFullRefreshStatusSnapshot(AiringFullRefreshPhase Phase, int Synced, int Total);

/// <summary>In-memory progress for the manual "refresh all airing data"
/// action, read by the settings-page status endpoint. Not persisted — like the
/// corrective MAL re-sync, this is a manually triggered, one-time action that
/// doesn't need to resume across restarts (the automatic one-time backfill
/// already covers that case).</summary>
public interface IAiringFullRefreshProgressTracker
{
    AiringFullRefreshStatusSnapshot Snapshot { get; }
    void Start(int total);
    void ReportProgress(int synced);
    void Complete();
}
