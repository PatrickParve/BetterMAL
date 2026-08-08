namespace AnimeTracker.Api.Services.Airing;

/// <summary>Queues one anime id for an immediate (not waiting for the hourly
/// tick) airing-data refresh — fired from UserAnimeEntryEditService when an
/// airing/upcoming anime is added to my list. A queue, not a coalescing
/// wake-up flag, since distinct anime ids must each be processed.</summary>
public interface IAiringRefreshTrigger
{
    void Enqueue(int animeId);
    Task<int> WaitAsync(CancellationToken ct);
}
