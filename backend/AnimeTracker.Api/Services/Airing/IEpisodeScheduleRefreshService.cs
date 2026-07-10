namespace AnimeTracker.Api.Services.Airing;

/// <summary>Refreshes the in-memory per-episode schedule cache from AniList for
/// the my-list shows where it matters — currently-airing, upcoming, and
/// just-finished — so the weekly schedule and "Airing today" reflect real
/// per-episode dates (including breaks).</summary>
public interface IEpisodeScheduleRefreshService
{
    Task RefreshAsync(CancellationToken ct);
}
