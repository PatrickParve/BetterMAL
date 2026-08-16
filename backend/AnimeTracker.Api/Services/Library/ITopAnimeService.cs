namespace AnimeTracker.Api.Services.Library;

/// <summary>Top Anime rankings — each selectable list (see
/// <see cref="TopAnimeRankingType"/>) is re-fetched at most once per local
/// calendar day, and only when that list is actually visited. A list that has
/// never been visited is never proactively fetched by anything.</summary>
public interface ITopAnimeService
{
    Task<List<TopAnimeItemDto>> GetRankingAsync(string rankingType, CancellationToken ct = default);
}
