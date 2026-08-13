namespace AnimeTracker.Api.Services.Series;

/// <summary>Thrown when a favourite-order write targets a series id that
/// doesn't exist.</summary>
public class SeriesIdNotFoundException(int seriesId) : Exception($"Series {seriesId} was not found.");

/// <summary>Thrown when a favourite-order request names an anime id that
/// isn't a member of the series being reordered (design.md decision 11).</summary>
public class UnknownSeriesMemberIdsException(IReadOnlyCollection<int> animeIds)
    : Exception($"Anime id(s) are not members of this series: {string.Join(", ", animeIds)}");
