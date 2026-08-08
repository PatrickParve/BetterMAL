namespace AnimeTracker.Api.Services.Airing;

/// <summary>Signals the manual "refresh all airing data" background loop
/// (settings page) to run. Multiple signals while a run is pending coalesce
/// into one — this is a wake-up flag, not a queue. Distinct from
/// IAiringRefreshTrigger, which queues individual anime ids for the
/// add-to-list case.</summary>
public interface IAiringFullRefreshTrigger
{
    void Signal();
    Task WaitAsync(CancellationToken ct);
}
