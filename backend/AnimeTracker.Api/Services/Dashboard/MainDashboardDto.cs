using AnimeTracker.Api.Models;

namespace AnimeTracker.Api.Services.Dashboard;

public record NextEpisodeEtaDto(int Days, int Hours);

// Shared by MainDashboardService and AnimeDetailService (design.md D1) so the
// currently-watching carousel and the detail page's countdown always agree
// for the same episode. Rounds up to the next whole hour before splitting
// into days/hours, so a remainder under an hour never reads 0d 0h.
public static class NextEpisodeEta
{
    public static NextEpisodeEtaDto? From(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var totalHours = (int)Math.Ceiling((instant - now).TotalHours);
        return new NextEpisodeEtaDto(totalHours / 24, totalHours % 24);
    }
}

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
    string? AiringStatus,
    // main-dashboard: decides whether a finished rewatch opens the
    // completion prompt.
    int? MyScore,
    // main-dashboard: what the Home undo restores.
    DateOnly? CompletedAt);

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
