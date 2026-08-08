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

        var currentlyWatching = new List<CurrentlyWatchingItemDto>();
        foreach (var e in entries.Where(e => e.Status == WatchStatus.Watching)
                     .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase))
        {
            currentlyWatching.Add(new CurrentlyWatchingItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                await scheduleService.EpisodesAiredAsOfAsync(e.Anime, now, ct),
                ToEta(await scheduleService.NextAiringInstantAsync(e.Anime, now, ct), now)));
        }

        // Resolve each show against today's local date through the shared
        // schedule service: it reads only stored AniList rows, so a finished
        // show or a show on hiatus today no longer leaks in.
        var airingToday = new List<(UserAnimeEntry Entry, ResolvedEpisode Episode)>();
        foreach (var e in entries)
        {
            if (await scheduleService.ResolveOnLocalDateAsync(e.Anime, today, ct) is { } episode)
                airingToday.Add((e, episode));
        }

        var airingTodayDtos = airingToday
            .OrderBy(x => x.Episode.LocalTime)
            .Select(x => new AiringTodayItemDto(x.Entry.AnimeId, x.Entry.Anime.Title, x.Entry.Anime.EnglishTitle, x.Entry.Anime.PictureUrl, x.Episode.LocalTime.ToString("HH:mm"), x.Episode.EpisodeNumber))
            .ToList();

        // Season membership is derived from AiredFrom rather than the cached
        // SeasonAnimeListing rows: those only exist for seasons the user has
        // browsed, which would make this section's contents depend on
        // unrelated navigation history instead of each anime's own data.
        var currentSeason = new List<CurrentSeasonItemDto>();
        foreach (var e in entries
                     .Where(e => e.Anime.AiringStatus == "currently_airing"
                         || (e.Anime.AiredFrom is { } from
                             && from <= today
                             && SeasonCalendar.GetSeasonFor(from) == currentSeasonQuarter))
                     .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase))
        {
            currentSeason.Add(new CurrentSeasonItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                e.Anime.MalScore,
                e.Anime.PopularityRank,
                await scheduleService.EpisodesAiredAsOfAsync(e.Anime, now, ct),
                e.Anime.AiringStatus == "finished_airing"));
        }

        return new MainDashboardDto(currentlyWatching, airingTodayDtos, currentSeason);
    }

    private static NextEpisodeEtaDto? ToEta(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var remaining = instant - now;
        return new NextEpisodeEtaDto(remaining.Days, remaining.Hours);
    }
}
