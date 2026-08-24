namespace AnimeTracker.Api.Data.Repositories;

public enum SeasonSortKey { Popularity, MalScore, Alphabetical, MyScore }

public record SeasonAnimeItem(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int? TotalEpisodes,
    string? MediaType,
    double? MalScore,
    int? PopularityRank,
    int? MyScore,
    bool InMyList);

/// <summary>Read-only access to cached season listings. Backs the season
/// page's render path — never call the MAL client from here.</summary>
public interface ISeasonRepository
{
    /// <summary>When this season's listing was last live-fetched, or null if
    /// it has never been fetched.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(int year, string season, CancellationToken ct = default);

    /// <summary>Whether this season has any cached listing row at all,
    /// unfiltered by type/hentai/in-my-list — <see cref="GetPageAsync"/>'s
    /// TotalCount can't serve this because it's computed after those filters,
    /// so a type filter would make a genuinely-listed season look unlisted.</summary>
    Task<bool> HasListingAsync(int year, string season, CancellationToken ct = default);

    /// <summary>Point-set form of <see cref="HasListingAsync"/> — a year is
    /// passed as its four (year, season) points, and answers "does any of
    /// them have a cached listing row".</summary>
    Task<bool> HasListingAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default);

    Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        int year, string season, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default);

    /// <summary>Point-set form of <see cref="GetPageAsync(int,string,SeasonSortKey,bool,bool,IReadOnlyCollection{string}?,int,int,CancellationToken)"/> —
    /// a year is passed as its four (year, season) points. The ordering,
    /// filtering, counting, and paging rules are identical whatever the point
    /// count: the season endpoint passes one point, the year endpoint passes
    /// four, and both read the same query.</summary>
    Task<(List<SeasonAnimeItem> Items, int TotalCount)> GetPageAsync(
        IReadOnlyCollection<(int Year, string Season)> points, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken ct = default);

    /// <summary>The horizon resolver's inputs in one round trip, no MAL call:
    /// for each requested (year, season) point, its fetch timestamp and
    /// whether it has any cached listing; plus the latest (year, season) that
    /// has any SeasonAnimeListing row at all (null if nothing is cached
    /// yet).</summary>
    Task<SeasonHorizonInputs> GetHorizonInputsAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default);
}

public record SeasonHorizonPoint(int Year, string Season, DateTimeOffset? LastFetchedAt, bool HasListings);

public record SeasonHorizonInputs(List<SeasonHorizonPoint> Points, (int Year, string Season)? LatestCachedSeason);
