using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Season;

namespace AnimeTracker.Api.Services.Dashboard;

public class MainDashboardService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeScheduleService scheduleService,
    ICompletedEntryReopenService reopenService,
    IBroadcastLocalTimeConverter broadcastConverter) : IMainDashboardService
{
    public async Task<MainDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // design.md D6: must run before the Watching/Rewatching filter below
        // — a still-Completed entry discarded by that filter first would
        // never be reopened, and would never reach Currently watching however
        // often the page is opened. Not free like the other read paths: this
        // one resolves aired counts only for the Completed-and-airing subset,
        // in one bulk query, since nothing else here needs them.
        var completedAiring = entries.Where(e => e.Status == WatchStatus.Completed && e.Anime.AiringStatus == "currently_airing").ToList();
        if (completedAiring.Count > 0)
        {
            var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(completedAiring.Select(e => e.Anime).ToList(), now, ct);
            await reopenService.ReopenAsync(completedAiring, airedSoFarByAnimeId, ct);
        }

        // "Airing today" is a local-calendar concept; ResolveForDate expects a
        // local reference date, so derive today in the broadcast-local zone
        // rather than from UTC (which is off by a day near local midnight).
        var today = broadcastConverter.GetLocalDate(now);
        var currentSeasonQuarter = SeasonCalendar.GetSeasonFor(today);

        var currentlyWatching = new List<CurrentlyWatchingItemDto>();
        // main-dashboard: Rewatching entries are runs in progress just like
        // Watching ones (design.md D5), so they share this section and its
        // ordering rather than being grouped separately.
        foreach (var e in OrderCurrentlyWatching(entries.Where(e => e.Status is WatchStatus.Watching or WatchStatus.Rewatching)))
        {
            currentlyWatching.Add(new CurrentlyWatchingItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                await scheduleService.EpisodesAiredAsOfAsync(e.Anime, now, ct),
                e.Anime.AiringStatus == "currently_airing",
                ToEta(await scheduleService.NextAiringInstantAsync(e.Anime, now, ct), now),
                e.Status,
                e.Anime.AiringStatus));
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

    // Internal so MainDashboardServiceOrderingTests can assert the rule
    // against plain UserAnimeEntry objects without standing up the fakes
    // GetDashboardAsync itself requires.
    internal static IEnumerable<UserAnimeEntry> OrderCurrentlyWatching(IEnumerable<UserAnimeEntry> entries) =>
        entries
            .OrderByDescending(e => e.EpisodesWatched)
            .ThenBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase);

    private static NextEpisodeEtaDto? ToEta(DateTimeOffset? nextInstant, DateTimeOffset now)
    {
        if (nextInstant is not { } instant)
            return null;

        var remaining = instant - now;
        return new NextEpisodeEtaDto(remaining.Days, remaining.Hours);
    }
}
