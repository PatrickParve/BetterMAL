namespace AnimeTracker.Api.Services.Sync;

/// <summary>Signals the corrective re-sync background loop to run. Multiple
/// signals while a run is pending coalesce into one — this is a wake-up flag,
/// not a queue.</summary>
public interface IResyncTrigger
{
    void Signal();
    Task WaitAsync(CancellationToken ct);
}
