namespace AnimeTracker.Api.Data.Repositories;

/// <summary>An anime's index within each of the four season-browser
/// orderings over the whole listing it came from — popularity, MAL score,
/// alphabetical, and my score. These are positions in a total order, so
/// filtering items out of a listing preserves the relative order of what's
/// left (design D3).</summary>
public record SeasonSortOrder(int Popularity, int MalScore, int Alphabetical, int MyScore);

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
    bool InMyList,
    SeasonSortOrder SortOrder);

/// <summary>Read-only access to cached season listings. Backs the season
/// page's render path — never call the MAL client from here.</summary>
public interface ISeasonRepository
{
    /// <summary>When this season's listing was last live-fetched, or null if
    /// it has never been fetched.</summary>
    Task<DateTimeOffset?> GetLastFetchedAsync(int year, string season, CancellationToken ct = default);

    /// <summary>Whether this season has any cached listing row at all,
    /// unfiltered by hentai — <see cref="GetListingAsync(int,string,bool,CancellationToken)"/>'s
    /// result is filtered by hideHentai, so trusting its emptiness here would
    /// make a hentai-only season that genuinely has a MAL listing look
    /// unlisted.</summary>
    Task<bool> HasListingAsync(int year, string season, CancellationToken ct = default);

    /// <summary>Point-set form of <see cref="HasListingAsync"/> — a year is
    /// passed as its four (year, season) points, and answers "does any of
    /// them have a cached listing row".</summary>
    Task<bool> HasListingAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default);

    /// <summary>The season's whole cached listing (design D1) — a season
    /// holds at most a few hundred anime, so one read is cheap enough to
    /// serve the page for as long as it's open. Every item carries its
    /// position under all four orderings (design D3); the caller sorts and
    /// filters client-side rather than asking for a server-side page.</summary>
    Task<List<SeasonAnimeItem>> GetListingAsync(
        int year, string season, bool hideHentai, CancellationToken ct = default);

    /// <summary>Point-set form of <see cref="GetListingAsync(int,string,bool,CancellationToken)"/> —
    /// a year is passed as its four (year, season) points. The ordering and
    /// filtering rules are identical whatever the point count: the season
    /// endpoint passes one point, the year endpoint passes four, and both
    /// read the same query.</summary>
    Task<List<SeasonAnimeItem>> GetListingAsync(
        IReadOnlyCollection<(int Year, string Season)> points, bool hideHentai, CancellationToken ct = default);

    /// <summary>The horizon resolver's inputs in one round trip, no MAL call:
    /// for each requested (year, season) point, its fetch timestamp and
    /// whether it has any cached listing; plus the latest (year, season) that
    /// has any SeasonAnimeListing row at all (null if nothing is cached
    /// yet).</summary>
    Task<SeasonHorizonInputs> GetHorizonInputsAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken ct = default);
}

public record SeasonHorizonPoint(int Year, string Season, DateTimeOffset? LastFetchedAt, bool HasListings);

public record SeasonHorizonInputs(List<SeasonHorizonPoint> Points, (int Year, string Season)? LatestCachedSeason);
