using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Entries;

// gate-editing-on-aired-episodes "A completed entry re-opens when a new
// episode airs" (tasks.md 5.5): the core re-open rule, in isolation from any
// particular read path.
public class CompletedEntryReopenServiceTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<List<UserAnimeEntry>> ReadUntrackedAsync(AnimeTrackerDbContext db) =>
        await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();

    [Fact]
    public async Task AnAiringAnimeWhoseAiredCountPassedTheWatchedCountIsSetBackToWatching()
    {
        using var db = CreateDb();
        var finishDate = new DateOnly(2024, 1, 1);
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = 24 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = anime,
            Status = WatchStatus.Completed,
            EpisodesWatched = 12,
            CompletedAt = finishDate,
        });
        await db.SaveChangesAsync();

        var syncScheduler = new RecordingEntrySyncScheduler();
        var service = new CompletedEntryReopenService(db, syncScheduler, NullLogger<CompletedEntryReopenService>.Instance);

        // Untracked, like the entries a read-path repository hands the caller.
        var entries = await ReadUntrackedAsync(db);
        await service.ReopenAsync(entries, new Dictionary<int, int> { [1] = 13 });

        // Mutated in place on the caller's own object, without a second read.
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
    public async Task ACaughtUpEntryIsLeftAlone()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new CompletedEntryReopenService(db, new RecordingEntrySyncScheduler(), NullLogger<CompletedEntryReopenService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.ReopenAsync(entries, new Dictionary<int, int> { [1] = 12 }); // equal, not ahead

        Assert.Equal(WatchStatus.Completed, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task AFinishedAiringEntryWhoseAiredCountExceedsItsCachedTotalStaysCompleted()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new CompletedEntryReopenService(db, new RecordingEntrySyncScheduler(), NullLogger<CompletedEntryReopenService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        // episode-airing-data: the aired count isn't clamped by the cached
        // total, so this legitimately reads higher — the anime having
        // finished airing is what keeps this entry Completed regardless.
        await service.ReopenAsync(entries, new Dictionary<int, int> { [1] = 13 });

        Assert.Equal(WatchStatus.Completed, entries[0].Status);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task ApplyingItTwiceWritesOnlyOneActivityRow()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12 });
        await db.SaveChangesAsync();

        var service = new CompletedEntryReopenService(db, new RecordingEntrySyncScheduler(), NullLogger<CompletedEntryReopenService>.Instance);
        var airedSoFar = new Dictionary<int, int> { [1] = 13 };

        // Two independent reads, as two separate page loads would produce.
        await service.ReopenAsync(await ReadUntrackedAsync(db), airedSoFar);
        await service.ReopenAsync(await ReadUntrackedAsync(db), airedSoFar);

        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    private sealed class RecordingEntrySyncScheduler : IEntrySyncScheduler
    {
        public List<int> ScheduledAnimeIds { get; } = [];
        public void ScheduleSync(int animeId) => ScheduledAnimeIds.Add(animeId);
    }
}
