using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Recap;

// record-mal-origin-activity design D6 (list-recaps "A recap counts only
// progress recorded in the app", tasks.md 8.7): end-to-end through the real
// ActivityLogRepository — a period whose only episode rows are MAL-origin
// reports no logged progress and is judged unavailable on that arm, while
// BetterMal rows in the same period count exactly as they do today.
public class RecapMalOriginTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UserAnimeEntry WatchingEntry(int animeId) => new()
    {
        AnimeId = animeId,
        Anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" },
        Status = WatchStatus.Watching, // no CompletedAt/AiredFrom — only logged progress can select it
    };

    private static ActivityLog EpisodeRow(int animeId, DateTimeOffset timestamp, ActivityChangeSource source, int newEpisodesWatched) => new()
    {
        AnimeId = animeId,
        Timestamp = timestamp,
        ChangeType = ActivityChangeType.EpisodeIncremented,
        ChangeDetail = $"Episode {newEpisodesWatched}",
        PreviousEpisodesWatched = newEpisodesWatched - 1,
        Source = source,
    };

    [Fact]
    public async Task APeriodWhoseOnlyEpisodeRowsAreMalOriginReportsNoLoggedProgress()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.ActivityLogs.Add(EpisodeRow(1, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), ActivityChangeSource.MalResync, 5));
        await db.SaveChangesAsync();

        var repository = new ActivityLogRepository(db);
        var entryRepository = new FakeUserAnimeEntryRepository([WatchingEntry(1)]);
        var timeConverter = new FakeBroadcastLocalTimeConverter();

        var recap = await new RecapService(entryRepository, repository, new TopAnimeSelectionRepository(db), timeConverter)
            .GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Watched, CancellationToken.None);
        Assert.Equal(0, recap.WatchedCount);
        Assert.Equal(0, recap.Stats.EpisodesWatched);

        var availability = await new RecapAvailabilityService(entryRepository, repository, timeConverter).GetAvailabilityAsync();
        Assert.DoesNotContain(availability.Years, y => y.Year == 2026);
    }

    [Fact]
    public async Task BetterMalRowsInTheSamePeriodCountAsTheyDoToday()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.ActivityLogs.Add(EpisodeRow(1, new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), ActivityChangeSource.BetterMal, 5));
        await db.SaveChangesAsync();

        var repository = new ActivityLogRepository(db);
        var entryRepository = new FakeUserAnimeEntryRepository([WatchingEntry(1)]);
        var timeConverter = new FakeBroadcastLocalTimeConverter();

        var recap = await new RecapService(entryRepository, repository, new TopAnimeSelectionRepository(db), timeConverter)
            .GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Watched, CancellationToken.None);
        Assert.Equal(1, recap.WatchedCount);
        // RecapWatchLog sums each row's positive increase (new - previous),
        // not the raw episode number — here 5 - 4 = 1.
        Assert.Equal(1, recap.Stats.EpisodesWatched);

        var availability = await new RecapAvailabilityService(entryRepository, repository, timeConverter).GetAvailabilityAsync();
        var year2026 = Assert.Single(availability.Years, y => y.Year == 2026);
        Assert.Equal(1, year2026.WatchedCount);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeBroadcastLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }
}
