using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Search;

/// <summary>Series ride along in <see cref="Series"/> rather than mixed into
/// <see cref="Items"/> (design.md decision 5) — populated only under the
/// relevance sort, at most 3, and never counted in <see
/// cref="TotalCount"/>. <see cref="MalSearchFailed"/> is true when the live
/// MAL search failed and these results came from local storage instead
/// (design.md D7) — false for an empty query and for a MAL search that
/// simply found nothing.</summary>
public record SearchPageDto(string Query, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount, List<SeriesSearchResultDto> Series, bool MalSearchFailed);
