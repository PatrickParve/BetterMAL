using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Relations;
using AnimeTracker.Api.Services.Scheduling;
using AnimeTracker.Api.Services.Updates;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Updates;

// anime-updates spec, "The updates menu shows the last 30 days, newest
// first": the navbar menu's window is GetRecentAsync()'s own 30-day
// constant, not something the dashboard imposes on it.
public class AnimeUpdateServiceRecentWindowTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeUpdateService CreateService(AnimeTrackerDbContext db) =>
        new(db, new RelationResolver(db), new BroadcastLocalTimeConverter());

    [Fact]
    public async Task A31DayOldUpdateIsExcludedFromTheRecentWindowButRemainsInHistory()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow.AddDays(-31), Kinds = AnimeUpdateKinds.StartDateReleased });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var recent = await service.GetRecentAsync();
        var history = await service.GetHistoryAsync();

        Assert.Empty(recent);
        Assert.Single(history);
    }

    [Fact]
    public async Task AFreshUpdateIsReturnedByBothTheRecentWindowAndHistory()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Solo" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Status = WatchStatus.Watching });
        db.AnimeUpdates.Add(new AnimeUpdate { AnimeId = 1, DetectedAt = DateTimeOffset.UtcNow.AddDays(-1), Kinds = AnimeUpdateKinds.EpisodeCountReleased });
        await db.SaveChangesAsync();

        var service = CreateService(db);
        var recent = await service.GetRecentAsync();
        var history = await service.GetHistoryAsync();

        Assert.Single(recent);
        Assert.Single(history);
    }
}
