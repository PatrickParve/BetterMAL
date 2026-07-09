namespace AnimeTracker.Api.Services.Search;

public record SearchAnimeItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int? TotalEpisodes,
    string? MediaType,
    double? MalScore,
    int? PopularityRank,
    int? MyScore);

public record SearchPageDto(string Query, List<SearchAnimeItemDto> Items, int Offset, int Limit, int TotalCount);
