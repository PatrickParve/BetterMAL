using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

/// <summary>One my-list row. Grouping by status, status filtering, sorting,
/// and rank numbers are all client-side concerns computed against the flat
/// list this shapes. <c>Genres</c> are the anime's stored MyAnimeList genres,
/// themes and demographics in MyAnimeList's order — never null, empty when
/// none were fetched — so the genre filter runs over the loaded list.
/// <c>Studios</c> are the studios MyAnimeList credits the anime with, in its
/// order, under the same rules (never null, empty when none are stored), so the
/// studio filter runs over the loaded list the same way. <c>Source</c> is the
/// source material as MyAnimeList stores it and <c>AiredFrom</c> the air-start
/// date, null when unknown, so the source and era filters run over the loaded list
/// too.</summary>
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
    List<string> Genres,
    List<string> Studios,
    string? Source,
    DateOnly? AiredFrom);
