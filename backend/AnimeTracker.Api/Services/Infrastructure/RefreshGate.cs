using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Infrastructure;

/// <summary>String-keyed single-flight lock: two concurrent visit-triggered
/// refreshes for the same subject (a season, the Top Anime ranking, a single
/// anime's detail) collapse into one live fetch instead of racing. Callers
/// build their own key per subject (e.g. <c>$"season:{year}:{season}"</c>,
/// <c>"top-anime"</c>, <c>$"anime:{animeId}"</c>) so different subjects never
/// wait on each other. Registered as a singleton so the lock map is shared
/// across requests.</summary>
public class RefreshGate
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public async Task<IDisposable> LockAsync(string key, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
