using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

public class MyListService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeScheduleService scheduleService,
    ICompletedEntryReopenService reopenService) : IMyListService
{
    public async Task<List<MyListItemDto>> GetMyListAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // Resolved for every entry regardless, so reopening a Completed one
        // (design.md D6) costs nothing extra here beyond the lookup this page
        // already needed for its own EpisodesAired column.
        var airedSoFarByAnimeId = new Dictionary<int, int>();
        foreach (var e in entries)
            if (await scheduleService.EpisodesAiredAsOfAsync(e.Anime, now, ct) is { } aired)
                airedSoFarByAnimeId[e.AnimeId] = aired;

        await reopenService.ReopenAsync(entries, airedSoFarByAnimeId, ct);

        var items = new List<MyListItemDto>();
        foreach (var e in entries)
        {
            items.Add(new MyListItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.Anime.MediaType,
                e.Anime.TotalEpisodes,
                e.Anime.MalScore,
                e.Anime.AiringStatus,
                airedSoFarByAnimeId.TryGetValue(e.AnimeId, out var episodesAired) ? episodesAired : null,
                UserAnimeEntryDto.FromEntity(e)));
        }
        return items;
    }
}
