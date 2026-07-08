namespace AnimeTracker.Api.Services.Season;

public record SeasonAnimeItemDto(
    int AnimeId,
    string Title,
    string? PictureUrl,
    int? TotalEpisodes,
    string? MediaType,
    double? MalScore,
    int? PopularityRank,
    int? MyScore);

public record SeasonPageDto(int Year, string Season, List<SeasonAnimeItemDto> Items, int Offset, int Limit, int TotalCount);
