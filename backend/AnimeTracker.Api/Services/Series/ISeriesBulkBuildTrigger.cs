namespace AnimeTracker.Api.Services.Series;

/// <summary>Signals the manual "build all series from my list" background run
/// (settings page) to start. Multiple signals while a run is pending/in-flight
/// coalesce into one — this is a wake-up flag, not a queue, since the run
/// computes its own targets fresh each time rather than consuming a supplied
/// list. Distinct from ISeriesBuildTrigger, which queues individual anime ids
/// for the on-read backfill case (mirrors IAiringFullRefreshTrigger).</summary>
public interface ISeriesBulkBuildTrigger
{
    void Signal();
    Task WaitAsync(CancellationToken ct);
}
