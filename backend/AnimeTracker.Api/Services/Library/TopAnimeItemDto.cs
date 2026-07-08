using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

/// <summary>One row of the global Top Anime ranking. Entry is null when the
/// anime isn't in my list yet — the row's action is "Add" in that case and
/// "Edit" otherwise, both driven by the same overlay via Entry/TotalEpisodes.</summary>
public record TopAnimeItemDto(
    int Rank,
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int? TotalEpisodes,
    double? MalScore,
    UserAnimeEntryDto? Entry);
