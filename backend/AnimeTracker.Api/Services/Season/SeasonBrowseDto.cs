using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Season;

public record SeasonPageDto(int Year, string Season, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount);
