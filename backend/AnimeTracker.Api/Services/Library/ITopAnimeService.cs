namespace AnimeTracker.Api.Services.Library;

/// <summary>Top Anime rankings — each selectable list (see
/// <see cref="TopAnimeRankingType"/>) is re-fetched at most once per local
/// calendar day, and only when that list is actually visited via
/// <see cref="RefreshAsync"/>. A list that has never been visited is never
/// proactively fetched by anything.</summary>
public interface ITopAnimeService
{
    /// <summary>Cache-only read; never calls MAL.</summary>
    Task<List<TopAnimeItemDto>> GetRankingAsync(string rankingType, CancellationToken ct = default);

    /// <summary>The visit path: refreshes this list from MAL, subject to the
    /// once-per-local-day rule and per-list single-flight.</summary>
    Task<TopAnimeRefreshResultDto> RefreshAsync(string rankingType, CancellationToken ct = default);
}
