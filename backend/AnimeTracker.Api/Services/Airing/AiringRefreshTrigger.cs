using System.Collections.Concurrent;

namespace AnimeTracker.Api.Services.Airing;

public class AiringRefreshTrigger : IAiringRefreshTrigger
{
    private readonly ConcurrentQueue<int> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public void Enqueue(int animeId)
    {
        _queue.Enqueue(animeId);
        _signal.Release();
    }

    public async Task<int> WaitAsync(CancellationToken ct)
    {
        await _signal.WaitAsync(ct);
        _queue.TryDequeue(out var animeId);
        return animeId;
    }
}
