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
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = orderedAnimeIds[i], Position = i, SelectedAt = DateTimeOffset.UtcNow });
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
}
