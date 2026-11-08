using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

/// <summary>One my-list row. Grouping by status, status filtering, sorting,
/// and rank numbers are all client-side concerns computed against the flat
/// list this shapes. <c>Genres</c> are the anime's stored MyAnimeList genres,
/// themes and demographics in MyAnimeList's order — never null, empty when
/// none were fetched — so the genre filter runs over the loaded list.</summary>
public record MyListItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string? MediaType,
    int? TotalEpisodes,
    double? MalScore,
    string? AiringStatus,
    int? EpisodesAired,
    UserAnimeEntryDto Entry,
    int? MyRank,
    int? PopularityRank,
    List<string> Genres);
