using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Entries;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
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
    private static AnimeTrackerDbContext CreateDb(string? name = null, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString());
        if (interceptors.Length > 0)
            builder.AddInterceptors(interceptors);
        return new AnimeTrackerDbContext(builder.Options);
    }

    private static async Task<List<UserAnimeEntry>> ReadUntrackedAsync(AnimeTrackerDbContext db) =>
        await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();

    private static UserAnimeEntry AddEntry(
        AnimeTrackerDbContext db, int animeId, WatchStatus status, string airingStatus, int? totalEpisodes, int episodesWatched)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Show {animeId}", AiringStatus = airingStatus, TotalEpisodes = totalEpisodes };
        db.AnimeMetadata.Add(anime);
        var entry = new UserAnimeEntry { AnimeId = animeId, Anime = anime, Status = status, EpisodesWatched = episodesWatched };
        db.UserAnimeEntries.Add(entry);
        return entry;
    }

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

    // --- Held entries (hold-startup-pending-sync-for-review design.md D15) ---

    [Fact]
    public async Task AnAutomaticReopenOnAHeldEntryLeavesTheHoldInPlace()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var finishDate = new DateOnly(2024, 1, 1);
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "currently_airing", TotalEpisodes = 24 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12, CompletedAt = finishDate,
            PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int> { [1] = 13 });

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, stored.Status); // the automatic transition still applies
        Assert.Equal(heldAt, stored.HeldForReviewAt); // but the hold survives it
    }

    [Fact]
    public async Task AnAutomaticCompleteOnAHeldEntryLeavesTheHoldInPlace()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Show", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12,
            PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        var service = new AiringWatchStatusService(db, new RecordingEntrySyncScheduler(), NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int>());

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status); // the automatic transition still applies
        Assert.Equal(heldAt, stored.HeldForReviewAt); // but the hold survives it
    }

    // --- Batched settle (design.md D5-D8): several entries move on one read
    // and one save, however many qualify, and a save that conflicts on one
    // entry drops only that entry. ---

    [Fact]
    public async Task SeveralCompletionsSettleInOneSave()
    {
        using var db = CreateDb();
        AddEntry(db, 1, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(db, 2, WatchStatus.Watching, "finished_airing", 24, 24);
        AddEntry(db, 3, WatchStatus.Watching, "finished_airing", 6, 6);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.All(entries, e => Assert.Equal(WatchStatus.Completed, e.Status));
        foreach (var animeId in new[] { 1, 2, 3 })
        {
            var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == animeId);
            Assert.Equal(WatchStatus.Completed, stored.Status);
            Assert.NotNull(stored.CompletedAt);
            Assert.True(stored.PendingSync);
            Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());
        }

        Assert.Equal(1, saveLog.Events.Count(e => e == "SavingChanges"));
        AssertTrackedBeforeFirstSave(saveLog, [1, 2, 3]);
        Assert.Equal(new[] { 1, 2, 3 }, syncScheduler.ScheduledAnimeIds.OrderBy(x => x));
    }

    [Fact]
    public async Task SeveralReopensSettleInOneSave()
    {
        using var db = CreateDb();
        AddEntry(db, 1, WatchStatus.Completed, "currently_airing", 24, 12);
        AddEntry(db, 2, WatchStatus.Completed, "currently_airing", 24, 10);
        AddEntry(db, 3, WatchStatus.Completed, "currently_airing", 24, 5);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        var entries = await ReadUntrackedAsync(db);
        var airedSoFar = new Dictionary<int, int> { [1] = 13, [2] = 11, [3] = 6 };
        await service.SettleAsync(entries, airedSoFar);

        Assert.All(entries, e => Assert.Equal(WatchStatus.Watching, e.Status));
        foreach (var animeId in new[] { 1, 2, 3 })
        {
            var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == animeId);
            Assert.Equal(WatchStatus.Watching, stored.Status);
            Assert.True(stored.PendingSync);
            Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());
        }

        Assert.Equal(1, saveLog.Events.Count(e => e == "SavingChanges"));
        AssertTrackedBeforeFirstSave(saveLog, [1, 2, 3]);
        Assert.Equal(new[] { 1, 2, 3 }, syncScheduler.ScheduledAnimeIds.OrderBy(x => x));
    }

    [Fact]
    public async Task AMixOfReopensAndCompletionsSettlesInOneSave()
    {
        using var db = CreateDb();
        AddEntry(db, 1, WatchStatus.Completed, "currently_airing", 24, 12); // reopen
        AddEntry(db, 2, WatchStatus.Watching, "finished_airing", 12, 12); // complete
        AddEntry(db, 3, WatchStatus.Completed, "currently_airing", 24, 10); // reopen
        AddEntry(db, 4, WatchStatus.Watching, "finished_airing", 6, 6); // complete
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        var byId = (await ReadUntrackedAsync(db)).ToDictionary(e => e.AnimeId);
        // Caller order interleaves the two directions.
        var ordered = new[] { 1, 2, 3, 4 }.Select(id => byId[id]).ToList();
        var airedSoFar = new Dictionary<int, int> { [1] = 13, [3] = 11 };
        await service.SettleAsync(ordered, airedSoFar);

        Assert.Equal(WatchStatus.Watching, byId[1].Status);
        Assert.Equal(WatchStatus.Completed, byId[2].Status);
        Assert.Equal(WatchStatus.Watching, byId[3].Status);
        Assert.Equal(WatchStatus.Completed, byId[4].Status);

        foreach (var animeId in new[] { 1, 2, 3, 4 })
            Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());

        Assert.Equal(1, saveLog.Events.Count(e => e == "SavingChanges"));
        AssertTrackedBeforeFirstSave(saveLog, [1, 2, 3, 4]);
        Assert.Equal(new[] { 1, 2, 3, 4 }, syncScheduler.ScheduledAnimeIds.OrderBy(x => x));
    }

    [Fact]
    public async Task NothingQualifiesTouchesNoDatabase()
    {
        using var db = CreateDb();

        var rewatchingAnime = new AnimeMetadata { Id = 1, Title = "Show 1", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        var rewatchingEntry = new UserAnimeEntry { AnimeId = 1, Anime = rewatchingAnime, Status = WatchStatus.Rewatching, EpisodesWatched = 12 };

        var finishedCompletedAnime = new AnimeMetadata { Id = 2, Title = "Show 2", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        var finishedCompletedEntry = new UserAnimeEntry { AnimeId = 2, Anime = finishedCompletedAnime, Status = WatchStatus.Completed, EpisodesWatched = 0 };

        var belowTotalAnime = new AnimeMetadata { Id = 3, Title = "Show 3", AiringStatus = "finished_airing", TotalEpisodes = 12 };
        var belowTotalEntry = new UserAnimeEntry { AnimeId = 3, Anime = belowTotalAnime, Status = WatchStatus.Watching, EpisodesWatched = 6 };

        var unknownTotalAnime = new AnimeMetadata { Id = 4, Title = "Show 4", AiringStatus = "currently_airing", TotalEpisodes = null };
        var unknownTotalEntry = new UserAnimeEntry { AnimeId = 4, Anime = unknownTotalAnime, Status = WatchStatus.Watching, EpisodesWatched = 12 };

        // Any database access from here on throws ObjectDisposedException, so
        // the assertion that nothing throws also proves pass 1 never touched it.
        db.Dispose();

        var syncScheduler = new RecordingEntrySyncScheduler();
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(
            [rewatchingEntry, finishedCompletedEntry, belowTotalEntry, unknownTotalEntry],
            new Dictionary<int, int>());

        Assert.Empty(syncScheduler.ScheduledAnimeIds);
    }

    [Fact]
    public async Task AnEntryMovedBeforeTheBatchedReadIsLeftAlone()
    {
        using var db = CreateDb();
        AddEntry(db, 1, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(db, 2, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(db, 3, WatchStatus.Watching, "finished_airing", 12, 12);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var entries = await ReadUntrackedAsync(db);

        // A change (an edit, a sync push) lands on anime 2 after the caller's
        // own untracked read but before settling's batched read.
        var moved = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 2);
        moved.Status = WatchStatus.Dropped;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(WatchStatus.Completed, entries.Single(e => e.AnimeId == 1).Status);
        Assert.Equal(WatchStatus.Dropped, entries.Single(e => e.AnimeId == 2).Status);
        Assert.Equal(WatchStatus.Completed, entries.Single(e => e.AnimeId == 3).Status);

        var stored2 = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 2);
        Assert.Equal(WatchStatus.Dropped, stored2.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 2).ToListAsync());
        Assert.DoesNotContain(2, syncScheduler.ScheduledAnimeIds);

        foreach (var animeId in new[] { 1, 3 })
        {
            var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == animeId);
            Assert.Equal(WatchStatus.Completed, stored.Status);
            Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());
        }

        Assert.Equal(1, saveLog.Events.Count(e => e == "SavingChanges"));
        Assert.Equal(new[] { 1, 3 }, syncScheduler.ScheduledAnimeIds.OrderBy(x => x));
    }

    [Fact]
    public async Task AConflictAtTheSaveDropsOnlyThatEntry()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedDb = CreateDb(dbName);
        AddEntry(seedDb, 1, WatchStatus.Watching, "finished_airing", 12, 12); // A
        AddEntry(seedDb, 2, WatchStatus.Watching, "finished_airing", 12, 12); // B
        AddEntry(seedDb, 3, WatchStatus.Watching, "finished_airing", 12, 12); // C
        await seedDb.SaveChangesAsync();
        var entries = await ReadUntrackedAsync(seedDb);

        // B's competing change stands in for a push landing between settle's
        // read and its save: it leaves B Watching with PendingSync cleared.
        var interceptor = new ScriptedConflictInterceptor(dbName, [new ConflictAttempt(2, WatchStatus.Watching, false)]);
        using var db = CreateDb(dbName, interceptor);
        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(WatchStatus.Completed, entries.Single(e => e.AnimeId == 1).Status);
        Assert.Equal(WatchStatus.Watching, entries.Single(e => e.AnimeId == 2).Status);
        Assert.Equal(WatchStatus.Completed, entries.Single(e => e.AnimeId == 3).Status);

        var storedB = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 2);
        Assert.Equal(WatchStatus.Watching, storedB.Status);
        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 2).ToListAsync());
        Assert.DoesNotContain(2, syncScheduler.ScheduledAnimeIds);

        foreach (var animeId in new[] { 1, 3 })
        {
            var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == animeId);
            Assert.Equal(WatchStatus.Completed, stored.Status);
            Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());
        }

        Assert.Equal(2, saveLog.Events.Count(e => e == "SavingChanges"));
        Assert.Equal(new[] { 1, 3 }, syncScheduler.ScheduledAnimeIds.OrderBy(x => x));
    }

    [Fact]
    public async Task TwoConflictsAcrossAttemptsLeaveOnlyTheThirdEntryMoved()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedDb = CreateDb(dbName);
        AddEntry(seedDb, 1, WatchStatus.Watching, "finished_airing", 12, 12); // A
        AddEntry(seedDb, 2, WatchStatus.Watching, "finished_airing", 12, 12); // B
        AddEntry(seedDb, 3, WatchStatus.Watching, "finished_airing", 12, 12); // C
        await seedDb.SaveChangesAsync();
        var entries = await ReadUntrackedAsync(seedDb);

        var interceptor = new ScriptedConflictInterceptor(dbName,
        [
            new ConflictAttempt(1, WatchStatus.Watching, false),
            new ConflictAttempt(2, WatchStatus.Watching, false),
        ]);
        using var db = CreateDb(dbName, interceptor);
        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(WatchStatus.Watching, entries.Single(e => e.AnimeId == 1).Status);
        Assert.Equal(WatchStatus.Watching, entries.Single(e => e.AnimeId == 2).Status);
        Assert.Equal(WatchStatus.Completed, entries.Single(e => e.AnimeId == 3).Status);

        foreach (var animeId in new[] { 1, 2 })
        {
            var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == animeId);
            Assert.Equal(WatchStatus.Watching, stored.Status);
            Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == animeId).ToListAsync());
            Assert.DoesNotContain(animeId, syncScheduler.ScheduledAnimeIds);
        }

        var storedC = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 3);
        Assert.Equal(WatchStatus.Completed, storedC.Status);
        Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 3).ToListAsync());
        Assert.Equal([3], syncScheduler.ScheduledAnimeIds);

        Assert.Equal(3, saveLog.Events.Count(e => e == "SavingChanges"));
    }

    [Fact]
    public async Task SyncsAreScheduledOnlyAfterTheFinalSaveInAPlainBatch()
    {
        using var db = CreateDb();
        AddEntry(db, 1, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(db, 2, WatchStatus.Watching, "finished_airing", 12, 12);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(await ReadUntrackedAsync(db), new Dictionary<int, int>());

        Assert.Equal(2, syncScheduler.ScheduledAnimeIds.Count);
        Assert.All(syncScheduler.CompletedSaveCountAtSchedule, c => Assert.Equal(saveLog.SavedChangesCount, c));
    }

    [Fact]
    public async Task SyncsAreScheduledOnlyAfterTheFinalSaveWithAConflict()
    {
        var dbName = Guid.NewGuid().ToString();
        using var seedDb = CreateDb(dbName);
        AddEntry(seedDb, 1, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(seedDb, 2, WatchStatus.Watching, "finished_airing", 12, 12);
        AddEntry(seedDb, 3, WatchStatus.Watching, "finished_airing", 12, 12);
        await seedDb.SaveChangesAsync();
        var entries = await ReadUntrackedAsync(seedDb);

        var interceptor = new ScriptedConflictInterceptor(dbName, [new ConflictAttempt(2, WatchStatus.Watching, false)]);
        using var db = CreateDb(dbName, interceptor);
        var saveLog = new SaveLog(db);
        var syncScheduler = new RecordingEntrySyncScheduler(saveLog);
        var service = new AiringWatchStatusService(db, syncScheduler, NullLogger<AiringWatchStatusService>.Instance);

        await service.SettleAsync(entries, new Dictionary<int, int>());

        Assert.Equal(2, syncScheduler.ScheduledAnimeIds.Count);
        Assert.All(syncScheduler.CompletedSaveCountAtSchedule, c => Assert.Equal(saveLog.SavedChangesCount, c));
    }

    /// <summary>Every id in <paramref name="animeIds"/> was tracked from a
    /// query before the first save, and none was tracked after it — rules out
    /// a read, save, read, save shape hiding behind a single reported save
    /// count.</summary>
    private static void AssertTrackedBeforeFirstSave(SaveLog saveLog, IEnumerable<int> animeIds)
    {
        var events = saveLog.Events.ToList();
        var firstSaving = events.IndexOf("SavingChanges");
        Assert.True(firstSaving >= 0);
        foreach (var animeId in animeIds)
        {
            var tracked = events.IndexOf($"Tracked:{animeId}");
            Assert.True(tracked >= 0 && tracked < firstSaving);
        }
        Assert.DoesNotContain(events.Skip(firstSaving + 1), e => e.StartsWith("Tracked:"));
    }

    /// <summary>Records SavingChanges/SavedChanges and every entity tracked
    /// from a query, in occurrence order, so a test can assert the batched
    /// settle makes exactly one read before its save(s) rather than a read
    /// per entry (design.md D8's "Counting on InMemory").</summary>
    private sealed class SaveLog
    {
        private readonly List<string> _events = [];
        public IReadOnlyList<string> Events => _events;
        public int SavedChangesCount { get; private set; }

        public SaveLog(AnimeTrackerDbContext db)
        {
            db.ChangeTracker.Tracked += (_, e) =>
            {
                if (e.FromQuery && e.Entry.Entity is UserAnimeEntry entry)
                    _events.Add($"Tracked:{entry.AnimeId}");
            };
            db.SavingChanges += (_, _) => _events.Add("SavingChanges");
            db.SavedChanges += (_, _) =>
            {
                SavedChangesCount++;
                _events.Add("SavedChanges");
            };
        }
    }

    private sealed class RecordingEntrySyncScheduler(SaveLog? saveLog = null) : IEntrySyncScheduler
    {
        public List<int> ScheduledAnimeIds { get; } = [];
        public List<int> CompletedSaveCountAtSchedule { get; } = [];

        public void ScheduleSync(int animeId)
        {
            ScheduledAnimeIds.Add(animeId);
            if (saveLog is not null)
                CompletedSaveCountAtSchedule.Add(saveLog.SavedChangesCount);
        }
    }

    private sealed record ConflictAttempt(int? AnimeId, WatchStatus CompetingStatus, bool CompetingPendingSync);

    /// <summary>Simulates a Postgres concurrency conflict on InMemory, which
    /// neither bumps xmin nor rolls back a failed save, so a real conflict
    /// there would partly apply the batch. Writing the competing change
    /// through a second, independent DbContext and then throwing before
    /// anything is written mirrors what Postgres does: the whole transaction
    /// rolls back, and the competing writer's values are what a reload
    /// sees.</summary>
    private sealed class ScriptedConflictInterceptor(string databaseName, IReadOnlyList<ConflictAttempt> attempts) : SaveChangesInterceptor
    {
        private int _attemptIndex;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var attempt = _attemptIndex < attempts.Count ? attempts[_attemptIndex] : null;
            _attemptIndex++;
            if (attempt?.AnimeId is not { } animeId)
                return result;

            using var competing = CreateDb(databaseName);
            var competingEntry = await competing.UserAnimeEntries.SingleAsync(e => e.AnimeId == animeId, cancellationToken);
            competingEntry.Status = attempt.CompetingStatus;
            competingEntry.PendingSync = attempt.CompetingPendingSync;
            await competing.SaveChangesAsync(cancellationToken);

            var entry = eventData.Context!.ChangeTracker.Entries<UserAnimeEntry>().Single(e => e.Entity.AnimeId == animeId);
            // InternalEntityEntry is EF Core's own internal entry type; this
            // is the only public way to build a real IUpdateEntry for a
            // simulated DbUpdateConcurrencyException (design.md D8).
#pragma warning disable EF1001
            throw new DbUpdateConcurrencyException("simulated", [entry.GetInfrastructure()]);
#pragma warning restore EF1001
        }
    }
}
