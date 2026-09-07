using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// record-mal-origin-activity design D9 (tasks.md 8.3): AcceptPendingDiffAsync
// records what it applied with MalReconciliation; declining and skipping
// record nothing.
public class ReconciliationServiceActivityTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ReconciliationService CreateService(AnimeTrackerDbContext db) =>
        new(new ThrowingMalClient(), db, NullLogger<ReconciliationService>.Instance);

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
    public async Task AcceptingADiffRecordsOneRowPerGenuinelyChangedField()
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
        var rows = await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync();
        Assert.Equal(2, rows.Count); // episodes + score; status unchanged, no row
        Assert.All(rows, r => Assert.Equal(ActivityChangeSource.MalReconciliation, r.Source));
        Assert.Contains(rows, r => r.ChangeType == ActivityChangeType.EpisodeIncremented && r.ChangeDetail == "Episode 7");
        Assert.Contains(rows, r => r.ChangeType == ActivityChangeType.ScoreChanged && r.ChangeDetail == "Score 8");
    }

    [Fact]
    public async Task ADiffEntryWhoseValuesAlreadyMatchRecordsNothing()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 7, MyScore = 8,
        });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Updated, WatchStatus.Watching, 7, myScore: 8)],
        });
        await db.SaveChangesAsync();

        await CreateService(db).AcceptPendingDiffAsync();

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
    }

    [Fact]
    public async Task APendingSyncEntryRecordsNothing()
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

        Assert.Empty(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(3, stored.EpisodesWatched); // untouched
        Assert.True(stored.PendingSync);
    }

    [Fact]
    public async Task ANewEntryRecordsOneAddedRow()
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

        var row = Assert.Single(await db.ActivityLogs.Where(a => a.AnimeId == 1).ToListAsync());
        Assert.Equal(ActivityChangeType.Added, row.ChangeType);
        Assert.Equal("Added as Completed", row.ChangeDetail);
        Assert.Equal(ActivityChangeSource.MalReconciliation, row.Source);
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
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(CancellationToken ct = default) =>
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
