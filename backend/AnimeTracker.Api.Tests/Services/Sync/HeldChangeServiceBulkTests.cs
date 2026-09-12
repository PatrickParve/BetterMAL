using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Sync;

// design.md D5: AcceptAllAsync/DeclineAllAsync report the held count as their
// total and progress after each decision; a per-item failure counts toward
// StillHeld rather than stopping the run.
public class HeldChangeServiceBulkTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task SeedTwoHeldEntriesAsync(AnimeTrackerDbContext db)
    {
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        db.AnimeMetadata.AddRange(anime1, anime2);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1) },
            new UserAnimeEntry { AnimeId = 2, Anime = anime2, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow.AddDays(-1) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task AcceptAllReportsTheHeldTotalAndProgressPerItem()
    {
        using var db = CreateDb();
        await SeedTwoHeldEntriesAsync(db);
        var malClient = new FakeHeldChangeMalClient();
        var sink = new RecordingProgressSink();

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).AcceptAllAsync(sink);

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.StillHeld);
        Assert.Equal([2], sink.Totals);
        Assert.Equal([1, 2], sink.Progress);
    }

    [Fact]
    public async Task DeclineAllReportsTheHeldTotalAndProgressPerItem()
    {
        using var db = CreateDb();
        await SeedTwoHeldEntriesAsync(db);
        var malClient = new FakeHeldChangeMalClient();
        malClient.SetStatus(1, new MalListStatus { Status = "watching" });
        malClient.SetStatus(2, new MalListStatus { Status = "watching" });
        var sink = new RecordingProgressSink();

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAllAsync(sink);

        Assert.Equal(2, result.Succeeded);
        Assert.Equal(0, result.StillHeld);
        Assert.Equal([2], sink.Totals);
        Assert.Equal([1, 2], sink.Progress);
    }

    [Fact]
    public async Task AnItemThatCannotBeAppliedCountsTowardStillHeldAndTheRunContinues()
    {
        using var db = CreateDb();
        await SeedTwoHeldEntriesAsync(db);
        var malClient = new FakeHeldChangeMalClient();
        malClient.FailReadFor(1); // decline for anime 1 fails to read MAL's current value
        malClient.SetStatus(2, new MalListStatus { Status = "watching" });
        var sink = new RecordingProgressSink();

        var result = await HeldChangeServiceTestFactory.Create(db, malClient).DeclineAllAsync(sink);

        Assert.Equal(1, result.Succeeded);
        Assert.Equal(1, result.StillHeld);
        Assert.Equal([2], sink.Totals);
        Assert.Equal([1, 2], sink.Progress); // both items are processed even though one stayed held
    }
}
