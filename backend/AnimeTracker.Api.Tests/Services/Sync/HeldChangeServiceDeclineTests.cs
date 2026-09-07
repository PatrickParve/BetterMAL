using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// hold-startup-pending-sync-for-review tasks.md 9.6 / design.md D6-D7: one
// test per decline branch, for both a held entry and a held removal.
public class HeldChangeServiceDeclineTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task DecliningAnEntryAppliesMalsCurrentValues()
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
        malClient.SetStatus(1, new MalListStatus { Status = "completed", NumEpisodesWatched = 12 });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Equal(12, stored.EpisodesWatched);
        Assert.False(stored.PendingSync);
        Assert.Null(stored.HeldForReviewAt);
        Assert.NotNull(stored.LastSyncedAt);
    }

    [Fact]
    public async Task DecliningALocalRewatchIsNotDemotedByMalsWatchingStatus()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Rewatching, EpisodesWatched = 3, RewatchCount = 1,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching", NumEpisodesWatched = 7, NumTimesRewatched = 1 });

        await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status); // preserved, not demoted to Watching
        Assert.Equal(7, stored.EpisodesWatched);
    }

    [Fact]
    public async Task DecliningAnEntryMalNoLongerListsDeletesItAndQueuesNoRemoval()
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
        var malClient = new FakeHeldChangeMalClient(); // no status set -> MAL has no entry

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
        Assert.Null(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
        Assert.Empty(await db.PendingEntryDeletions.ToListAsync());
    }

    [Fact]
    public async Task ADeclineReadFailureChangesNothing()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
            PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.FailReadFor(1);

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Failed, result.Outcome);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(3, stored.EpisodesWatched);
        Assert.True(stored.PendingSync);
        Assert.Equal(heldAt, stored.HeldForReviewAt);
    }

    [Fact]
    public async Task DecliningAHeldRemovalRestoresTheEntryFromMalsCurrentValue()
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

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
        Assert.Empty(await db.PendingEntryDeletions.ToListAsync());
        var restored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Watching, restored.Status);
        Assert.Equal(5, restored.EpisodesWatched);
        Assert.False(restored.PendingSync);
    }

    [Fact]
    public async Task DecliningAHeldRemovalForAnAnimeMalNoLongerListsJustDropsIt()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow.AddDays(-1), HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient(); // no status -> MAL no longer lists it

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
        Assert.Empty(await db.PendingEntryDeletions.ToListAsync());
        Assert.Null(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
    }
}
