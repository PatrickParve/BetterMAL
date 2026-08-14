namespace AnimeTracker.Api.Services.Series;

/// <summary>Queues an anime id whose search-matched franchise has never been
/// built, for a background series build — fired from AnimeSearchService when
/// a search's top-ranked anime match has no stored series (design.md
/// decision 6). A queue, not a coalescing wake-up flag, since distinct anime
/// ids must each be processed; Enqueue dedupes against ids already queued
/// since the app started, so a debounced type-ahead doesn't queue the same
/// build once per keystroke.</summary>
public interface ISeriesBuildTrigger
{
    void Enqueue(int animeId);
    Task<int> WaitAsync(CancellationToken ct);
}
