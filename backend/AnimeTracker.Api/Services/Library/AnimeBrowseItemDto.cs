namespace AnimeTracker.Api.Services.Library;

// An anime's index within each of the season browser's four orderings over
// the whole listing it came from (design D3) — populated only by the season
// and year listing reads; see AnimeBrowseItemDto.SortOrder.
public record BrowseSortOrderDto(int Popularity, int MalScore, int Alphabetical, int MyScore);

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
    int? MyScore,
    bool InMyList,
    // Optional trailing field (design D3): only the season and year listing
    // reads populate this, so the client can re-sort a loaded listing by a
    // server-computed key instead of reimplementing the ordering rules.
    // AnimeSearchService shares this record and leaves it null.
    BrowseSortOrderDto? SortOrder = null);
