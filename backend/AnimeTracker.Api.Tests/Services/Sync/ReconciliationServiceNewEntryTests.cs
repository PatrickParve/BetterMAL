using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Tests.Services.Airing;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// episode-airing-data "Event-driven refresh triggers": accepting a list check
// queues each anime it brings into my list, once the accepted entries are saved,
// and queues nothing for an entry that was already there.
public class ReconciliationServiceNewEntryTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ReconciliationService CreateService(AnimeTrackerDbContext db, INewEntryRefreshTrigger trigger) =>
        new(new ThrowingMalClient(), db, new ReconciliationRunGate(), new FakeAnimeSearchIndex(), trigger, NullLogger<ReconciliationService>.Instance);

    private static PendingReconciliationDiffEntry DiffEntry(int animeId, ReconciliationDiffChangeType changeType) => new()
    {
        AnimeId = animeId,
        ChangeType = changeType,
        Status = WatchStatus.Completed,
        EpisodesWatched = 12,
    };

    [Fact]
    public async Task AcceptingAddedAnimeQueuesEachOnceTheEntriesAreSaved()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 3; id++)
            db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}" });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries =
            [
                DiffEntry(1, ReconciliationDiffChangeType.Added),
                DiffEntry(2, ReconciliationDiffChangeType.Added),
                DiffEntry(3, ReconciliationDiffChangeType.Added),
            ],
        });
        await db.SaveChangesAsync();
        var entriesWhenQueued = new List<int>();
        var trigger = new RecordingNewEntryRefreshTrigger(_ =>
            entriesWhenQueued.Add(db.UserAnimeEntries.AsNoTracking().Count()));

        await CreateService(db, trigger).AcceptPendingDiffAsync();

        Assert.Equal([1, 2, 3], trigger.Enqueued);
        Assert.All(entriesWhenQueued, count => Assert.Equal(3, count)); // all saved before any is queued
    }

    [Fact]
    public async Task AcceptingAnUpdatedOrRemovedEntryQueuesNothing()
    {
        using var db = CreateDb();
        foreach (var id in new[] { 1, 2 })
        {
            var anime = new AnimeMetadata { Id = id, Title = $"Anime {id}" };
            db.AnimeMetadata.Add(anime);
            db.UserAnimeEntries.Add(new UserAnimeEntry
            {
                AnimeId = id, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3,
                LastSyncedAt = DateTimeOffset.UtcNow.AddDays(-2),
            });
        }
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries =
            [
                DiffEntry(1, ReconciliationDiffChangeType.Updated),
                DiffEntry(2, ReconciliationDiffChangeType.RemovedOnMal),
            ],
        });
        await db.SaveChangesAsync();
        var trigger = new RecordingNewEntryRefreshTrigger();

        await CreateService(db, trigger).AcceptPendingDiffAsync();

        Assert.Empty(trigger.Enqueued);
    }

    [Fact]
    public async Task AnAddedEntryThatAppearedLocallyMeanwhileIsNotQueuedAgain()
    {
        using var db = CreateDb();
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = anime, Status = WatchStatus.Watching, EpisodesWatched = 3 });
        db.PendingReconciliationDiffs.Add(new PendingReconciliationDiff
        {
            ComputedAt = DateTimeOffset.UtcNow,
            Entries = [DiffEntry(1, ReconciliationDiffChangeType.Added)],
        });
        await db.SaveChangesAsync();
        var trigger = new RecordingNewEntryRefreshTrigger();

        await CreateService(db, trigger).AcceptPendingDiffAsync();

        Assert.Empty(trigger.Enqueued); // the entry existed already, so nothing was created
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
