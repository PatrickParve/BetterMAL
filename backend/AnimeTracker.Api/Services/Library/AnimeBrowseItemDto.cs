namespace AnimeTracker.Api.Services.Library;

// Shared shape for a browsable (not-yet-in-my-list-scoped) anime card, used by
// both the season page and the search page — the two feature sets that list
// arbitrary MAL anime rather than the user's own entries.
public record AnimeBrowseItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int? TotalEpisodes,
    string? MediaType,
    double? MalScore,
    int? PopularityRank,
    int? MyScore);
