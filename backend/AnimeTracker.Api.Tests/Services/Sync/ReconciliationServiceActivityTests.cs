using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// log-only-my-own-edits (design D1): AcceptPendingDiffAsync is a sync path
// and records nothing, whatever it applies; declining a diff already applied
// nothing and still records nothing.
public class ReconciliationServiceActivityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ReconciliationService CreateService(AnimeTrackerDbContext db) =>
        new(new ThrowingMalClient(), db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);

    private static PendingReconciliationDiffEntry DiffEntry(
        int animeId, ReconciliationDiffChangeType changeType, WatchStatus status, int episodesWatched,
        int? myScore = null, DateOnly? startedAt = null, DateOnly? completedAt = null, int rewatchCount = 0) => new()
    {
        AnimeId = animeId,
        ChangeType = changeType,
        Status = status,
        EpisodesWatched = episodesWatched,
        MyScore = myScore,
        StartedAt = startedAt,
        CompletedAt = completedAt,
        RewatchCount = rewatchCount,
    };

    [Fact]
    public async Task AcceptingADiffWithChangedFieldsAppliesThemAndRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3, MyScore = null,
        });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Updated, WatchStatus.Watching, 7, myScore: 8)],
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).AcceptPendingDiffAsync();

        Assert.True(result);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(7, stored.EpisodesWatched);
        Assert.Equal(8, stored.MyScore);
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AcceptingADiffWithANewEntryAppliesItAndRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Added, WatchStatus.Completed, 12)],
        });
        await db.SaveChangesAsync();

        await CreateService(db).AcceptPendingDiffAsync();

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Completed, stored.Status);
        Assert.Equal(12, stored.EpisodesWatched);
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task AnEntryWithAPendingSyncEditIsSkippedAndRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3, PendingSync = true,
        });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Updated, WatchStatus.Watching, 7)],
        });
        await db.SaveChangesAsync();

        await CreateService(db).AcceptPendingDiffAsync();

        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(3, stored.EpisodesWatched); // untouched
        Assert.True(stored.PendingSync);
        Assert.Empty(await db.ActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task DecliningRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Updated, WatchStatus.Watching, 7)],
        });
        await db.SaveChangesAsync();

        var result = await CreateService(db).CancelPendingDiffAsync();

        Assert.True(result);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
        Assert.Empty(await db.ActivityLogs.ToListAsync());
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(3, stored.EpisodesWatched); // untouched
    }

    private sealed class ThrowingMalClient : IMalClient
    {
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
        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<MalListStatus?> GetMyListStatusAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
