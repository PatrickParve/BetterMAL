namespace AnimeTracker.Api.Services.Relations;

/// <summary>Background counterpart to the airing-lookup relation piggyback
/// (<c>EpisodeScheduleRefreshService</c>): fetches AniList relations for
/// my-list anime that hold an Unconfirmed MAL edge but would otherwise never
/// get an AniList lookup for any other reason (e.g. a finished show with no
/// airing schedule to track).</summary>
public interface IRelationAdjudicationService
{
    /// <summary>Looks up AniList relations for up to <paramref name="batchSize"/>
    /// candidate anime in one paged AniList request. Returns how many were
    /// looked up.</summary>
    Task<int> RunBatchAsync(int batchSize, CancellationToken ct = default);
}
