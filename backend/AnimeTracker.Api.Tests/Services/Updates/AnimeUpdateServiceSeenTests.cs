using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Updates;

// The seen flag (store-seen-updates-on-server design.md D1/D3/D4): carried
// on the read DTOs untouched by eligibility, and set through MarkSeenAsync
// without a concurrency token, so repeating or overlapping a batch is
// harmless.
public class AnimeUpdateServiceSeenTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeUpdateService CreateService(AnimeTrackerDbContext db) =>
        new(db, new AnimeUpdateRelevance(db, new RelationResolver(db)), new BroadcastLocalTimeConverter());

    [Fact]
    public async Task GetRecentAndGetHistoryCarryEachRowsSeen()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced, Seen = true });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 2, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased, Seen = false });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var recent = await service.GetRecentAsync();
        var history = await service.GetHistoryAsync();

        Assert.True(recent.Single(u => u.Id == 1).Seen);
        Assert.False(recent.Single(u => u.Id == 2).Seen);
        Assert.True(history.Single(u => u.Id == 1).Seen);
        Assert.False(history.Single(u => u.Id == 2).Seen);
    }

    [Fact]
    public async Task MarkSeenAsyncSetsExactlyTheGivenIdsAndLeavesTheOthersUnseen()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 2, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        await CreateService(db).MarkSeenAsync([1]);

        var updates = await db.AnimeUpdates.AsNoTracking().ToListAsync();
        Assert.True(updates.Single(u => u.Id == 1).Seen);
        Assert.False(updates.Single(u => u.Id == 2).Seen);
    }

    [Fact]
    public async Task MarkSeenAsyncIgnoresIdsWithNoRow()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        await CreateService(db).MarkSeenAsync([1, 999]);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.True(update.Seen);
    }

    [Fact]
    public async Task MarkSeenAsyncIsANoOpForAnEmptyList()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        await CreateService(db).MarkSeenAsync([]);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.False(update.Seen);
    }

    [Fact]
    public async Task MarkSeenAsyncIsHarmlessWhenRepeated()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        await service.MarkSeenAsync([1]);
        await service.MarkSeenAsync([1]);

        var update = Assert.Single(await db.AnimeUpdates.AsNoTracking().ToListAsync());
        Assert.True(update.Seen);
    }

    [Fact]
    public async Task AnUnseenUpdateOnADroppedStandaloneEntryIsAbsentFromGetRecentAsync()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Dropped });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced, Seen = false });
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentAsync();

        Assert.Empty(result);
    }

    [Fact]
    public async Task RestoringTheEntryBringsItBackStillUnseenAndASeenSiblingStillSeen()
    {
        using var db = CreateDb();
        var entry = new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Dropped };
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(entry);
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 1, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.Announced, Seen = false });
        db.AnimeUpdates.Add(new AnimeUpdate { Id = 2, AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow, Kinds = AnimeUpdateKinds.StartDateReleased, Seen = true });
        await db.SaveChangesAsync();

        entry.Status = WatchStatus.Watching;
        await db.SaveChangesAsync();

        var result = await CreateService(db).GetRecentAsync();

        Assert.False(result.Single(u => u.Id == 1).Seen);
        Assert.True(result.Single(u => u.Id == 2).Seen);
    }
}
