using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Services.Dashboard;

public class MainDashboardService(
    IUserAnimeEntryRepository entryRepository,
    IBroadcastLocalTimeConverter broadcastConverter) : IMainDashboardService
{
    public async Task<MainDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var currentlyWatching = entries
            .Where(e => e.Status == WatchStatus.Watching)
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new CurrentlyWatchingItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                ToEta(broadcastConverter.NextBroadcastInstant(e.Anime, now), now)))
            .ToList();

        var airingToday = entries
            .Select(e => (Entry: e, Slot: broadcastConverter.ResolveForDate(e.Anime.BroadcastDayOfWeek, e.Anime.BroadcastTime, today)))
            .Where(x => x.Slot is not null)
            .OrderBy(x => x.Slot!.Time)
            .Select(x => new AiringTodayItemDto(x.Entry.AnimeId, x.Entry.Anime.Title, x.Entry.Anime.PictureUrl, x.Slot!.Time.ToString("HH:mm")))
            .ToList();

        var currentSeason = entries
            .Where(e => e.Anime.AiringStatus == "currently_airing")
            .OrderBy(e => e.Anime.Title, StringComparer.OrdinalIgnoreCase)
            .Select(e => new CurrentSeasonItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.PictureUrl,
                e.EpisodesWatched,
                e.Anime.TotalEpisodes,
                e.Anime.MalScore,
                e.Anime.PopularityRank))
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
