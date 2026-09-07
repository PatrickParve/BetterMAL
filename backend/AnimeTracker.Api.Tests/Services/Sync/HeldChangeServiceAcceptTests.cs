using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// hold-startup-pending-sync-for-review tasks.md 9.5 / design.md D5: accepting
// releases the hold and pushes immediately; a failed push is not a decision
// failure — it just leaves the (now unheld) item pending for the retry job.
public class HeldChangeServiceAcceptTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task AcceptingAnEntryClearsTheHoldAndPushesIt()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 5,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).AcceptAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome);
        Assert.Equal([1], malClient.UpdatedAnimeIds);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.False(stored.PendingSync);
        Assert.Null(stored.HeldForReviewAt);
    }

    [Fact]
    public async Task AFailedAcceptPushLeavesTheItemPendingAndUnheld()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 5,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();
        var malClient = new FakeHeldChangeMalClient();
        malClient.FailPushFor(1);

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).AcceptAsync(1);

        Assert.Equal(HeldChangeDecisionOutcome.Applied, result.Outcome); // the hold release itself succeeds
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync); // left for the retry job
        Assert.Null(stored.HeldForReviewAt); // no longer held — an in-session failure now
    }
}
