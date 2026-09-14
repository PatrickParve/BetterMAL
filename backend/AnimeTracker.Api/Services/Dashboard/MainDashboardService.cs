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
    IAiringWatchStatusService airingWatchStatusService,
    IBroadcastLocalTimeConverter broadcastConverter) : IMainDashboardService
{
    public async Task<MainDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllForListViewAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // design.md D7: resolved once in bulk for the whole list, rather than
        // per card inside the DTO loop below — SettleAsync's completion
        // direction, the caught-up filter below, the carousel's own
        // episodesAired field, and the current-season section further down
        // all read this same dictionary rather than re-querying.
        var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(entries.Select(e => e.Anime).ToList(), now, ct);

        // design.md D6: must run before the Watching/Rewatching selection
        // below — a still-Completed entry read first would never reach
        // Currently watching however often the page is opened, and a
        // Watching entry that just reached its total (the AniList-fallback
        // case) needs to complete before the caught-up filter sees it.
        await airingWatchStatusService.SettleAsync(entries, airedSoFarByAnimeId, ct);

        // "Airing today" is a local-calendar concept; ResolveForDate expects a
        // local reference date, so derive today in the broadcast-local zone
        // rather than from UTC (which is off by a day near local midnight).
        var today = broadcastConverter.GetLocalDate(now);
        var currentSeasonQuarter = SeasonCalendar.GetSeasonFor(today);

        var currentlyWatching = new List<CurrentlyWatchingItemDto>();
        // main-dashboard: Rewatching entries are runs in progress just like
        // Watching ones (design.md D5), so they share this section and its
        // ordering rather than being grouped separately. design.md D7: an
        // entry that has watched everything aired so far has nothing to
        // watch right now — no airing-status condition belongs here, since a
        // stale status would only reintroduce the lag EverythingHasAired
        // exists to route around. `>=` so an entry ahead of the app's aired
        // figure isn't shown as caught up; `a > 0` so an unresolved aired
        // count never hides an entry. An entry that has watched its full run
        // never reaches this filter — SettleAsync (or the edit path) already
        // moved it to Completed above.
        var caughtUp = entries.Where(e => e.Status is WatchStatus.Watching or WatchStatus.Rewatching
            && !(airedSoFarByAnimeId.TryGetValue(e.AnimeId, out var a) && a > 0 && e.EpisodesWatched >= a));

        // Materialised so the same ordered set backs both the bulk countdown
        // read below and the DTO loop — one read for every card's next
        // episode instead of one per card (episode-airing-data), the same
        // shape as AiringScheduleService.GetWeekAsync's single range query.
        var orderedCurrentlyWatching = OrderCurrentlyWatching(caughtUp).ToList();
        var nextAiringInstantByAnimeId = await scheduleService.NextAiringInstantAsync(orderedCurrentlyWatching.Select(e => e.Anime).ToList(), now, ct);
        foreach (var e in orderedCurrentlyWatching)
        {
            currentlyWatching.Add(new CurrentlyWatchingItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                airedSoFarByAnimeId.TryGetValue(e.AnimeId, out var aired) ? aired : null,
                e.Anime.AiringStatus == "currently_airing",
                NextEpisodeEta.From(nextAiringInstantByAnimeId.TryGetValue(e.AnimeId, out var nextInstant) ? nextInstant : null, now),
                e.Status,
                e.Anime.AiringStatus));
        }

        // Resolved once in bulk for the whole list against today's local
        // date (episode-airing-data), rather than per entry: it reads only
        // stored AniList rows, so a finished show or a show on hiatus today
        // still never leaks in, and an anime absent from the result means
        // nothing airs — the same shape AiringScheduleService.GetWeekAsync
        // uses for its week-wide range query.
        var airingTodayByAnimeId = await scheduleService.ResolveOnLocalDateAsync(entries.Select(e => e.Anime).ToList(), today, ct);
        var airingToday = new List<(UserAnimeEntry Entry, ResolvedEpisode Episode)>();
        foreach (var e in entries)
        {
            if (airingTodayByAnimeId.TryGetValue(e.AnimeId, out var episode))
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
                airedSoFarByAnimeId.TryGetValue(e.AnimeId, out var seasonAired) ? seasonAired : null,
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
}
