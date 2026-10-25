using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Dashboard;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Dashboard;

// main-dashboard "Airing today filtered to my list in local time": the section's
// rows are today's stored airing rows grouped by the Airing page's own rule
// (AiringDaySlotGrouper), so a run of episodes that air together reads
// `Ep 1-10`, an interrupted run reads as two rows, and the section matches the
// Airing page's slots for the same day. Run over the real schedule service and
// an in-memory database, so the grouping is fed by the real bulk read.
public class MainDashboardServiceAiringTodayTests
{
    private static readonly IBroadcastLocalTimeConverter LocalTime = new UtcLocalTimeConverter();

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static DateTimeOffset TodayAt(int hour, int minute = 0) =>
        LocalTime.LocalMidnightUtc(LocalTime.GetLocalDate(DateTimeOffset.UtcNow)).AddHours(hour).AddMinutes(minute);

    private static UserAnimeEntry Entry(int animeId) => new()
    {
        AnimeId = animeId,
        Status = WatchStatus.PlanToWatch,
        Anime = new AnimeMetadata
        {
            Id = animeId,
            Title = $"Anime {animeId}",
            EnglishTitle = $"English {animeId}",
            PictureUrl = $"https://img/{animeId}.jpg",
        },
    };

    private static EpisodeAiring Row(int animeId, int episode, DateTimeOffset airsAtUtc) =>
        new() { AnimeId = animeId, Episode = episode, AirsAtUtc = airsAtUtc, FetchedAt = DateTimeOffset.UtcNow };

    private static async Task<MainDashboardDto> DashboardFor(
        AnimeTrackerDbContext db, List<UserAnimeEntry> entries, params EpisodeAiring[] rows)
    {
        db.EpisodeAirings.AddRange(rows);
        await db.SaveChangesAsync();
        var scheduleService = new EpisodeScheduleService(new EpisodeAiringRepository(db), LocalTime);
        var service = new MainDashboardService(
            new FakeUserAnimeEntryRepository(entries), scheduleService, new NullAiringWatchStatusService(), LocalTime);
        return await service.GetDashboardAsync();
    }

    [Fact]
    public async Task TenEpisodesAiringTogetherAreOneRowReadingTheRange()
    {
        using var db = CreateDb();
        var at = TodayAt(19, 30);

        var dashboard = await DashboardFor(db, [Entry(1)],
            Enumerable.Range(1, 10).Select(episode => Row(1, episode, at)).ToArray());

        var item = Assert.Single(dashboard.AiringToday);
        Assert.Equal(1, item.AnimeId);
        Assert.Equal("19:30", item.LocalTime);
        Assert.Equal(1, item.EpisodeNumber);
        Assert.Equal(10, item.EpisodeNumberEnd);
    }

    [Fact]
    public async Task SameDayEpisodesAtDifferentTimesMergeIntoOneRowTimedAtTheEarliest()
    {
        using var db = CreateDb();

        var dashboard = await DashboardFor(db, [Entry(1), Entry(2)],
            Row(1, 5, TodayAt(19)), Row(1, 6, TodayAt(19, 30)));

        var item = Assert.Single(dashboard.AiringToday);
        Assert.Equal("19:00", item.LocalTime);
        Assert.Equal(5, item.EpisodeNumber);
        Assert.Equal(6, item.EpisodeNumberEnd);
    }

    [Fact]
    public async Task AnInterruptedRunIsTwoRowsWithTheOtherAnimeBetween()
    {
        using var db = CreateDb();

        var dashboard = await DashboardFor(db, [Entry(1), Entry(2)],
            Row(1, 5, TodayAt(19)), Row(2, 3, TodayAt(20)), Row(1, 6, TodayAt(21)));

        Assert.Equal(
            [(1, "19:00", 5, (int?)null), (2, "20:00", 3, null), (1, "21:00", 6, null)],
            dashboard.AiringToday.Select(i => (i.AnimeId, i.LocalTime, i.EpisodeNumber, i.EpisodeNumberEnd)));
    }

    [Fact]
    public async Task ASingleEpisodeHasNoRangeEnd()
    {
        using var db = CreateDb();

        var dashboard = await DashboardFor(db, [Entry(1)], Row(1, 4, TodayAt(19, 30)));

        var item = Assert.Single(dashboard.AiringToday);
        Assert.Equal(4, item.EpisodeNumber);
        Assert.Null(item.EpisodeNumberEnd);
    }

    [Fact]
    public async Task AnimeWithNoRowTodayOrOutsideMyListAreAbsent()
    {
        using var db = CreateDb();

        var dashboard = await DashboardFor(db, [Entry(1), Entry(2)],
            Row(1, 4, TodayAt(19).AddDays(1)), // tomorrow
            Row(3, 8, TodayAt(20)));           // not in my list

        Assert.Empty(dashboard.AiringToday);
    }

    [Fact]
    public async Task TheRowsMatchTheAiringPagesSlotsForTheSameDay()
    {
        using var db = CreateDb();
        List<UserAnimeEntry> entries = [Entry(1), Entry(2), Entry(3), Entry(4)];
        // A batch of three at one instant, a run broken by another anime, an
        // episode of an anime that airs once, and a late single.
        var rows = new[]
        {
            Row(1, 1, TodayAt(9)), Row(1, 2, TodayAt(9)), Row(1, 3, TodayAt(9)),
            Row(2, 7, TodayAt(10)), Row(3, 2, TodayAt(11)), Row(2, 8, TodayAt(12)),
            Row(4, 12, TodayAt(23, 15)),
        };

        var dashboard = await DashboardFor(db, entries, rows);
        var week = await new AiringScheduleService(
            new FakeUserAnimeEntryRepository(entries), new EpisodeAiringRepository(db), LocalTime)
            .GetWeekAsync(LocalTime.GetLocalDate(DateTimeOffset.UtcNow));

        var todaysSlots = week.Days[0].Slots;
        Assert.Equal(5, todaysSlots.Count);
        Assert.Equal(
            todaysSlots.Select(s => (s.AnimeId, s.Title, s.EnglishTitle, s.PictureUrl, s.LocalTime, s.EpisodeNumber, s.EpisodeNumberEnd)),
            dashboard.AiringToday.Select(i => (i.AnimeId, i.Title, i.EnglishTitle, i.PictureUrl, i.LocalTime, i.EpisodeNumber, i.EpisodeNumberEnd)));
    }

    private sealed class NullAiringWatchStatusService : IAiringWatchStatusService
    {
        public Task SettleAsync(IReadOnlyCollection<UserAnimeEntry> entries, IReadOnlyDictionary<int, int> airedSoFarByAnimeId, CancellationToken ct = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    // The local zone is UTC, and a week starts on the date it is asked for, so
    // GetWeekAsync's first day is today.
    private sealed class UtcLocalTimeConverter : IBroadcastLocalTimeConverter
    {
        public DateOnly GetStartOfWeek(DateOnly referenceDate) => referenceDate;
        public DateOnly GetLocalDate(DateTimeOffset instantUtc) => DateOnly.FromDateTime(instantUtc.UtcDateTime);
        public TimeOnly GetLocalTime(DateTimeOffset instantUtc) => TimeOnly.FromDateTime(instantUtc.UtcDateTime);
        public DateTimeOffset LocalMidnightUtc(DateOnly localDate) => new(localDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        public (DayOfWeek LocalDayOfWeek, TimeOnly LocalTime) ConvertBroadcastSlot(DayOfWeek jstDayOfWeek, TimeOnly jstTime, DateTimeOffset referenceUtc) =>
            (jstDayOfWeek, jstTime);
    }
}
