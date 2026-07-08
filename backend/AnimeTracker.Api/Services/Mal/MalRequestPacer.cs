namespace AnimeTracker.Api.Services.Mal;

/// <summary>Gates outbound MAL API calls to a conservative minimum interval.
/// MAL has no published rate limit and throttles bursts with a 403; this keeps
/// normal usage nowhere near whatever that threshold actually is.</summary>
public class MalRequestPacer(TimeSpan? minInterval = null)
{
    private readonly TimeSpan _minInterval = minInterval ?? TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastDispatch = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            var wait = _minInterval - (DateTimeOffset.UtcNow - _lastDispatch);
            if (wait > TimeSpan.Zero)
                await Task.Delay(wait, ct);
            _lastDispatch = DateTimeOffset.UtcNow;
        }
        finally
        {
            _gate.Release();
        }
    }
}
