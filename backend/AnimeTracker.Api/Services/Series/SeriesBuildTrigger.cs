using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Series;

public class SeriesBuildTrigger : ISeriesBuildTrigger
{
    // Bounds memory on a long-running process; large enough that a warm
    // store's steady stream of distinct franchises won't evict an id before
    // its build has had a chance to run. Not persisted — a restart simply
    // forgets what was already queued, which just means a possible re-enqueue.
    private const int MaxTrackedIds = 10_000;

    private readonly ConcurrentQueue<int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly ConcurrentDictionary<int, byte> _enqueued = new();
    private readonly ConcurrentQueue<int> _enqueuedOrder = new();

    public void Enqueue(int animeId)
    {
        if (!_enqueued.TryAdd(animeId, 0))
            return; // already queued (or already built) since app start — no-op

        _enqueuedOrder.Enqueue(animeId);
        TrimTrackedIds();

        _queue.Enqueue(animeId);
        _signal.Release();
    }

    public async Task<int> WaitAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        _queue.TryDequeue(out var animeId);
        return animeId;
    }

    private void TrimTrackedIds()
    {
        while (_enqueuedOrder.Count > MaxTrackedIds && _enqueuedOrder.TryDequeue(out var oldest))
            _enqueued.TryRemove(oldest, out _);
    }
}
