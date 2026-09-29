namespace AnimeTracker.Api.Services.Airing;

/// <summary>Signals the manual full airing-date refresh background loop
/// (settings page) to run: "Refresh all airing dates" or, with
/// <c>force</c>, "Force all airing dates". Multiple signals while a run is
/// pending coalesce into one — this is a wake-up flag, not a queue. Distinct from
/// IAiringRefreshTrigger, which queues individual anime ids for the
/// add-to-list case.</summary>
public interface IAiringFullRefreshTrigger
{
    /// <summary>Wakes the loop. A pending signal that was forced stays forced when a
    /// plain one coalesces into it.</summary>
    void Signal(bool force = false);

    /// <summary>Waits for a signal and reports whether it was forced.</summary>
    Task<bool> WaitAsync(CancellationToken ct);
}
