using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// design.md D5: DrainPendingAsync loads both the pending-edit and
// pending-removal id lists before the first push, so a sink's total counts
// exactly what the run set out to push, and it returns how many pushed
// versus stayed pending (DrainResult).
public class EntryPushServiceDrainTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static EntryPushService CreateService(AnimeTrackerDbContext db, IMalClient malClient) =>
        new(db, malClient, new FakeEntrySyncScheduler(), NullLogger<EntryPushService>.Instance);

    [Fact]
    public async Task TheTotalIsPendingEditsPlusPendingRemovals()
    {
        using var db = CreateDb();
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        var anime3 = new AnimeMetadata { Id = 3, Title = "Anime 3" };
        db.AnimeMetadata.AddRange(anime1, anime2, anime3);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, PendingSync = true });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = anime2, Status = WatchStatus.Watching, PendingSync = true });
        db.PendingEntryDeletions.Add(new PendingEntryDeletion { AnimeId = 3, Anime = anime3, RequestedAt = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        var malClient = new RecordingMalClient();
        var sink = new RecordingProgressSink();

        var result = await CreateService(db, malClient).DrainPendingAsync(sink);

        Assert.Equal([3], sink.Totals);
        Assert.Equal(3, result.Pushed);
        Assert.Equal(0, result.NotPushed);
        Assert.Equal([1, 2, 3], sink.Progress);
    }

    [Fact]
    public async Task AFailedPushCountsTowardNotPushedAndTheOthersStillGoThrough()
    {
        using var db = CreateDb();
        var anime1 = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var anime2 = new AnimeMetadata { Id = 2, Title = "Anime 2" };
        db.AnimeMetadata.AddRange(anime1, anime2);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime1, Status = WatchStatus.Watching, PendingSync = true });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = anime2, Status = WatchStatus.Watching, PendingSync = true });
        await db.SaveChangesAsync();
        var malClient = new RecordingMalClient();
        malClient.FailUpdateFor(1);

        var result = await CreateService(db, malClient).DrainPendingAsync();

        Assert.Equal(1, result.Pushed);
        Assert.Equal(1, result.NotPushed);
        var stillPending = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.True(stillPending.PendingSync);
    }

    private sealed class RecordingProgressSink : IJobProgressSink
    {
        public List<int> Totals { get; } = [];
        public List<int> Progress { get; } = [];
        public void SetTotal(int total) => Totals.Add(total);
        public void ReportProgress(int done) => Progress.Add(done);
    }

    private sealed class FakeEntrySyncScheduler : IEntrySyncScheduler
    {
        public void ScheduleSync(int animeId) { }
    }

    private sealed class RecordingMalClient : IMalClient
    {
        private readonly HashSet<int> _failUpdates = [];
        public void FailUpdateFor(int animeId) => _failUpdates.Add(animeId);

        public Task<MalListStatus> UpdateMyListStatusAsync(int animeId, MalListStatusUpdate update, CancellationToken ct = default)
        {
            if (_failUpdates.Contains(animeId))
                throw new InvalidOperationException("Simulated push failure.");
            return Task.FromResult(new MalListStatus());
        }

        public Task DeleteMyListStatusAsync(int animeId, CancellationToken ct = default) => Task.CompletedTask;

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
