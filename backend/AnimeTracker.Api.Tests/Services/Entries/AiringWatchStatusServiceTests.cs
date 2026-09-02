using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// design.md D5/D6 (refine-sync-status-and-episode-totals tasks.md 5.1-5.4/9.6):
// AiringWatchStatusService.SettleAsync, both directions — re-open (a
// Completed entry on a still-airing anime that hasn't watched the published
// total returns to Watching) and complete (a Watching entry that already
// covers the full run completes, the case where the total arrives after the
// viewing).
public class AiringWatchStatusServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<List<UserAnimeEntry>> ReadUntrackedAsync(AnimeTrackerDbContext db) =>
        await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();

    // --- Complete direction ---

    [Fact]
    public async Task CompletesAWatchingEntryOnceItsTotalBecomesKnownAndFillsAMissingFinishDate()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var syncScheduler = new RecordingEntrySyncScheduler();
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(WatchStatus.Completed, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.NotNull(stored.CompletedAt);
        Assert.True(stored.PendingSync);
        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal([1], syncScheduler.ScheduledAnimeIds);
    }

    [Fact]
    public async Task PreservesAnExistingFinishDateWhenCompletingAgain()
    {
        using var db = CreateDb();
        var oldFinish = new DateOnly(2023, 1, 1);
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12, CompletedAt = oldFinish,
        });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Equal(oldFinish, stored.CompletedAt); // never overwritten
    }

    [Fact]
    public async Task ARewatchingEntryIsLeftAloneEvenAtTheTotal()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Rewatching, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task AWatchingEntryWithAnUnknownTotalIsLeftAlone()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = null };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task CompletingIsIdempotent()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        // Two independent reads, as two separate page loads would produce.
        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());
        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());

        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    // --- Re-open direction ---

    [Fact]
    public async Task ReopensAPartialCountCompletedEntryOnAnAiringAnime()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2024, 1, 1);
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = 24 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12, CompletedAt = finishDate,
        });
        await db.SaveChangesAsync();

        var syncScheduler = new RecordingEntrySyncScheduler();
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int> { [1] = 13 });

        Assert.Equal(WatchStatus.Watching, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status);
        Assert.Equal(12, stored.EpisodesWatched); // untouched
        Assert.Equal(finishDate, stored.CompletedAt); // untouched — never cleared automatically
        Assert.True(stored.PendingSync);
        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal([1], syncScheduler.ScheduledAnimeIds);
    }

    [Fact]
    public async Task DoesNotReopenAnEntryThatHasWatchedTheTotalUnderAStaleCurrentlyAiringStatus()
    {
        // design.md D5: reads the total, not the aired count — a stale
        // currently_airing status must not repeatedly un-complete a run the
        // user has genuinely finished.
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int> { [1] = 12 });

        Assert.Equal(WatchStatus.Completed, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task DoesNotTouchACompletedAtZeroEntryOnAFinishedAnime()
    {
        // design.md D5: the currently_airing guard is load-bearing — MAL
        // lists commonly hold entries imported as completed at a partial (or
        // zero) count on an already-finished show, and this must not churn them.
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 0 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(WatchStatus.Completed, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task ReopeningIsIdempotent()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = 24 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);
        var airedSoFar = new Dictionary<int, int> { [1] = 13 };

        await service.SettleAsync(await ReadUntrackedAsync(db), airedSoFar);
        await service.SettleAsync(await ReadUntrackedAsync(db), airedSoFar);

        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    private sealed class RecordingEntrySyncScheduler : IEntrySyncScheduler
    {
        public List<int> ScheduledAnimeIds { get; } = [];
        public void ScheduleSync(int animeId) => ScheduledAnimeIds.Add(animeId);
    }
}
