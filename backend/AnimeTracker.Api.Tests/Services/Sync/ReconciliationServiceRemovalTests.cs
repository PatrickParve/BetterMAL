using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// mal-write-sync "Reconciliation proposes removing anime MyAnimeList no
// longer lists" (design.md D4, D5, D6): what gets proposed for removal, what
// is excluded, and what Accept/Cancel do to a RemovedOnMal entry.
public class ReconciliationServiceRemovalTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<UserAnimeEntry> SeedLocalAsync(
        AnimeTrackerDbContext db, int animeId, WatchStatus status = WatchStatus.Watching,
        int episodesWatched = 5, bool pendingSync = false, DateTimeOffset? lastSyncedAt = null)
    {
        var anime = new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}" };
        var entry = new UserAnimeEntry
        {
            AnimeId = animeId,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            PendingSync = pendingSync,
            LastSyncedAt = lastSyncedAt,
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static MalUserAnimeListEdge RemoteEdge(int animeId, string status = "watching", int episodesWatched = 5) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = status, NumEpisodesWatched = episodesWatched },
    };

    [Fact]
    public async Task ALocalEntryMalDoesNotListGetsARemovedOnMalEntryCarryingItsLocalValues()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1, WatchStatus.Watching, episodesWatched: 7);
        var malClient = new FakeMalClient([]); // MAL lists nothing

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(1, result.RemovedOnMal);
        var diff = await db.PendingReconciliationDiffs.Include(d => d.Entries).SingleAsync();
        var entry = Assert.Single(diff.Entries);
        Assert.Equal(ReconciliationDiffChangeType.RemovedOnMal, entry.ChangeType);
        Assert.Equal(1, entry.AnimeId);
        Assert.Equal(WatchStatus.Watching, entry.Status);
        Assert.Equal(7, entry.EpisodesWatched);
    }

    [Fact]
    public async Task APendingSyncEntryIsNotProposedForRemoval()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1, pendingSync: true);
        var malClient = new FakeMalClient([]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.RemovedOnMal);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    [Fact]
    public async Task AnAnimeWithAQueuedRemovalIsNotProposedAgain()
    {
        using var db = CreateDb();
        var entry = await SeedLocalAsync(db, 1);
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 1, Anime = entry.Anime, RequestedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var malClient = new FakeMalClient([]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.RemovedOnMal);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    [Fact]
    public async Task AnAnimeMalListsWithAnUnrecognizedStatusIsNotProposedForRemoval()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var malClient = new FakeMalClient([RemoteEdge(1, status: "rewatching_v2")]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.RemovedOnMal);
        Assert.Equal(1, result.SkippedUnrecognized);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    [Fact]
    public async Task AnEntryPushedAfterTheReadStartedIsNotProposedForRemoval()
    {
        using var db = CreateDb();
        // Later than any readStartedAt this run captures at call time — as if
        // a debounced push landed while a long list read was in flight.
        await SeedLocalAsync(db, 1, lastSyncedAt: DateTimeOffset.UtcNow.AddMinutes(10));
        var malClient = new FakeMalClient([]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.RemovedOnMal);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    [Fact]
    public async Task AcceptingARemovalDeletesTheEntryWithNoQueuedRemovalOrActivity()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var service = new ReconciliationService(new FakeMalClient([]), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        Assert.Null(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
        Assert.Empty(await db.PendingEntryDeletions.ToListAsync());
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task ARemovalIsSkippedWhenTheEntryHasSinceBecomePendingSync()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var service = new ReconciliationService(new FakeMalClient([]), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        var entry = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 1);
        entry.PendingSync = true;
        await db.SaveChangesAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        Assert.NotNull(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
    }

    [Fact]
    public async Task ARemovalIsSkippedWhenTheEntrysLastSyncedAtIsAfterComputedAt()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var service = new ReconciliationService(new FakeMalClient([]), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        // As if a push landed and stamped LastSyncedAt after this diff was computed.
        var entry = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 1);
        entry.LastSyncedAt = DateTimeOffset.UtcNow.AddMinutes(10);
        await db.SaveChangesAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        Assert.NotNull(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
    }

    [Fact]
    public async Task ARemovalForAnEntryAlreadyGoneIsANoOp()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var service = new ReconciliationService(new FakeMalClient([]), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        db.UserAnimeEntries.Remove(await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 1));
        await db.SaveChangesAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        Assert.Empty(await db.UserAnimeEntries.ToListAsync());
    }

    [Fact]
    public async Task AMixedDiffStillAppliesItsAddedAndUpdatedEntries()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1); // removed on MAL
        await SeedLocalAsync(db, 2, WatchStatus.Watching, episodesWatched: 3); // updated
        var malClient = new FakeMalClient([RemoteEdge(2, episodesWatched: 9), RemoteEdge(3)]); // anime 3 added
        var service = new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        Assert.Null(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
        Assert.Equal(9, (await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 2)).EpisodesWatched);
        Assert.NotNull(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 3));
    }

    [Fact]
    public async Task CancellingADiffWithARemovalLeavesTheEntry()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, 1);
        var service = new ReconciliationService(new FakeMalClient([]), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        var cancelled = await service.CancelPendingDiffAsync();

        Assert.True(cancelled);
        Assert.NotNull(await db.UserAnimeEntries.AsNoTracking().SingleOrDefaultAsync(e => e.AnimeId == 1));
    }

    private sealed class FakeMalClient(List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default) =>
            Task.FromResult(edges);

        public Task<MalPagedResponse<MalAnimeListEdge>> SearchAnimeAsync(string query, int limit = 5, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetSeasonAsync(int year, string season, int limit = 100, int offset = 0, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<MalAnimeListEdge>?> GetFullSeasonAsync(int year, string season, string? sort = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalAnimeListEdge>> GetRankingAsync(string rankingType = "all", int limit = 100, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalAnimeNode> GetAnimeDetailsAsync(int animeId, IReadOnlyCollection<string>? fields = null, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalPagedResponse<MalUserAnimeListEdge>> GetUserAnimeListAsync(string? status = null, int limit = 100, int offset = 0, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
