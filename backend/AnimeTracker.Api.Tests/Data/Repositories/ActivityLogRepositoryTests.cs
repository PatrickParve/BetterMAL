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
}
