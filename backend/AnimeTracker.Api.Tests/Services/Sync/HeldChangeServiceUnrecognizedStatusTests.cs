using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// mal-write-sync "A MyAnimeList list status the app does not recognize is
// never guessed" (design.md D7): the held-changes review and decline treat an
// unrecognized MyAnimeList list status exactly like an unreadable MyAnimeList
// side — never guessed, never self-cleared.
public class HeldChangeServiceUnrecognizedStatusTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MalListStatus UnrecognizedStatus() => new() { Status = "rewatching_v2", NumEpisodesWatched = 3 };

    [Fact]
    public async Task GetHeldReturnsAHeldEntryAsRemoteUnavailableAndDoesNotClearIt()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        db.AnimeMetadata.AddRange(anime1, anime2);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, EpisodesWatched = 3, PendingSync = true, HeldForReviewAt = heldAt },
            new UserAnimeEntry { AnimeId = 2, Anime = anime2, Status = WatchStatus.Watching, EpisodesWatched = 5, PendingSync = true, HeldForReviewAt = heldAt });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, UnrecognizedStatus());
        // Deliberately mismatched, so this item stays genuinely held rather
        // than self-clearing — proving the unrecognized-status item above
        // didn't affect it.
        malClient.SetStatus(2, new MalListStatus { Status = "watching", NumEpisodesWatched = 1 });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        var item1 = Assert.Single(result, r => r.AnimeId == 1);
        Assert.True(item1.RemoteUnavailable);
        Assert.Null(item1.RemoteValues);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.Equal(heldAt, stored.HeldForReviewAt);
        Assert.Single(result, r => r.AnimeId == 2); // the other held item is unaffected
    }

    [Fact]
    public async Task GetHeldReturnsAHeldRemovalAsRemoteUnavailableAndDoesNotClearIt()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 1, Anime = anime, RequestedAt = heldAt, HeldForReviewAt = heldAt });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, UnrecognizedStatus());

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).GetHeldAsync();

        var item = Assert.Single(result);
        Assert.True(item.RemoteUnavailable);
        Assert.Equal(HeldChangeKind.Removal, item.Kind);
        Assert.NotEmpty(await db.PendingEntryDeletions.ToListAsync());
    }

    [Fact]
    public async Task DeclineOnAnUnrecognizedStatusEntryFailsAndLeavesItHeld()
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
        malClient.SetStatus(1, UnrecognizedStatus());

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Failed, result.Outcome);
        Assert.Equal("Could not read MyAnimeList's current value.", result.Error);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.Equal(heldAt, stored.HeldForReviewAt);
    }

    [Fact]
    public async Task DeclineOnAnUnrecognizedStatusRemovalFailsAndLeavesItHeld()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 1, Anime = anime, RequestedAt = heldAt, HeldForReviewAt = heldAt });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, UnrecognizedStatus());

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Failed, result.Outcome);
        Assert.NotEmpty(await db.PendingEntryDeletions.ToListAsync());
    }

    [Fact]
    public async Task DeclineAllCountsAnUnrecognizedStatusItemAsStillHeldAndDeclinesTheRest()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-1);
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        db.AnimeMetadata.AddRange(anime1, anime2);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = heldAt },
            new UserAnimeEntry { AnimeId = 2, Anime = anime2, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = heldAt });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, UnrecognizedStatus());
        malClient.SetStatus(2, new MalListStatus { Status = "watching" });

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAllAsync();

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.StillHeld);
        var stillHeld = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stillHeld.PendingSync);
    }

    [Fact]
    public async Task AcceptOnAnUnrecognizedStatusEntryStillApplies()
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
        malClient.SetStatus(1, UnrecognizedStatus()); // AcceptAsync never reads MyAnimeList's status

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).AcceptAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
    }
}
