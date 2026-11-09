using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// log-only-my-own-edits (design D5): the log holds only changes made in the
// app, so GetEpisodeProgressInRangeAsync returns every episode row in range
// with no filter of its own.
public class ActivityLogRepositoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActivityLog EpisodeRow(int animeId, DateTimeOffset timestamp) => new()
    {
        AnimeId = animeId,
        Timestamp = timestamp,
        ChangeType = ActivityChangeType.EpisodeIncremented,
        ChangeDetail = "Episode 5",
    };

    [Fact]
    public async Task GetEpisodeProgressInRangeReturnsEveryEpisodeRowInRangeAndNothingElse()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var from = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
        db.ActivityLogs.AddRange(
            EpisodeRow(1, from.AddDays(1)),
            EpisodeRow(1, from.AddDays(2)),
            EpisodeRow(1, from.AddDays(-1)), // outside the range, before it
            EpisodeRow(1, to), // outside the range, exclusive upper bound
            new ActivityLog { AnimeId = 1, Timestamp = from.AddDays(1), ChangeType = ActivityChangeType.ScoreChanged, ChangeDetail = "Score 8" });
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetEpisodeProgressInRangeAsync(from, to);

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal(ActivityChangeType.EpisodeIncremented, r.ChangeType));
    }

    [Fact]
    public async Task GetEpisodeProgressInRangeAlsoReturnsTheRowsThatStartAViewing()
    {
        // The watching days read net gain per viewing: a move to Rewatching and a removal begin a new one.
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        var at = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
        ActivityLog Row(int id, ActivityChangeType type, string detail, int minutes) =>
            new() { Id = id, AnimeId = 1, Timestamp = at.AddMinutes(minutes), ChangeType = type, ChangeDetail = detail };
        db.ActivityLogs.AddRange(
            Row(1, ActivityChangeType.EpisodeIncremented, "Episode 3", 0),
            Row(2, ActivityChangeType.StatusChanged, "Completed -> Rewatching", 1),
            Row(3, ActivityChangeType.StatusChanged, "Completed -> Watching", 2), // not a new viewing
            Row(4, ActivityChangeType.StatusChanged, "Watching -> Rewatching", 3),
            Row(5, ActivityChangeType.Removed, "Removed from list", 4),
            Row(6, ActivityChangeType.ScoreChanged, "Score 8", 5),
            Row(7, ActivityChangeType.Completed, "Completed", 6),
            Row(8, ActivityChangeType.Added, "Added as Watching", 7),
            Row(9, ActivityChangeType.StatusChanged, "Completed -> Rewatching", 3 * 24 * 60)); // outside the range
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetEpisodeProgressInRangeAsync(at.AddDays(-1), at.AddDays(2));

        Assert.Equal([1, 2, 4, 5], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetEpisodeProgressInRangeOrdersRowsSharingATimestampById()
    {
        // The watching days merge an anime's increases into runs in this order, so two rows with one
        // timestamp must always come back the same way, whatever order they were stored in.
        using var db = CreateDb();
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Anime 1" });
        var at = new DateTimeOffset(2026, 1, 5, 12, 0, 0, TimeSpan.Zero);
        db.ActivityLogs.AddRange(
            new ActivityLog { Id = 3, AnimeId = 1, Timestamp = at, ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 9" },
            new ActivityLog { Id = 1, AnimeId = 1, Timestamp = at, ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 3" },
            new ActivityLog { Id = 2, AnimeId = 1, Timestamp = at, ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 6" },
            new ActivityLog { Id = 4, AnimeId = 1, Timestamp = at.AddMinutes(-1), ChangeType = ActivityChangeType.EpisodeIncremented, ChangeDetail = "Episode 1" });
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetEpisodeProgressInRangeAsync(at.AddDays(-1), at.AddDays(1));

        Assert.Equal([4, 1, 2, 3], rows.Select(r => r.Id));
    }

    [Fact]
    public async Task GetRecentAndGetAllReturnEveryRow()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.ActivityLogs.AddRange(
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added },
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.ScoreChanged, ChangeDetail = "Score 8" });
        await db.SaveChangesAsync();

        var repository = new ActivityLogRepository(db);
        Assert.Equal(2, (await repository.GetRecentAsync(20)).Count);
        Assert.Equal(2, (await repository.GetAllAsync()).Count);
    }

    [Fact]
    public async Task RowsSavedWithoutSettingEventIdGetDistinctNonEmptyValues()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.ActivityLogs.AddRange(
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added },
            new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added });
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetAllAsync();

        Assert.All(rows, r => Assert.NotEqual(Guid.Empty, r.EventId));
        Assert.Equal(2, rows.Select(r => r.EventId).Distinct().Count());
    }

    [Fact]
    public async Task ARowSavedWithAnExplicitEventIdKeepsIt()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var eventId = Guid.NewGuid();
        db.ActivityLogs.Add(new ActivityLog { AnimeId = 1, Timestamp = DateTimeOffset.UtcNow, ChangeType = ActivityChangeType.Added, EventId = eventId });
        await db.SaveChangesAsync();

        var row = (await new ActivityLogRepository(db).GetAllAsync()).Single();

        Assert.Equal(eventId, row.EventId);
    }

    // --- 5.2: GetAllOldestFirstAsync (device-transfer's activity-log read) ---

    [Fact]
    public async Task GetAllOldestFirstAsyncOrdersByTimestampAscending()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var t1 = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var t2 = t1.AddDays(1);
        var t3 = t1.AddDays(2);
        db.ActivityLogs.AddRange(
            new ActivityLog { AnimeId = 1, Timestamp = t3, ChangeType = ActivityChangeType.Added },
            new ActivityLog { AnimeId = 1, Timestamp = t1, ChangeType = ActivityChangeType.Added },
            new ActivityLog { AnimeId = 1, Timestamp = t2, ChangeType = ActivityChangeType.Added });
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetAllOldestFirstAsync();

        Assert.Equal([t1, t2, t3], rows.Select(r => r.Timestamp));
    }

    [Fact]
    public async Task GetAllOldestFirstAsyncBreaksASharedTimestampByStorageOrder()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        var timestamp = DateTimeOffset.UtcNow;
        var first = new ActivityLog { AnimeId = 1, Timestamp = timestamp, ChangeType = ActivityChangeType.EpisodeIncremented };
        var second = new ActivityLog { AnimeId = 1, Timestamp = timestamp, ChangeType = ActivityChangeType.ScoreChanged };
        var third = new ActivityLog { AnimeId = 1, Timestamp = timestamp, ChangeType = ActivityChangeType.Completed };
        db.ActivityLogs.AddRange(first, second, third);
        await db.SaveChangesAsync();

        var rows = await new ActivityLogRepository(db).GetAllOldestFirstAsync();

        Assert.Equal([first.Id, second.Id, third.Id], rows.Select(r => r.Id));
    }
}
