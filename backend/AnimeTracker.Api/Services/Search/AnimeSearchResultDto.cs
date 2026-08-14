namespace AnimeTracker.Api.Services.Search;

/// <summary>One row in the type-ahead dropdown. Kind discriminates "anime"
/// (Id is the anime id) from "series" (Id is the series id; RootAnimeId is
/// the navigation target and EntryCount is set) — design.md decision
/// 5.</summary>
public record AnimeSearchResultDto(
    string Kind, int Id, string Title, string? EnglishTitle, string? PictureUrl,
    int? RootAnimeId = null, int? EntryCount = null)
{
    public static AnimeSearchResultDto ForAnime(int id, string title, string? englishTitle, string? pictureUrl) =>
        new("anime", id, title, englishTitle, pictureUrl);

    public static AnimeSearchResultDto ForSeries(SeriesSearchResultDto series) =>
        new("series", series.SeriesId, series.Title, series.EnglishTitle, series.PictureUrl, series.RootAnimeId, series.EntryCount);
}
