namespace AnimeTracker.Api.Services.Setup;

/// <summary>Wakes the setup coordinator: the OAuth callback signals it once a
/// login is stored (setup starts by itself, with no further press), and again
/// after a Reconnect so a list read that was waiting on the login runs. Signals
/// while one is pending coalesce into one — a wake-up flag, not a queue, like
/// <see cref="Import.IImportTrigger"/>.</summary>
public interface ISetupTrigger
{
    void Signal();
    Task WaitAsync(CancellationToken ct);
}

public class SetupTrigger : ISetupTrigger
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
            // A wake-up is already pending — coalesce, nothing more to do.
        }
    }

    public Task WaitAsync(CancellationToken ct) => _signal.WaitAsync(ct);
}
