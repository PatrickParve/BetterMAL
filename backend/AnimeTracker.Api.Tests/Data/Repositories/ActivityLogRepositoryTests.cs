using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// record-mal-origin-activity design D6 (tasks.md 6.1/6.2): the recap's
// logged-progress query excludes MAL-origin rows, since a synced row's
// timestamp is when the sync ran, not when the episode was watched. The
// feed/history queries stay unfiltered — they exist to show everything.
public class ActivityLogRepositoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActivityLog EpisodeRow(int animeId, DateTimeOffset timestamp, ActivityChangeSource source) => new()
    {
        AnimeId = animeId,
        Timestamp = timestamp,
        ChangeType = ActivityChangeType.EpisodeIncremented,
        ChangeDetail = "Episode 5",
        Source = source,
    };

    [Fact]
    public async Task GetEpisodeProgressInRangeExcludesMalOriginRows()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        db.ActivityLogs.AddRange(
            EpisodeRow(1, from.AddDays(1), ActivityChangeSource.BetterMal),
            EpisodeRow(1, from.AddDays(2), ActivityChangeSource.MalReconciliation),
            EpisodeRow(1, from.AddDays(3), ActivityChangeSource.MalResync),
            EpisodeRow(1, from.AddDays(4), ActivityChangeSource.MalStartupImport));
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetEpisodeProgressInRangeAsync(from, to);

        var row = Assert.Single(rows);
        Assert.Equal(ActivityChangeSource.BetterMal, row.Source);
    }

    [Fact]
    public async Task ARangeWithOnlyMalOriginRowsReportsNoLoggedProgress()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        db.ActivityLogs.Add(EpisodeRow(1, from.AddDays(1), ActivityChangeSource.MalResync));
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetEpisodeProgressInRangeAsync(from, to);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetRecentAndGetAllAreNotFilteredBySource()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.ActivityLogs.AddRange(
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added, Source = ActivityChangeSource.BetterMal },
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added, Source = ActivityChangeSource.MalStartupImport });
        await db.SaveChangesAsync();

        var repository = new ActivityLogRepository(db);
        Assert.Equal(2, (await repository.GetRecentAsync(20)).Count);
        Assert.Equal(2, (await repository.GetAllAsync()).Count);
    }
}
