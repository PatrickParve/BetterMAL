namespace AnimeTracker.Api.Services.Airing;

/// <summary>In-memory store of per-episode air dates (from AniList), keyed by
/// MAL anime id. Populated by <see cref="EpisodeScheduleRefreshService"/> and
/// read on the airing/dashboard render paths. Deliberately not persisted: the
/// set of currently-airing shows is small and cheap to re-fetch on startup, so
/// a cache avoids a schema migration and a per-request network call.</summary>
public interface IEpisodeScheduleCache
{
    /// <summary>The cached schedule for this anime (may be empty when AniList
    /// had no data), or false when it was never fetched.</summary>
    bool TryGet(int animeId, out IReadOnlyList<EpisodeAiring> schedule);

    void Set(int animeId, IReadOnlyList<EpisodeAiring> schedule);
}
