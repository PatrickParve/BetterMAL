namespace AnimeTracker.Api.Services.Dashboard;

public record NextEpisodeEtaDto(int Days, int Hours);

public record CurrentlyWatchingItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EpisodesWatched,
    int? TotalEpisodes,
    NextEpisodeEtaDto? NextEpisode);

public record AiringTodayItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string LocalTime);

public record CurrentSeasonItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EpisodesWatched,
    int? TotalEpisodes,
    double? MalScore,
    int? PopularityRank);

public record MainDashboardDto(
    List<CurrentlyWatchingItemDto> CurrentlyWatching,
    List<AiringTodayItemDto> AiringToday,
    List<CurrentSeasonItemDto> CurrentSeason);
