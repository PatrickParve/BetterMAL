namespace AnimeTracker.Api.Services.Series;

public class SeriesBulkBuildTrigger : ISeriesBulkBuildTrigger
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
