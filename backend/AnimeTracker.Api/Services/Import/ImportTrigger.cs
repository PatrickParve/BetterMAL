namespace AnimeTracker.Api.Services.Import;

public class ImportTrigger : IImportTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);

    public void Signal()
    {
        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A run is already pending/in-flight — coalesce, nothing more to do.
        }
    }

    public Task WaitAsync(CancellationToken ct) => _signal.WaitAsync(ct);
}
