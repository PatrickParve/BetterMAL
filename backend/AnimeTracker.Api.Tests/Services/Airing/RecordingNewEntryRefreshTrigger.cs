using AnimeTracker.Api.Services.Airing;

namespace AnimeTracker.Api.Tests.Services.Airing;

/// <summary>A new-entry queue that only remembers what was queued, in order — for
/// the tests of the paths that create a list entry and queue it. <paramref
/// name="onEnqueue"/> runs at the moment of each enqueue, so a test can check what
/// had been saved by then (the worker reads the database, so a queued id must
/// already have its entry).</summary>
internal sealed class RecordingNewEntryRefreshTrigger(Action<int>? onEnqueue = null) : INewEntryRefreshTrigger
{
    public List<int> Enqueued { get; } = [];

    public void Enqueue(int animeId)
    {
        Enqueued.Add(animeId);
        onEnqueue?.Invoke(animeId);
    }

    public Task<IReadOnlyList<int>> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
}
