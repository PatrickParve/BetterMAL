using AnimeTracker.Api.Data;
using AnimeTracker.Api.Services.Jobs;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using AnimeTracker.Api.Tests.Services.Search;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// design.md D5/D6: MyAnimeList never gives reconciliation a total, so a
// manual run reports only the running "anime read" count as MAL's list is
// paged through, and never sets a total. design.md D6: the weekly run passes
// no sink and reconciliation runs one at a time behind ReconciliationRunGate.
public class ReconciliationServiceProgressTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ReconciliationService CreateService(IMalClient malClient, AnimeTrackerDbContext db, ReconciliationRunGate? gate = null) =>
        new(malClient, db, gate ?? new ReconciliationRunGate(), new FakeAnimeSearchIndex(), NullLogger<ReconciliationService>.Instance);

    private static MalUserAnimeListEdge Edge(int animeId) => new()
    {
        Node = new MalAnimeNode { Id = animeId, Title = $"Anime {animeId}" },
        ListStatus = new MalListStatus { Status = "watching", NumEpisodesWatched = 0 },
    };

    [Fact]
    public async Task RunAsyncWithASinkReportsEachPagesCumulativeCountAndNeverSetsATotal()
    {
        using var db = CreateDb();
        var malClient = new PagingMalClient([3, 2]);
        var sink = new RecordingProgressSink();

        await CreateService(malClient, db).RunAsync(sink);

        Assert.Equal([3, 5], sink.Progress);
        Assert.Empty(sink.Totals);
    }

    [Fact]
    public async Task RunAsyncWithNoSinkTouchesNothing()
    {
        using var db = CreateDb();
        var malClient = new PagingMalClient([2]);

        var result = await CreateService(malClient, db).RunAsync();

        Assert.Equal(2, result.Added);
    }

    [Fact]
    public async Task TwoOverlappingRunsSharingOneGateLeaveExactlyOnePendingDiff()
    {
        using var db = CreateDb();
        var releaseFirst = new TaskCompletionSource();
        var malClient = new BlockingMalClient(releaseFirst, [Edge(1)]);
        var gate = new ReconciliationRunGate();
        var service = CreateService(malClient, db, gate);

        var first = service.RunAsync();
        using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
        {
            while (malClient.CallCount < 1 && !cts.IsCancellationRequested)
                await Task.Delay(10, CancellationToken.None);
        }
        Assert.Equal(1, malClient.CallCount); // first has grabbed the gate and is blocked inside the MAL fetch

        var second = service.RunAsync();
        await Task.Delay(50); // give the second call time to reach and block on the gate
        releaseFirst.SetResult();

        await Task.WhenAll(first, second);

        var diffs = await db.PendingReconciliationDiffs.ToListAsync();
        Assert.Single(diffs);
    }

    private sealed class RecordingProgressSink : IJobProgressSink
    {
        public List<int> Totals { get; } = [];
        public List<int> Progress { get; } = [];
        public void SetTotal(int total) => Totals.Add(total);
        public void ReportProgress(int done) => Progress.Add(done);
    }

    private sealed class PagingMalClient(int[] pageSizes) : IMalClient
    {
        public Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default)
        {
            var all = new List<MalUserAnimeListEdge>();
            var nextId = 1;
            foreach (var size in pageSizes)
            {
                for (var i = 0; i < size; i++)
                    all.Add(Edge(nextId++));
                onPageRead?.Invoke(all.Count);
            }
            return Task.FromResult(all);
        }

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

    // Blocks its first call on `releaseFirst`, so a test can hold the run
    // gate open long enough to start a second, genuinely overlapping call.
    private sealed class BlockingMalClient(TaskCompletionSource releaseFirst, List<MalUserAnimeListEdge> edges) : IMalClient
    {
        public int CallCount { get; private set; }

        public async Task<List<MalUserAnimeListEdge>> GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default)
        {
            CallCount++;
            if (CallCount == 1)
                await releaseFirst.Task;
            return edges;
        }

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
