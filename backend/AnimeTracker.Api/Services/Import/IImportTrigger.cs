namespace AnimeTracker.Api.Services.Import;

/// <summary>Signals the import background loop to (re)run. Multiple signals
/// while a run is pending coalesce into one — this is a wake-up flag, not a
/// queue.</summary>
public interface IImportTrigger
{
    void Signal();
    Task WaitAsync(CancellationToken ct);
}
