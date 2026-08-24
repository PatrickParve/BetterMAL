using AnimeTracker.Api.Services.Airing;

namespace AnimeTracker.Api.Services.Series;

/// <summary>Assembles the Series page's read (add-series-browser design.md
/// D1/D2/D6): loads the ranking index, resolves aired-episode counts for its
/// currently-airing main-line members in one batched call, and returns the
/// listed series in the endpoint's deterministic default order. Builds
/// nothing and makes no MyAnimeList request — its cost is bounded by what is
/// stored.</summary>
public class SeriesListService(SeriesRankingLookup rankingLookup, IEpisodeScheduleService scheduleService)
{
    public async Task<SeriesListDto> GetSeriesListAsync(CancellationToken ct = default)
    {
        var index = await rankingLookup.LoadAsync(ct);
        var currentlyAiringIds = index.CurrentlyAiringMainLineAnimeIds();
        var airedEpisodesByAnimeId = currentlyAiringIds.Count > 0
            ? await scheduleService.EpisodesAiredAsOfAsync(currentlyAiringIds, DateTimeOffset.UtcNow, ct)
            : [];

        // Default order (design.md D1): my main-line average descending,
        // nulls last, then raw title case-insensitively.
        var items = index.ListedSeries(airedEpisodesByAnimeId)
            .OrderByDescending(item => item.MineMain.Value.HasValue)
            .ThenByDescending(item => item.MineMain.Value ?? 0)
            .ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new SeriesListDto(items);
    }
}
