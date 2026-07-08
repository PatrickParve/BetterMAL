using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

/// <summary>One my-list row. Grouping by status, status filtering, sorting,
/// and rank numbers are all client-side concerns computed against the flat
/// list this shapes.</summary>
public record MyListItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    int? TotalEpisodes,
    double? MalScore,
    UserAnimeEntryDto Entry);
