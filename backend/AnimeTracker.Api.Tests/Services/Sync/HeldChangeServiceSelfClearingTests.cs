using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// hold-startup-pending-sync-for-review tasks.md 9.4 / design.md D8a:
// GetHeldAsync clears (and omits) any item MyAnimeList already agrees with,
// records no activity for it, and leaves everything else held.
public class HeldChangeServiceSelfClearingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AnEntryMatchingMalOnAllSixFieldsClearsAndIsOmitted()
    {
        using var db = CreateDb();
        var start = new DateOnly(2024, 1, 1);
        var finish = new DateOnly(2024, 3, 1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Completed, EpisodesWatched = 12, MyScore = 8,
            StartedAt = start, CompletedAt = finish, RewatchCount = 0,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus
        {
            Status = "completed", NumEpisodesWatched = 12, Score = 8,
            StartDate = "2024-01-01", FinishDate = "2024-03-01", NumTimesRewatched = 0,
        });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        Assert.Empty(result);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.False(stored.PendingSync);
        Assert.Null(stored.HeldForReviewAt);
        Assert.NotNull(stored.LastSyncedAt);
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task ALocalRewatchingAgainstMalsWatchingCountsAsMatchingAndClears()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Rewatching, EpisodesWatched = 5, RewatchCount = 2,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 5, NumTimesRewatched = 2 });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        Assert.Empty(result);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.False(stored.PendingSync);
        Assert.Null(stored.HeldForReviewAt);
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AnEntryWhoseEpisodeCountIsHigherThanMalsStaysHeld()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12,
            PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 4 });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        Assert.Single(result);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.Equal(heldAt, stored.HeldForReviewAt);
    }

    [Fact]
    public async Task ARemovalWhoseAnimeMalNoLongerListsIsDropped()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow.AddDays(-1), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var malClient = new FakeHeldChangeMalClient(); // no status set -> null, MAL no longer lists it

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        Assert.Empty(result);
        Assert.Empty(await db.PendingEntryDeletions.ToListAsync());
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AFailedMalReadClearsNothing()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 12,
            PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        var malClient = new FakeHeldChangeMalClient();
        malClient.FailReadFor(1);

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        var item = Assert.Single(result);
        Assert.True(item.RemoteUnavailable);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.Equal(heldAt, stored.HeldForReviewAt);
    }
}
