using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// log-only-my-own-edits (design D2/D8): declining a held entry records one
// row per genuinely moved field (nothing when nothing moves), a MAL-absent
// decline records a removal, declining a held removal MyAnimeList still
// lists records the restored addition, and accepting records nothing at all
// — accepting never changes a stored field, only sync bookkeeping.
public class HeldChangeServiceActivityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task DecliningRecordsOneRowPerGenuinelyMovedField()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3, MyScore = null,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 7, Score = 8 });

        await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        var rows = await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync();
        Assert.Equal(2, rows.Count); // episodes + score; status unchanged, no row
        Assert.Contains(rows, r => r.ChangeType == ActivityChangeType.EpisodeIncremented && r.ChangeDetail == "Episode 7");
        Assert.Contains(rows, r => r.ChangeType == ActivityChangeType.ScoreChanged && r.ChangeDetail == "Score 8");
    }

    [Fact]
    public async Task DecliningRecordsNothingWhenNothingActuallyMoves()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 7, MyScore = 8,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 7, Score = 8 });

        await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task DecliningAMalAbsentEntryRecordsARemoval()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient(); // MAL has no entry for this anime

        await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal(ActivityChangeType.Removed, row.ChangeType);
    }

    [Fact]
    public async Task DecliningAHeldRemovalMyAnimeListStillListsRecordsOneAddition()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow.AddDays(-1), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 5 });

        await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal(ActivityChangeType.Added, row.ChangeType);
        Assert.Equal("Added as Watching", row.ChangeDetail);
    }

    [Fact]
    public async Task AcceptingRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();

        await HeldChangeServiceTestFactory.Create(db, malClient).AcceptAsync(1);

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }
}
