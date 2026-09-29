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

    /// <summary>Builds the series of <paramref name="animeId"/> for first-run
    /// setup and reports how that went, without projecting it. Setup-only:
    /// <see cref="GetSeriesAsync"/> spends a small fetch budget so a page visit
    /// stays quick, but setup builds every series in the background and would
    /// otherwise leave them all partial, so this passes
    /// <see cref="SeriesGraphBuilder.Unbounded"/> for both budgets (the member
    /// cap still applies). It shares <see cref="GetSeriesAsync"/>'s single-flight
    /// key, its stored-series check and its fall-back to what is already stored,
    /// so an up-to-date series is not rebuilt and two callers build once. It
    /// skips the projection because setup never shows the series, and the
    /// projection reads the TMDB cache, the ranking and the episode schedule
    /// for nothing.</summary>
    Task<SetupBuildOutcome> BuildForSetupAsync(int animeId, CancellationToken ct = default);

    /// <summary>The id of the stored series containing <paramref name="animeId"/>,
    /// or null when it belongs to none — the by-anime picture backfill
    /// endpoint's lookup (design.md D13), without the build-or-refresh
    /// <see cref="GetSeriesAsync"/> does.</summary>
    Task<int?> FindSeriesIdAsync(int animeId, CancellationToken ct = default);
}
