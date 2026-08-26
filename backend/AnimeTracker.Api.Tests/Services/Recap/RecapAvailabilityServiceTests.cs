using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;

namespace AnimeTracker.Api.Tests.Services.Recap;

// RecapAvailabilityService picks up the "watched" logged-progress arm
// (tasks.md 5.3, list-recaps "Dynamic time filter" — "Logged progress alone
// makes the option available").
public class RecapAvailabilityServiceTests
{
    private static ActivityLog LogRow(int animeId, DateTimeOffset timestamp, int previousEpisodesWatched, int newEpisodesWatched) =>
        new()
        {
            AnimeId = animeId,
            Timestamp = timestamp,
            ChangeType = ActivityChangeType.EpisodeIncremented,
            ChangeDetail = $"Episode {newEpisodesWatched}",
            PreviousEpisodesWatched = previousEpisodesWatched,
        };

    [Fact]
    public async Task AYearWithOnlyLoggedProgressIsOfferedAsWatchable()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Watching, // no CompletedAt, no AiredFrom
        };
        List<ActivityLog> logRows = [LogRow(1, new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero), 0, 5)];
        var service = new RecapAvailabilityService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var availability = await service.GetAvailabilityAsync();

        var year2024 = Assert.Single(availability.Years, y => y.Year == 2024);
        Assert.Equal(1, year2024.WatchedCount);
        Assert.Equal(0, year2024.AiredCount);
    }

    [Fact]
    public async Task ACompletionDateAloneStillMakesAYearWatchable()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Completed,
            CompletedAt = new DateOnly(2022, 6, 1),
        };
        var service = new RecapAvailabilityService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository([]), new FakeBroadcastLocalTimeConverter());

        var availability = await service.GetAvailabilityAsync();

        var year2022 = Assert.Single(availability.Years, y => y.Year == 2022);
        Assert.Equal(1, year2022.WatchedCount);
    }

    [Fact]
    public async Task ALoggedDecreaseDoesNotMakeAYearWatchable()
    {
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = new AnimeMetadata { Id = 1, Title = "Anime 1" },
            Status = WatchStatus.Watching,
        };
        List<ActivityLog> logRows = [LogRow(1, new DateTimeOffset(2024, 5, 1, 12, 0, 0, TimeSpan.Zero), 10, 3)];
        var service = new RecapAvailabilityService(
            new FakeUserAnimeEntryRepository([entry]), new FakeActivityLogRepository(logRows), new FakeBroadcastLocalTimeConverter());

        var availability = await service.GetAvailabilityAsync();

        Assert.DoesNotContain(availability.Years, y => y.Year == 2024);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository(List<ActivityLog> rows) : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();

        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            Task.FromResult(rows
                .Where(r => r.ChangeType == ActivityChangeType.EpisodeIncremented && r.Timestamp >= fromUtc && r.Timestamp < toUtc)
                .OrderBy(r => r.Timestamp)
                .ToList());
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
