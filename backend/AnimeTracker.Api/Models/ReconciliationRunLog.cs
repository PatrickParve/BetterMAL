namespace AnimeTracker.Api.Models;

/// <summary>Tracks when the scheduled weekly reconciliation last ran, so
/// ReconciliationBackgroundService can pick up where it left off after a
/// restart instead of always waiting a fresh week from process start. Unlike
/// SeasonFetchLog there is no dimension key — at most one row ever exists.</summary>
public class ReconciliationRunLog
{
    public int Id { get; set; }
    public DateTimeOffset LastRunAt { get; set; }

    /// <summary>Null means this attempt hasn't recorded an ending — either
    /// it's still running, or it was interrupted (a restart) before it could
    /// record one. Reset to null at attempt time, then set to true/false when
    /// the run ends (design.md D7).</summary>
    public bool? LastRunFailed { get; set; }
    public string? LastRunError { get; set; }

    /// <summary>Whether the outcome of the run <see cref="LastRunAt"/> names
    /// has been reported as seen. Reset to false at attempt time, alongside
    /// <see cref="LastRunFailed"/>/<see cref="LastRunError"/>, so a new run's
    /// outcome is unseen again (design.md D6).</summary>
    public bool LastRunOutcomeSeen { get; set; }
}
