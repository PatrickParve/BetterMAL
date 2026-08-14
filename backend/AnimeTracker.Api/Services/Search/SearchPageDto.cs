using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Search;

/// <summary>Series ride along in <see cref="Series"/> rather than mixed into
/// <see cref="Items"/> (design.md decision 5) — populated only under the
/// relevance sort, at most 3, and never counted in <see
/// cref="TotalCount"/>.</summary>
public record SearchPageDto(string Query, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount, List<SeriesSearchResultDto> Series);
