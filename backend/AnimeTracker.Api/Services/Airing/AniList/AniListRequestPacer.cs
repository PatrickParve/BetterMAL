namespace AnimeTracker.Api.Services.Airing.AniList;

/// <summary>Gates outbound AniList calls to a conservative minimum interval so
/// a multi-page schedule fetch (or many anime refreshed back to back) stays
/// within AniList's 30 req/min limit regardless of how many requests one
/// anime's refresh ends up needing.</summary>
public class AniListRequestPacer(TimeSpan? minInterval = null)
{
    private readonly TimeSpan _minInterval = minInterval ?? TimeSpan.FromMilliseconds(4500);
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
