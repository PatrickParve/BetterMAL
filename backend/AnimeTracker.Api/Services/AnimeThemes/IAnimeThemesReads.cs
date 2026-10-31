namespace AnimeTracker.Api.Services.AnimeThemes;

/// <summary>What the anime page and the series page read of the stored
/// AnimeThemes catalogue (spec `anime-themes`, "No page depends on AnimeThemes").
/// Local rows only: neither method makes a request to AnimeThemes, and neither can
/// fail because AnimeThemes is down. Each is one index lookup, which is why the
/// themes arrive with the page's own read and not in a call of their own.</summary>
public interface IAnimeThemesReads
{
    /// <summary>The openings and endings of one anime, whether or not it is on my
    /// list or stored at all.</summary>
    Task<AnimeThemesDto> GetForAnimeAsync(int animeId, CancellationToken ct = default);

    /// <summary>The openings and endings of several anime in one query, grouped
    /// by anime in the order <paramref name="animeIds"/> gives them. An anime
    /// with none has no group, and an id given twice is read once.</summary>
    Task<SeriesThemesDto> GetForSeriesAsync(IReadOnlyList<int> animeIds, CancellationToken ct = default);
}
