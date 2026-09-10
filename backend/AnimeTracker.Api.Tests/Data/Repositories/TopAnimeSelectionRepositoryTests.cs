using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// design.md D5's slot-preserving single pass (tasks.md 2.6): untouched ids
// keep their exact slots, each edited group's own relative order survives
// regardless of how groups interleave with untouched ids in the existing
// order (this is also the mechanism that keeps a scope-hidden hand-ordered
// member's position intact — from ReplaceOrderAsync's point of view a
// hidden member is simply an untouched id), and a never-placed id is
// appended at the end.
public class TopAnimeSelectionRepositoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void SeedKnown(AnimeTrackerDbContext db, params int[] animeIds)
    {
        foreach (var id in animeIds)
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}" });
    }

    private static void SeedStoredOrder(AnimeTrackerDbContext db, params int[] orderedAnimeIds)
    {
        for (var i = 0; i < orderedAnimeIds.Length; i++)
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = orderedAnimeIds[i], Position = i });
    }

    [Fact]
    public async Task EditedGroupsRefillExactlyTheirOwnSlotsWhileUntouchedIdsKeepTheirs()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3, 4, 5, 6);
        SeedStoredOrder(db, 1, 2, 3, 4, 5, 6);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        // Two edited groups (e.g. two tiers), concatenated: {2,5} -> [5,2]
        // and {3,6} -> [6,3], with untouched ids 1 and 4 interleaved between
        // their original slots.
        await repository.ReplaceOrderAsync([5, 2, 6, 3]);

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 5, 2, 4, 6, 3], order);
        Assert.Equal(0, order.IndexOf(1));
        Assert.Equal(3, order.IndexOf(4));
    }

    [Fact]
    public async Task AnIdWithNoExistingSlotIsAppendedAtTheEnd()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3, 99);
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await repository.ReplaceOrderAsync([2, 99]);

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 2, 3, 99], order);
    }

    [Fact]
    public async Task UnknownAnimeIdIsRejected()
    {
        using var db = CreateDb();
        SeedKnown(db, 1);
        SeedStoredOrder(db, 1);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await Assert.ThrowsAsync<UnknownAnimeIdsException>(() => repository.ReplaceOrderAsync([1, 404]));
    }

    [Fact]
    public async Task NoRankingStateRowExistsBeforeAnyWrite()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3);
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();

        Assert.Null(await db.RankingStates.FirstOrDefaultAsync());
    }

    [Fact]
    public async Task ReplaceOrderAsyncStampsTheRankingEvenWhenTheWrittenOrderMatchesTheStoredOne()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3);
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        var before = DateTimeOffset.UtcNow;
        await repository.ReplaceOrderAsync([1, 2, 3]); // identical to the stored order
        var after = DateTimeOffset.UtcNow;

        var state = await db.RankingStates.SingleAsync();
        Assert.InRange(state.ModifiedAt!.Value, before, after);
    }

    [Fact]
    public async Task ASecondReplaceOrderAsyncWriteAdvancesTheRankingTime()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3);
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await repository.ReplaceOrderAsync([2, 1]);
        var first = (await db.RankingStates.SingleAsync()).ModifiedAt;

        await repository.ReplaceOrderAsync([1, 2]);
        var second = (await db.RankingStates.SingleAsync()).ModifiedAt;

        Assert.True(second > first);
    }

    [Fact]
    public async Task ReplaceOrderAsyncMergeExample()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3); // A=1, B=2, C=3
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await repository.ReplaceOrderAsync([3, 1]); // [C, A]

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([3, 2, 1], order); // [C, B, A]
    }

    [Fact]
    public async Task ReplaceAllAsyncDropsAnimeMissingFromTheList()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2, 3);
        SeedStoredOrder(db, 1, 2, 3);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await repository.ReplaceAllAsync([3, 1], DateTimeOffset.UtcNow);

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([3, 1], order);
    }

    [Fact]
    public async Task ReplaceAllAsyncStoresTheGivenTimeNotNow()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        SeedStoredOrder(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        var pastTime = DateTimeOffset.UtcNow.AddDays(-3);

        await repository.ReplaceAllAsync([1, 2], pastTime);

        var state = await db.RankingStates.SingleAsync();
        Assert.Equal(pastTime, state.ModifiedAt);
    }

    [Fact]
    public async Task ReplaceAllAsyncARepeatedIdTakesItsFirstPosition()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        SeedStoredOrder(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);

        await repository.ReplaceAllAsync([1, 2, 1], DateTimeOffset.UtcNow);

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 2], order);
    }

    [Fact]
    public async Task ReplaceAllAsyncWithAnEmptyListLeavesNoRowsAndRecordsTheGivenTime()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        SeedStoredOrder(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        var time = DateTimeOffset.UtcNow;

        await repository.ReplaceAllAsync([], time);

        Assert.Empty(await repository.GetOrderedAnimeIdsAsync());
        var state = await db.RankingStates.SingleAsync();
        Assert.Equal(time, state.ModifiedAt);
    }

    [Fact]
    public async Task ReplaceAllAsyncAnUnknownIdThrowsAndLeavesOrderAndTimeUnchanged()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        SeedStoredOrder(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        await repository.ReplaceAllAsync([2, 1], DateTimeOffset.UtcNow.AddDays(-1));
        var stateBefore = (await db.RankingStates.SingleAsync()).ModifiedAt;

        await Assert.ThrowsAsync<UnknownAnimeIdsException>(
            () => repository.ReplaceAllAsync([1, 404], DateTimeOffset.UtcNow));

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([2, 1], order);
        var stateAfter = (await db.RankingStates.SingleAsync()).ModifiedAt;
        Assert.Equal(stateBefore, stateAfter);
    }

    [Fact]
    public async Task ReplaceAllAsyncWorksWhenNoRankingStateRowExistedYet()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        var time = DateTimeOffset.UtcNow;

        await repository.ReplaceAllAsync([1, 2], time);

        var order = await repository.GetOrderedAnimeIdsAsync();
        Assert.Equal([1, 2], order);
        var state = await db.RankingStates.SingleAsync();
        Assert.Equal(time, state.ModifiedAt);
    }

    // --- 5.1: GetModifiedAtAsync (device-transfer's ranking-time read) ---

    [Fact]
    public async Task GetModifiedAtAsyncReturnsNullWhenNoRankingStateRowExists()
    {
        using var db = CreateDb();
        var repository = new TopAnimeSelectionRepository(db);

        Assert.Null(await repository.GetModifiedAtAsync());
    }

    [Fact]
    public async Task GetModifiedAtAsyncReturnsTheStoredTimeAfterReplaceAllAsync()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        var time = DateTimeOffset.UtcNow.AddDays(-1);

        await repository.ReplaceAllAsync([1, 2], time);

        Assert.Equal(time, await repository.GetModifiedAtAsync());
    }

    [Fact]
    public async Task GetModifiedAtAsyncReturnsTheStoredTimeAfterAnEmptyingReplaceAllAsync()
    {
        using var db = CreateDb();
        SeedKnown(db, 1, 2);
        SeedStoredOrder(db, 1, 2);
        await db.SaveChangesAsync();
        var repository = new TopAnimeSelectionRepository(db);
        var time = DateTimeOffset.UtcNow;

        await repository.ReplaceAllAsync([], time);

        Assert.Equal(time, await repository.GetModifiedAtAsync());
    }
}
