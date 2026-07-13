using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Search;

public record SearchPageDto(string Query, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount);
