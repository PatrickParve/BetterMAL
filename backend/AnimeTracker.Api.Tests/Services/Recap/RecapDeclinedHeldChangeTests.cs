using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Recap;
using AnimeTracker.Api.Services.Scheduling;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Recap;

// log-only-my-own-edits (design D5, list-recaps "Progress applied by
// declining a held change counts"): the log holds no origin any more, so an
// episode row from declining a held change counts toward a recap exactly
// like any other logged progress.
public class RecapDeclinedHeldChangeTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task ADeclinedHeldChangeRaisingEpisodesInsideAPeriodAddsTheIncreaseAndPlacesTheAnime()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        // The row EntryActivityRecorder.Diff produces when declining a held
        // change raises an entry's episodes watched from 5 to 7.
        db.ActivityLogs.Add(new ActivityLog
        {
            AnimeId = 1,
            Timestamp = new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero),
            ChangeType = ActivityChangeType.EpisodeIncremented,
            ChangeDetail = "Episode 7",
            PreviousEpisodesWatched = 5,
        });
        await db.SaveChangesAsync();

        var repository = new ActivityLogRepository(db);
        var entryRepository = new FakeUserAnimeEntryRepository([
            new UserAnimeEntry
            {
                AnimeId = 1,
                Anime = anime,
                Status = WatchStatus.Watching, // no CompletedAt/AiredFrom — only logged progress can select it
            },
        ]);
        var timeConverter = new FakeBroadcastLocalTimeConverter();

        var recap = await new RecapService(entryRepository, repository, new TopAnimeSelectionRepository(db), timeConverter)
            .GetRecapAsync(RecapPeriod.Yearly(2026), RecapTimeFilter.Watched, CancellationToken.None);

        Assert.Equal(1, recap.WatchedCount);
        Assert.Equal(2, recap.Stats.EpisodesWatched);
        Assert.Contains(recap.Items, e => e.AnimeId == 1);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
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
