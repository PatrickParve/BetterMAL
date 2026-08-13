namespace AnimeTracker.Api.Services.Series;

public interface ISeriesService
{
    /// <summary>Resolves the series containing <paramref name="animeId"/>,
    /// building or refreshing it first when there is no stored series, the
    /// stored one is partial, or it was built more than 30 days ago. Throws
    /// <see cref="SeriesNotFoundException"/> when the anime isn't part of a
    /// series.</summary>
    Task<SeriesDto> GetSeriesAsync(int animeId, CancellationToken ct = default);

    /// <summary>Forces a rebuild with the larger fetch budget, regardless of
    /// how fresh the stored series is, then returns the same projection.</summary>
    Task<SeriesDto> RebuildSeriesAsync(int animeId, CancellationToken ct = default);

    /// <summary>Writes an explicit favourite order (design.md decision 11):
    /// the given anime ids receive ranks 0..n-1 in the order given, and every
    /// other member of the series has its rank cleared. Throws
    /// <see cref="SeriesIdNotFoundException"/> for an unknown series id, and
    /// <see cref="UnknownSeriesMemberIdsException"/> when an id isn't a
    /// member of the series.</summary>
    Task SetFavouriteOrderAsync(int seriesId, List<int> animeIds, CancellationToken ct = default);
}
