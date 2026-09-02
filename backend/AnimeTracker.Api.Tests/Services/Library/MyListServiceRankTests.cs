using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Library;

// anime-ranking capability read into my list (design.md D11, tasks.md 3.4):
// MyListItemDto.MyRank carries the entry's overall rank, null when the
// ranking doesn't cover it.
public class MyListServiceRankTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task RowsCarryTheAnimesOverallRankAndUnrankedRowsCarryNull()
    {
        using var db = CreateDb();
        var ranked = new AnimeMetadata { Id = 1, Title = "Ranked", AiringStatus = "finished_airing" };
        var unranked = new AnimeMetadata { Id = 2, Title = "Unranked", AiringStatus = "finished_airing" };
        db.AnimeMetadata.AddRange(ranked, unranked);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = ranked, Status = WatchStatus.Completed, MyScore = 8 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = unranked, Status = WatchStatus.PlanToWatch, MyScore = null });
        await db.SaveChangesAsync();

        var service = new MyListService(
            new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db), new FakeEpisodeScheduleService(), new NoopAiringWatchStatusService());

        var list = await service.GetMyListAsync();

        Assert.Equal(1, list.Single(i => i.AnimeId == 1).MyRank);
        Assert.Null(list.Single(i => i.AnimeId == 2).MyRank);
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            Task.FromResult<ResolvedEpisode?>(null);
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    private sealed class NoopAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }
}
