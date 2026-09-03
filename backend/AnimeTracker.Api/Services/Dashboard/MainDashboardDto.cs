using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Dashboard;

public record NextEpisodeEtaDto(int Days, int Hours);

public record CurrentlyWatchingItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EpisodesWatched,
    int? TotalEpisodes,
    int? EpisodesAired,
    bool CurrentlyAiring,
    NextEpisodeEtaDto? NextEpisode,
    WatchStatus Status,
    // gate-editing-on-aired-episodes: the raw airing status, since
    // CurrentlyAiring alone can't distinguish finished_airing from
    // not_yet_aired once EpisodesAired is unknown — the frontend gate needs
    // that distinction to decide whether to draw the progress row at all.
    string? AiringStatus);

public record AiringTodayItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    string LocalTime,
    int? EpisodeNumber);

public record CurrentSeasonItemDto(
    int AnimeId,
    string Title,
    string? EnglishTitle,
    string? PictureUrl,
    int EpisodesWatched,
    int? TotalEpisodes,
    double? MalScore,
    int? PopularityRank,
    int? EpisodesAired,
    bool FinishedAiring);

public record MainDashboardDto(
    List<CurrentlyWatchingItemDto> CurrentlyWatching,
    List<AiringTodayItemDto> AiringToday,
    List<CurrentSeasonItemDto> CurrentSeason);
