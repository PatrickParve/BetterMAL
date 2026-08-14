namespace AnimeTracker.Api.Services.Search;

/// <summary>A matched series as shown in search — display fields sourced from
/// its root anime (design.md decision 1), entry count covering every member
/// including extras (decision 8).</summary>
public record SeriesSearchResultDto(int SeriesId, int RootAnimeId, string Title, string? EnglishTitle, string? PictureUrl, int EntryCount);
