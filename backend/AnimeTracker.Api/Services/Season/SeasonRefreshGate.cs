using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Season;

/// <summary>Per-season single-flight lock: two concurrent refresh requests for
/// the same (year, season) collapse into one MAL fetch instead of racing.
/// Registered as a singleton so the lock map is shared across requests.</summary>
public class SeasonRefreshGate
{
    private readonly ConcurrentDictionary<(int Year, string Season), SemaphoreSlim> _locks = new();

    public async Task<IDisposable> LockAsync(int year, string season, CancellationToken ct)
    {
        var gate = _locks.GetOrAdd((year, season), _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        return new Releaser(gate);
    }

    private sealed class Releaser(SemaphoreSlim gate) : IDisposable
    {
        public void Dispose() => gate.Release();
    }
}
