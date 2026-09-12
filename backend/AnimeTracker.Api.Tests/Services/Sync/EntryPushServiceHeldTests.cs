using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// hold-startup-pending-sync-for-review tasks.md 9.2 (design.md D3): the push
// guard lives at the MAL call itself, so a held entry or removal is refused by
// PushIfPendingAsync/PushPendingDeletionAsync without calling MAL, and
// DrainPendingAsync's queries exclude held rows entirely.
public class EntryPushServiceHeldTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EntryPushService CreateService(AnimeTrackerDbContext db, RecordingMalClient malClient) =>
        new(db, malClient, new FakeEntrySyncScheduler(), NullLogger<EntryPushService>.Instance);

    [Fact]
    public async Task AHeldEntryIsNotPushedByPushIfPendingAsync()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
            PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var malClient = new RecordingMalClient();

        var result = await CreateService(db, malClient).PushIfPendingAsync(1);

        Assert.False(result);
        Assert.False(malClient.UpdateCalled);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stored.PendingSync);
        Assert.NotNull(stored.HeldForReviewAt);
    }

    [Fact]
    public async Task AHeldRemovalIsNotPushedByPushPendingDeletionAsync()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion
        {
            AnimeId = 1, Anime = anime, RequestedAt = DateTimeOffset.UtcNow, HeldForReviewAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();
        var malClient = new RecordingMalClient();

        var result = await CreateService(db, malClient).PushPendingDeletionAsync(1);

        Assert.False(result);
        Assert.False(malClient.DeleteCalled);
        Assert.NotNull(await db.PendingEntryDeletions.AsNoTracking().SingleOrDefaultAsync(d => d.AnimeId == 1));
    }

    [Fact]
    public async Task DrainPendingAsyncPushesOnlyTheUnheldItemsAndLeavesTheHeldOnesPending()
    {
        using var db = CreateDb();
        var unheldEntryAnime = new AnimeMetadata { Id = 1, Title = "Unheld Entry" };
        var heldEntryAnime = new AnimeMetadata { Id = 2, Title = "Held Entry" };
        var unheldRemovalAnime = new AnimeMetadata { Id = 3, Title = "Unheld Removal" };
        var heldRemovalAnime = new AnimeMetadata { Id = 4, Title = "Held Removal" };
        db.AnimeMetadata.AddRange(unheldEntryAnime, heldEntryAnime, unheldRemovalAnime, heldRemovalAnime);
        db.UserAnimeEntries.AddRange(
            new UserAnimeEntry { AnimeId = 1, Anime = unheldEntryAnime, Status = WatchStatus.Watching, PendingSync = true },
            new UserAnimeEntry { AnimeId = 2, Anime = heldEntryAnime, Status = WatchStatus.Watching, PendingSync = true, HeldForReviewAt = DateTimeOffset.UtcNow });
        db.PendingEntryDeletions.AddRange(
            new PendingEntryDeletion { AnimeId = 3, Anime = unheldRemovalAnime, RequestedAt = DateTimeOffset.UtcNow },
            new PendingEntryDeletion { AnimeId = 4, Anime = heldRemovalAnime, RequestedAt = DateTimeOffset.UtcNow, HeldForReviewAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var malClient = new RecordingMalClient();

        var result = await CreateService(db, malClient).DrainPendingAsync();

        Assert.Equal(2, result.Pushed);
        Assert.Equal(0, result.NotPushed);
        Assert.Equal([1], malClient.UpdatedAnimeIds);
        Assert.Equal([3], malClient.DeletedAnimeIds);

        var entry1 = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.False(entry1.PendingSync);

        var entry2 = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 2);
        Assert.True(entry2.PendingSync);
        Assert.NotNull(entry2.HeldForReviewAt);

        Assert.Null(await db.PendingEntryDeletions.AsNoTracking().SingleOrDefaultAsync(d => d.AnimeId == 3));
        Assert.NotNull(await db.PendingEntryDeletions.AsNoTracking().SingleOrDefaultAsync(d => d.AnimeId == 4));
    }

    private sealed class FakeEntrySyncScheduler : IEntrySyncScheduler
    {
        public void ScheduleSync(int animeId) { }
    }

    private sealed class RecordingMalClient : IMalClient
    {
        public bool UpdateCalled { get; private set; }
        public bool DeleteCalled { get; private set; }
        public List<int> UpdatedAnimeIds { get; } = [];
        public List<int> DeletedAnimeIds { get; } = [];

        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default)
        {
            UpdateCalled = true;
            UpdatedAnimeIds.Add(animeId);
            return Task.FromResult(new MalListStatus());
        }

        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default)
        {
            DeleteCalled = true;
            DeletedAnimeIds.Add(animeId);
            return Task.CompletedTask;
        }

        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
