using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Library;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Library;

// add-watching-days-and-split-filters (library-views "My list source filter" and "My list
// era filter"): each my-list row carries the anime's stored source material and air-start
// date, so the client's two filters run over the loaded list. The rows come through the
// real repository's lean projection, so these also fail if either field ever leaves
// GetAllForListViewAsync's field list.
public class MyListServiceSourceAndEraTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MyListService BuildService(AnimeTrackerDbContext db) => new(
        new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db), new FakeEpisodeScheduleService(), new NoopAiringWatchStatusService());

    [Fact]
    public async Task EachRowCarriesItsOwnAnimesSourceAndAirStartDate()
    {
        using var db = CreateDb();
        var first = new AnimeMetadata { Id = 1, Title = "First", AiringStatus = "finished_airing", Source = "light_novel", AiredFrom = new DateOnly(2019, 12, 31) };
        var second = new AnimeMetadata { Id = 2, Title = "Second", AiringStatus = "finished_airing", Source = "manga", AiredFrom = new DateOnly(2020, 1, 1) };
        db.AnimeMetadata.AddRange(first, second);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = first, Status = WatchStatus.Completed },
            new UserAnimeEntry { AnimeId = 2, Anime = second, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var list = await BuildService(db).GetMyListAsync();

        var one = list.Single(i => i.AnimeId == 1);
        Assert.Equal(("light_novel", new DateOnly(2019, 12, 31)), (one.Source, one.AiredFrom));
        var two = list.Single(i => i.AnimeId == 2);
        Assert.Equal(("manga", new DateOnly(2020, 1, 1)), (two.Source, two.AiredFrom));
    }

    [Fact]
    public async Task AnAnimeWithNeitherArrivesWithBothNull()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Bare", AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed });
        await db.SaveChangesAsync();

        var item = (await BuildService(db).GetMyListAsync()).Single();

        Assert.Null(item.Source);
        Assert.Null(item.AiredFrom);
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
