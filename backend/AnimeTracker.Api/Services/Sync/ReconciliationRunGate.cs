namespace AnimeTracker.Api.Services.Sync;

/// <summary>Ensures a manual reconciliation run and the weekly scheduled run
/// never compute a diff at the same time (design.md D6): each deletes the
/// held diff and adds its own, so running together could leave two diffs
/// behind. Whichever starts second waits for the other to finish — a manual
/// run that is waiting shows "Starting…", and the weekly run is short enough
/// that the wait is seconds.</summary>
public class ReconciliationRunGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public Task WaitAsync(CancellationToken ct) => _semaphore.WaitAsync(ct);

    public void Release() => _semaphore.Release();
}
