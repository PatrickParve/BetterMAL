namespace AnimeTracker.Api.Services.Library;

/// <summary>Global Top Anime ranking — re-fetched at most once per local
/// calendar day, and only when the page is actually visited. A ranking that
/// has never been visited is never proactively fetched by anything.</summary>
public interface ITopAnimeService
{
    Task<List<TopAnimeItemDto>> GetRankingAsync(CancellationToken ct = default);
}
