using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// hold-startup-pending-sync-for-review tasks.md 9.1: the startup stamp
// (design.md D1/D2) — a pending entry and a queued removal are held, an
// already-held row is left alone by a second stamp, and a non-pending entry
// is untouched.
public class StartupPendingSyncHoldTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static StartupPendingSyncHold CreateService(AnimeTrackerDbContext db) =>
        new(db, NullLogger<StartupPendingSyncHold>.Instance);

    [Fact]
    public async Task StampsAPendingEntryAndAQueuedRemoval()
    {
        using var db = CreateDb();
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        db.AnimeMetadata.AddRange(anime1, anime2);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, PendingSync = true });
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 2, Anime = anime2, RequestedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();

        await CreateService(db).ApplyAsync();

        var entry = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.NotNull(entry.HeldForReviewAt);

        var removal = await db.PendingEntryDeletions.AsNoTracking().SingleAsync(d => d.AnimeId == 2);
        Assert.NotNull(removal.HeldForReviewAt);
    }

    [Fact]
    public async Task AnAlreadyHeldEntryIsNeitherReDatedNorReleased()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-3);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        await CreateService(db).ApplyAsync();

        var entry = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(heldAt, entry.HeldForReviewAt);
    }

    [Fact]
    public async Task AnAlreadyHeldRemovalIsNeitherReDatedNorReleased()
    {
        using var db = CreateDb();
        var heldAt = DateTimeOffset.UtcNow.AddDays(-3);
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow.AddDays(-3), HeldForReviewAt = heldAt,
        });
        await db.SaveChangesAsync();

        await CreateService(db).ApplyAsync();

        var removal = await db.PendingEntryDeletions.AsNoTracking().SingleAsync(d => d.AnimeId == 1);
        Assert.Equal(heldAt, removal.HeldForReviewAt);
    }

    [Fact]
    public async Task ANonPendingEntryIsUntouched()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, PendingSync = false });
        await db.SaveChangesAsync();

        await CreateService(db).ApplyAsync();

        var entry = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Null(entry.HeldForReviewAt);
    }
}
