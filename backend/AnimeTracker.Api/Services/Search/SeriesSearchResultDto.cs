namespace AnimeTracker.Api.Services.Search;

/// <summary>A matched series as shown in search — display fields sourced from
/// its root anime (design.md decision 1), entry count covering every member
/// including extras (decision 8). The series id is the root entry's MAL id
/// (key-series-by-root-anime-id design.md D1/D7), so a link built from it
/// points at the root anime.
/// <para><c>MalScore</c> is the series' main-line MAL average, the figure the
/// series page and the series browser show as the main series' average
/// (<c>SeriesAverages.Mal</c> over the main-line members, improve-search-page
/// design.md D2), or null when no main-line entry has a MAL score. Only the
/// results page's series cards draw it: the dropdown's rows
/// (<see cref="AnimeSearchResultDto.ForSeries"/>) show no scores.</para></summary>
public record SeriesSearchResultDto(
    int SeriesId, string Title, string? EnglishTitle, string? PictureUrl, int EntryCount, double? MalScore);
