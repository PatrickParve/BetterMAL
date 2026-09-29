namespace AnimeTracker.Api.Services.Airing;

public class AiringFullRefreshTrigger : IAiringFullRefreshTrigger
{
    private readonly SemaphoreSlim _signal = new(0, 1);
    private int _force;

    public void Signal(bool force = false)
    {
        // Before the release, so the loop that wakes for it reads the mode.
        if (force)
            Volatile.Write(ref _force, 1);

        try
        {
            _signal.Release();
        }
        catch (SemaphoreFullException)
        {
            // A run is already pending/in-flight — coalesce, nothing more to do.
        }
    }

    public async Task<bool> WaitAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        return Interlocked.Exchange(ref _force, 0) == 1;
    }
}
