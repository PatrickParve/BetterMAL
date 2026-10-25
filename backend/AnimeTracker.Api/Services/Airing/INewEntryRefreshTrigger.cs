namespace AnimeTracker.Api.Services.Airing;

/// <summary>Queues an anime that has just entered my list for an immediate (not
/// waiting for the hourly tick or the metadata job's next pass) data fetch —
/// fired after the save that commits each new list entry, by every path that
/// creates one: the app's own add (UserAnimeEntryEditService), the MyAnimeList
/// list import, an accepted list check and a declined held removal. A queue, not
/// a coalescing wake-up flag, since distinct anime ids must each be processed.</summary>
public interface INewEntryRefreshTrigger
{
    void Enqueue(int animeId);

    /// <summary>Waits until something is queued, then returns every id queued so
    /// far at once — distinct, in the order they were first queued — so the worker
    /// can fetch a burst (an import, an accepted list check) as one batch.</summary>
    Task<IReadOnlyList<int>> WaitAsync(CancellationToken ct);
}
