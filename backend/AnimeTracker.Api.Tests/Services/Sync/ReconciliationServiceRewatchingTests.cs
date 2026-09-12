using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Dto;
using AnimeTracker.Api.Services.Sync;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Sync;

// mal-write-sync "Reconciliation does not demote a rewatch" (tasks.md 5.8) —
// the highest-consequence rule in the change: getting it wrong silently
// destroys user state on a background pass.
public class ReconciliationServiceRewatchingTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<UserAnimeEntry> SeedLocalAsync(
        AnimeTrackerDbContext db, WatchStatus status, int episodesWatched = 5, int? myScore = null,
        int rewatchCount = 0, DateOnly? startedAt = null, DateOnly? completedAt = null)
    {
        var anime = new AnimeMetadata { Id = 1, Title = "Anime 1" };
        var entry = new UserAnimeEntry
        {
            AnimeId = 1,
            Anime = anime,
            Status = status,
            EpisodesWatched = episodesWatched,
            MyScore = myScore,
            RewatchCount = rewatchCount,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            PendingSync = false,
        };
        db.AnimeMetadata.Add(anime);
        db.UserAnimeEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static MalUserAnimeListEdge RemoteEdge(
        string status, int episodesWatched = 5, int? myScore = null, int rewatchCount = 0,
        DateOnly? startedAt = null, DateOnly? completedAt = null) => new()
    {
        Node = new MalAnimeNode { Id = 1, Title = "Anime 1" },
        ListStatus = new MalListStatus
        {
            Status = status,
            NumEpisodesWatched = episodesWatched,
            Score = myScore ?? 0,
            NumTimesRewatched = rewatchCount,
            StartDate = startedAt?.ToString("yyyy-MM-dd"),
            FinishDate = completedAt?.ToString("yyyy-MM-dd"),
        },
    };

    [Fact]
    public async Task ARewatchSurvivesReconciliationAgainstARemoteWatchingStatus()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, WatchStatus.Rewatching, episodesWatched: 5, rewatchCount: 2);
        var malClient = new FakeMalClient([RemoteEdge("watching", episodesWatched: 5, rewatchCount: 2)]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(1, result.Unchanged);
        Assert.Equal(0, result.Updated);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status);
        Assert.Empty(await db.PendingReconciliationDiffs.ToListAsync());
    }

    [Fact]
    public async Task ARealRemoteChangeStillAppliesWhenTheRemoteStatusIsNotWatching()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, WatchStatus.Rewatching, episodesWatched: 5, rewatchCount: 2);
        var malClient = new FakeMalClient([RemoteEdge("dropped", episodesWatched: 5, rewatchCount: 2)]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.Unchanged);
        Assert.Equal(1, result.Updated);
        var diff = await db.PendingReconciliationDiffs.Include(d => d.Entries).SingleAsync();
        var diffEntry = Assert.Single(diff.Entries);
        Assert.Equal(WatchStatus.Dropped, diffEntry.Status);
    }

    [Fact]
    public async Task OtherFieldsStillDiffBetweenALocalRewatchAndItsRemoteWatchingCopy()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, WatchStatus.Rewatching, episodesWatched: 5, rewatchCount: 2);
        // Same status (Rewatching ~ watching), but a different episode count —
        // must still be recorded as a difference (design.md D4: "constrained to
        // the status comparison only").
        var malClient = new FakeMalClient([RemoteEdge("watching", episodesWatched: 7, rewatchCount: 2)]);

        var result = await new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance).RunAsync();

        Assert.Equal(0, result.Unchanged);
        Assert.Equal(1, result.Updated);
        var diff = await db.PendingReconciliationDiffs.Include(d => d.Entries).SingleAsync();
        var diffEntry = Assert.Single(diff.Entries);
        Assert.Equal(7, diffEntry.EpisodesWatched);
    }

    // design.md D2: the held diff records the status the entry will end up
    // with (Rewatching), not MAL's raw watching — otherwise accepting a diff
    // raised by some other field would demote the rewatch. Recorded here at
    // the diff-review layer; AcceptPendingDiffAsync's own re-resolution is
    // covered separately below.
    [Fact]
    public async Task AcceptingADiffRaisedByAnotherFieldKeepsTheEntryRewatching()
    {
        using var db = CreateDb();
        await SeedLocalAsync(db, WatchStatus.Rewatching, episodesWatched: 5, rewatchCount: 2);
        var malClient = new FakeMalClient([RemoteEdge("watching", episodesWatched: 7, rewatchCount: 2)]);
        var service = new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status);
        Assert.Equal(7, stored.EpisodesWatched);
    }

    // design.md D2: AcceptPendingDiffAsync re-resolves against the entry as it
    // stands at accept time, not as it stood when the diff was computed — a
    // diff computed before the entry became a rewatch must not demote it.
    [Fact]
    public async Task ADiffComputedBeforeTheEntryBecameARewatchDoesNotDemoteItOnAccept()
    {
        using var db = CreateDb();
        // Computed while the entry was still plain Watching, against a
        // remote that differs on episodes — a genuine diff at compute time.
        await SeedLocalAsync(db, WatchStatus.Watching, episodesWatched: 5);
        var malClient = new FakeMalClient([RemoteEdge("watching", episodesWatched: 9)]);
        var service = new ReconciliationService(malClient, db, new ReconciliationRunGate(), NullLogger<ReconciliationService>.Instance);
        await service.RunAsync();

        // The entry becomes a rewatch after the diff was computed, but before
        // it's reviewed and accepted.
        var entry = await db.UserAnimeEntries.SingleAsync(e => e.AnimeId == 1);
        entry.Status = WatchStatus.Rewatching;
        await db.SaveChangesAsync();

        var accepted = await service.AcceptPendingDiffAsync();

        Assert.True(accepted);
        var stored = await db.UserAnimeEntries.AsNoTracking().SingleAsync(e => e.AnimeId == 1);
        Assert.Equal(WatchStatus.Rewatching, stored.Status);
        Assert.Equal(9, stored.EpisodesWatched);
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
