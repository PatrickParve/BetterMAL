using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Ranking;

// design.md D5/D6 (tasks.md 2.5): PlaceLastInTierAsync's tail placement, and
// the slot-preserving write's behaviour through a drop/undrop cycle.
public class AnimeRankingServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeRankingService CreateService(AnimeTrackerDbContext db) =>
        new(new UserAnimeEntryRepository(db), new TopAnimeSelectionRepository(db));

    private static void Seed(
        AnimeTrackerDbContext db, int animeId, string title, int? myScore,
        WatchStatus status = WatchStatus.Completed, string? mediaType = "tv")
    {
        var anime = new AnimeMetadata { Id = animeId, Title = title, MediaType = mediaType, AiringStatus = "finished_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = status, MyScore = myScore });
    }

    [Fact]
    public async Task AFirstScoreLandsLastInItsTierEvenWhenEveryOtherMemberWasNeverPlaced()
    {
        using var db = CreateDb();
        Seed(db, 1, "Existing A", myScore: 8);
        Seed(db, 2, "Existing B", myScore: 8);
        Seed(db, 3, "Existing C", myScore: 8);
        Seed(db, 4, "New scorer", myScore: 8);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        await service.PlaceLastInTierAsync(4, 8);

        var snapshot = await service.GetSnapshotAsync();
        var order = snapshot.RankedEntries.Where(e => e.MyScore == 8).Select(e => e.AnimeId).ToList();
        Assert.Equal([1, 2, 3, 4], order);
    }

    [Fact]
    public async Task ARescoreMovesTheAnimeToTheEndOfTheNewTierAndLeavesTheOldTierIntact()
    {
        using var db = CreateDb();
        Seed(db, 1, "Eight A", myScore: 8);
        Seed(db, 2, "Eight B", myScore: 8);
        Seed(db, 3, "Nine A", myScore: 9);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.PlaceLastInTierAsync(1, 8); // establishes an explicit position among the 8s

        var entry = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 1);
        entry.MyScore = 9;
        await db.SaveChangesAsync();
        await service.PlaceLastInTierAsync(1, 9);

        var snapshot = await service.GetSnapshotAsync();
        var eights = snapshot.RankedEntries.Where(e => e.MyScore == 8).Select(e => e.AnimeId).ToList();
        var nines = snapshot.RankedEntries.Where(e => e.MyScore == 9).Select(e => e.AnimeId).ToList();

        Assert.Equal([2], eights);
        Assert.Equal([3, 1], nines);
    }

    [Fact]
    public async Task ADroppedAnimeKeepsItsStoredSlotThroughATierEditAndReturnsToItWhenUnDropped()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", myScore: 7);
        Seed(db, 2, "B", myScore: 7);
        Seed(db, 3, "C", myScore: 7);
        Seed(db, 4, "D", myScore: 7);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var selectionRepository = new TopAnimeSelectionRepository(db);

        await service.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(7, [4, 3, 2, 1])]);
        var slotBefore = (await selectionRepository.GetOrderedAnimeIdsAsync()).IndexOf(3);

        var c = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 3);
        c.Status = WatchStatus.Dropped;
        await db.SaveChangesAsync();

        // Edit the tier's remaining hand-orderable members while C is away —
        // C is no longer a valid member of the request, so it never appears.
        await service.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(7, [1, 2, 4])]);
        var slotAfterEdit = (await selectionRepository.GetOrderedAnimeIdsAsync()).IndexOf(3);
        Assert.Equal(slotBefore, slotAfterEdit);

        c.Status = WatchStatus.Completed;
        await db.SaveChangesAsync();

        var snapshot = await service.GetSnapshotAsync();
        Assert.Contains(3, snapshot.RankedEntries.Where(e => e.MyScore == 7).Select(e => e.AnimeId));
        var slotAfterUndrop = (await selectionRepository.GetOrderedAnimeIdsAsync()).IndexOf(3);
        Assert.Equal(slotBefore, slotAfterUndrop);
    }

    [Fact]
    public async Task ApplyTierOrderAsyncAdvancesTheRankingTime()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", myScore: 7);
        Seed(db, 2, "B", myScore: 7);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var before = DateTimeOffset.UtcNow;
        await service.ApplyTierOrderAsync([new AnimeRankingTierOrderRequest(7, [2, 1])]);
        var after = DateTimeOffset.UtcNow;

        var state = await db.RankingStates.SingleAsync();
        Assert.InRange(state.ModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task MoveAdjacentAsyncAdvancesTheRankingTime()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", myScore: 7);
        Seed(db, 2, "B", myScore: 7);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var before = DateTimeOffset.UtcNow;
        await service.MoveAdjacentAsync(2, 1);
        var after = DateTimeOffset.UtcNow;

        var state = await db.RankingStates.SingleAsync();
        Assert.InRange(state.ModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task MoveAdjacentAsyncThatReturnsEarlyLeavesTheRankingTimeUntouched()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", myScore: 7);
        Seed(db, 2, "B", myScore: 8); // different score: MoveAdjacentAsync returns early
        Seed(db, 3, "C", myScore: 7);
        await db.SaveChangesAsync();
        var service = CreateService(db);
        await service.MoveAdjacentAsync(3, 1); // a real write, so a RankingState row exists
        var before = (await db.RankingStates.SingleAsync()).ModifiedAt;

        await service.MoveAdjacentAsync(2, 1); // different scores: returns early

        var after = (await db.RankingStates.SingleAsync()).ModifiedAt;
        Assert.Equal(before, after);
    }
}
