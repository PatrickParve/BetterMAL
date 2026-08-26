using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// list-editing "A saved score places the anime in the ranking" (design.md
// D6, tasks.md 2.4/2.5): UserAnimeEntryEditService's gate on ScoreChanged,
// exercised end to end through UpdateEntryAsync.
public class UserAnimeEntryEditServiceRankingPlacementTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntryEditService CreateService(AnimeTrackerDbContext db) =>
        new(
            db,
            new FakeEntrySyncScheduler(),
            new FakeEpisodeScheduleService(),
            new FakeAiringRefreshTrigger(),
            new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db)),
            new FakeServiceScopeFactory(),
            NullLogger<UserAnimeEntryEditService>.Instance);

    private static void Seed(AnimeTrackerDbContext db, int animeId, string title, int? myScore, WatchStatus status)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = title, AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = status, MyScore = myScore, EpisodesWatched = 12 });
    }

    [Fact]
    public async Task ScoringAnEntryForTheFirstTimePlacesItLastInItsTier()
    {
        using var db = CreateDb();
        Seed(db, 1, "Existing", myScore: 8, WatchStatus.Completed);
        Seed(db, 2, "Newly scored", myScore: null, WatchStatus.Completed);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.UpdateEntryAsync(2, new UserAnimeEntryEditRequest { MyScore = 8 });

        var order = await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public async Task ReSavingTheSameScoreMovesNothing()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", myScore: 8, WatchStatus.Completed);
        Seed(db, 2, "B", myScore: null, WatchStatus.Completed);
        Seed(db, 3, "C", myScore: null, WatchStatus.Completed);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.UpdateEntryAsync(2, new UserAnimeEntryEditRequest { MyScore = 8 }); // -> [1, 2]
        await service.UpdateEntryAsync(3, new UserAnimeEntryEditRequest { MyScore = 8 }); // -> [1, 2, 3]

        // A bogus re-placement here would append entry 2 again, past entry 3.
        await service.UpdateEntryAsync(2, new UserAnimeEntryEditRequest { MyScore = 8 });

        var order = await new TopAnimeSelectionRepository(db).GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 2, 3], order);
    }

    [Fact]
    public async Task ChangingTheScoreMovesTheEntryToTheEndOfTheNewTierAndLeavesTheOldTierIntact()
    {
        using var db = CreateDb();
        Seed(db, 1, "Eight", myScore: 8, WatchStatus.Completed);
        Seed(db, 2, "Nine", myScore: 9, WatchStatus.Completed);
        Seed(db, 3, "Re-scored", myScore: 8, WatchStatus.Completed);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var rankingService = new AnimeRankingService(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db));

        await service.UpdateEntryAsync(3, new UserAnimeEntryEditRequest { MyScore = 9 });

        var snapshot = await rankingService.GetSnapshotAsync();
        var eights = snapshot.RankedEntries.Where(e => e.MyScore == 8).Select(e => e.AnimeId).ToList();
        var nines = snapshot.RankedEntries.Where(e => e.MyScore == 9).Select(e => e.AnimeId).ToList();
        Assert.Equal([1], eights);
        Assert.Equal([2, 3], nines);
    }

    private sealed class FakeEntrySyncScheduler : IEntrySyncScheduler
    {
        public void ScheduleSync(int animeId) { }
    }

    private sealed class FakeAiringRefreshTrigger : IAiringRefreshTrigger
    {
        public void Enqueue(int animeId) { }
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            Task.FromResult<DateTimeOffset?>(null);
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult<int?>(null);
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }
}
