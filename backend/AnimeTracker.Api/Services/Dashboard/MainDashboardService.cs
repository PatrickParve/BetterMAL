using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Dashboard;

public class MainDashboardService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeScheduleService scheduleService,
    IBroadcastLocalTimeConverter broadcastConverter) : IMainDashboardService
{
    public async Task<MainDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;
        // "Airing today" is a local-calendar concept; ResolveForDate expects a
        // local reference date, so derive today in the broadcast-local zone
        // rather than from UTC (which is off by a day near local midnight).
        var today = broadcastConverter.GetLocalDate(now);
        var currentSeasonQuarter = SeasonCalendar.GetSeasonFor(today);

        var currentlyWatching = entries
            .Where(e => e.Status == WatchStatus.Watching)
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new CurrentlyWatchingItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                scheduleService.EpisodesAiredAsOf(e.Anime, now),
                ToEta(scheduleService.NextAiringInstant(e.Anime, now), now)))
            .ToList();

        // Resolve each show against today's local date through the shared
        // schedule logic: it bounds shows to their real air window and skips
        // break weeks, so a finished show (past window) or a show on hiatus
        // today no longer leaks in.
        var airingToday = entries
            .Select(e => (Entry: e, Episode: scheduleService.ResolveOnLocalDate(e.Anime, today)))
            .Where(x => x.Episode is not null)
            .OrderBy(x => x.Episode!.LocalTime)
            .Select(x => new AiringTodayItemDto(x.Entry.AnimeId, x.Entry.Anime.Title, x.Entry.Anime.EnglishTitle, x.Entry.Anime.PictureUrl, x.Episode!.LocalTime.ToString("HH:mm"), x.Episode!.EpisodeNumber))
            .ToList();

        // Season membership is derived from AiredFrom rather than the cached
        // SeasonAnimeListing rows: those only exist for seasons the user has
        // browsed, which would make this section's contents depend on
        // unrelated navigation history instead of each anime's own data.
        var currentSeason = entries
            .Where(e => e.Anime.AiringStatus == "currently_airing"
                || (e.Anime.AiredFrom is { } from
                    && from <= today
                    && SeasonCalendar.GetSeasonFor(from) == currentSeasonQuarter))
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new CurrentSeasonItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                e.Anime.MalScore,
                e.Anime.PopularityRank,
                scheduleService.EpisodesAiredAsOf(e.Anime, now),
                e.Anime.AiringStatus == "finished_airing"))
            .ToList();

        return new MainDashboardDto(currentlyWatching, airingToday, currentSeason);
    }

    private static NextEpisodeEtaDto? ToEta(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var remaining = instant - now;
        return new NextEpisodeEtaDto(remaining.Days, remaining.Hours);
    }
}
