using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// hold-startup-pending-sync-for-review tasks.md 9.8 / design.md D14:
// GetSyncStatusAsync counts held entries into HeldCount and excludes them
// from PendingCount, so the two figures never conflate "in flight" with
// "waiting on me".
public class UserAnimeEntryRepositoryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task HeldEntriesAreCountedInHeldCountAndExcludedFromPendingCount()
    {
        using var db = CreateDb();
        var unheldAnime = new AnimeMetadata { Id = 1, Title = "Unheld" };
        var heldAnime = new AnimeMetadata { Id = 2, Title = "Held" };
        var heldRemovalAnime = new AnimeMetadata { Id = 3, Title = "Held Removal" };
        db.AnimeMetadata.AddRange(unheldAnime, heldAnime, heldRemovalAnime);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = unheldAnime, Status = WatchStatus.Watching, PendingSync = true },
            new UserAnimeEntry { AnimeId = 2, Anime = heldAnime, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow });
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 3, Anime = heldRemovalAnime, RequestedAt = DateTimeOffset.UtcNow, HeldForReviewAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        var (pendingCount, heldCount, _) = await new UserAnimeEntryRepository(db).GetSyncStatusAsync();

        Assert.Equal(1, pendingCount);
        Assert.Equal(2, heldCount); // one held entry + one held removal
    }
}
