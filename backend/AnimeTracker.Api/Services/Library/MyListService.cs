using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Ranking;

namespace AnimeTracker.Api.Services.Library;

public class MyListService(
    IUserAnimeEntryRepository entryRepository,
    ITopAnimeSelectionRepository topAnimeSelectionRepository,
    IEpisodeScheduleService scheduleService,
    IAiringWatchStatusService airingWatchStatusService) : IMyListService
{
    public async Task<List<MyListItemDto>> GetMyListAsync(CancellationToken ct = default)
    {
        var entries = await entryRepository.GetAllAsync(ct);
        var now = DateTimeOffset.UtcNow;

        // The ranking is derived from the same whole-list entries this page
        // already loads (anime-ranking capability), so MyRank costs no extra
        // database round trip beyond the stored order itself.
        var storedOrder = await topAnimeSelectionRepository.GetOrderedAnimeIdsAsync(ct);
        var snapshot = AnimeRankingSnapshot.Build(entries, storedOrder);

        // Resolved once in bulk for the whole list (episode-airing-data), so
        // settling one (design.md D6) costs nothing extra here beyond the
        // single read this page already needed for its own EpisodesAired
        // column — the same dictionary backs both.
        var airedSoFarByAnimeId = await scheduleService.EpisodesAiredAsOfAsync(entries.Select(e => e.Anime).ToList(), now, ct);

        await airingWatchStatusService.SettleAsync(entries, airedSoFarByAnimeId, ct);

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
                UserAnimeEntryDto.FromEntity(e),
                snapshot.RankOf(e.AnimeId),
                e.Anime.PopularityRank));
        }
        return items;
    }
}
