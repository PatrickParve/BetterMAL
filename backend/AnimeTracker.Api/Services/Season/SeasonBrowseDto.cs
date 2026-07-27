using AnimeTracker.Api.Services.Library;

namespace AnimeTracker.Api.Services.Season;

// LastFetchedAt is null when this season has never been fetched — the client
// uses that to distinguish "never cached" (stay in a loading state) from
// "cached and genuinely empty".
public record SeasonPageDto(int Year, string Season, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount, DateTimeOffset? LastFetchedAt);

public record SeasonRefreshResultDto(bool Refreshed);
