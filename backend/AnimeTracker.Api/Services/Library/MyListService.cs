using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;

namespace AnimeTracker.Api.Services.Library;

public class MyListService(
    IUserAnimeEntryRepository entryRepository,
    IEpisodeScheduleService scheduleService) : IMyListService
{
    public async Task<List<MyListItemDto>> GetMyListAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;
        return entries
            .Select(e => new MyListItemDto(
                e.AnimeId,
                e.Anime.Title,
                e.Anime.EnglishTitle,
                e.Anime.PictureUrl,
                e.Anime.MediaType,
                e.Anime.TotalEpisodes,
                e.Anime.MalScore,
                e.Anime.AiringStatus,
                scheduleService.EpisodesAiredAsOf(e.Anime, now),
                UserAnimeEntryDto.FromEntity(e)))
            .ToList();
    }
}
