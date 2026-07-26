namespace AnimeTracker.Api.Models;

/// <summary>Tracks when the scheduled weekly reconciliation last ran, so
/// ReconciliationBackgroundService can pick up where it left off after a
/// restart instead of always waiting a fresh week from process start. Unlike
/// SeasonFetchLog there is no dimension key — at most one row ever exists.</summary>
public class ReconciliationRunLog
{
    public int Id { get; set; }
    public DateTimeOffset LastRunAt { get; set; }
}
