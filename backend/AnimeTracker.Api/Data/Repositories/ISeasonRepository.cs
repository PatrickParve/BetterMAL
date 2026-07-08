namespace AnimeTracker.Api.Data.Repositories;

public enum SeasonSortKey { Popularity, MalScore, Alphabetical, MyScore }

public record SeasonAnimeItem(
    int AnimeId,
    string Title,
    string? PictureUrl,
    int? TotalEpisodes,
    string? MediaType,
    double? MalScore,
    int? PopularityRank,
    int? MyScore);

/// <summary>Read-only access to cached season listings. Backs the season
/// page's render path — never call the MAL client from here.</summary>
public interface ISeasonRepository
{
    /// <summary>When this season's listing was last live-fetched, or null if
    /// it has never been fetched.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(int year, string season, CancellationToken ct = default);

    Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        int year, string season, SeasonSortKey sort, int offset, int limit, CancellationToken ct = default);
}
