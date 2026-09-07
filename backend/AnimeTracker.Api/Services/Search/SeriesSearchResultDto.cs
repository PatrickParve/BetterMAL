namespace AnimeTracker.Api.Services.Search;

/// <summary>A matched series as shown in search — display fields sourced from
/// its root anime (design.md decision 1), entry count covering every member
/// including extras (decision 8). The series id is the root entry's MAL id
/// (key-series-by-root-anime-id design.md D1/D7), so a link built from it
/// points at the root anime.</summary>
public record SeriesSearchResultDto(int SeriesId, string Title, string? EnglishTitle, string? PictureUrl, int EntryCount);
