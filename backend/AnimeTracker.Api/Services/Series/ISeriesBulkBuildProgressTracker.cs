namespace AnimeTracker.Api.Services.Series;

public enum SeriesBulkBuildPhase
{
    NotStarted,
    Running,
    Complete,
    Failed,
}

/// <summary>Built counts targets processed, not builds run — one build
/// typically stores several other targets' membership at once, and those are
/// counted as processed without a build of their own (design.md decision
/// 6).</summary>
public record SeriesBulkBuildStatusSnapshot(SeriesBulkBuildPhase Phase, int Built, int Total);

/// <summary>In-memory progress for the manual "build all series from my
/// list" action, read by the settings-page status endpoint. Not persisted —
/// like the airing full refresh, this is a manually triggered, one-time
/// action that doesn't need to resume across restarts.</summary>
public interface ISeriesBulkBuildProgressTracker
{
    SeriesBulkBuildStatusSnapshot Snapshot { get; }

    /// <summary>Reports a run as in flight before the background service has
    /// resolved targets — called from the trigger request itself, so the
    /// response the controller returns already shows a run in progress
    /// instead of whatever the previous run left (design.md decision
    /// 5).</summary>
    void MarkPending();

    void Start(int total);
    void ReportProgress(int built);
    void Complete();

    /// <summary>Reports a run that died before or during target resolution —
    /// with `MarkPending` in play, a silent `NotStarted` would otherwise
    /// become a silent-looking "Building…" that never finishes (design.md
    /// decision 6).</summary>
    void Fail();
}
