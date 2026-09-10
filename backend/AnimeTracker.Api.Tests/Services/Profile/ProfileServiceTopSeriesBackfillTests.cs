using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Profile;

// The Top series on-read backfill (design.md decision 6/task 4): reading the
// section enqueues a bounded batch of my-list anime with no stored series
// onto the existing ISeriesBuildTrigger, fire-and-forget. Uses the real
// SeriesBuildTrigger (not a fake) so the dedupe behaviour under test is the
// actual production guarantee, not a stand-in for it.
public class ProfileServiceTopSeriesBackfillTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries, ISeriesBuildTrigger trigger) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(),
            new SeriesRankingLookup(db),
            trigger,
            new FakeEpisodeScheduleService(),
            new UnusedAnimeRankingService());

    // Drains whatever is already queued, without blocking once the queue is
    // empty — a short per-item timeout stands in for "nothing more to dequeue".
    private static async Task<List<int>> DrainAsync(ISeriesBuildTrigger trigger, int maxItems)
    {
        var results = new List<int>();
        for (var i = 0; i < maxItems; i++)
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));
            try
            {
                results.Add(await trigger.WaitAsync(cts.Token));
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        return results;
    }

    private static UserAnimeEntry Entry(int animeId) => new() { AnimeId = animeId, Status = WatchStatus.Watching };

    [Fact]
    public async Task AtMost20IdsEnqueuedPerRead()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no series stored at all -> every list entry is "missing"
        var trigger = new SeriesBuildTrigger();
        var entries = Enumerable.Range(1, 25).Select(Entry).ToList();

        await CreateService(db, entries, trigger).GetTopSeriesSectionAsync();
        var drained = await DrainAsync(trigger, maxItems: 30);

        Assert.Equal(20, drained.Count);
    }

    [Fact]
    public async Task OnlyIdsWithNoSeriesMembershipAreEnqueued()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Already Built" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0 });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 2, Title = "Also Already Built" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 2, SeriesId = 1, IsMainLine = false, Order = 1 });
        await db.SaveChangesAsync();

        var trigger = new SeriesBuildTrigger();
        var entries = new List<UserAnimeEntry> { Entry(1), Entry(2), Entry(3), Entry(4) };

        await CreateService(db, entries, trigger).GetTopSeriesSectionAsync();
        var drained = await DrainAsync(trigger, maxItems: 10);

        Assert.Equal([3, 4], drained.OrderBy(id => id));
    }

    [Fact]
    public async Task NeverThrowsIntoTheResponseEvenWithManyMissingIds()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();
        var trigger = new SeriesBuildTrigger();
        var entries = Enumerable.Range(1, 50).Select(Entry).ToList();

        var section = await CreateService(db, entries, trigger).GetTopSeriesSectionAsync();

        Assert.NotNull(section);
    }

    [Fact]
    public async Task RepeatReadsDoNotRequeueStillMissingIds()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync();
        var trigger = new SeriesBuildTrigger();
        var entries = new List<UserAnimeEntry> { Entry(1), Entry(2), Entry(3) };

        await CreateService(db, entries, trigger).GetTopSeriesSectionAsync();
        var firstRead = await DrainAsync(trigger, maxItems: 10);
        Assert.Equal([1, 2, 3], firstRead.OrderBy(id => id));

        // Simulate anime 1's franchise getting built between reads — it drops
        // out of the "missing" set naturally; 2 and 3 are still missing.
        db.Series.Add(new SeriesModel { Id = 1, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 1, Title = "Now Built" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 1, SeriesId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();

        await CreateService(db, entries, trigger).GetTopSeriesSectionAsync();
        var secondRead = await DrainAsync(trigger, maxItems: 10);

        // 2 and 3 were already tracked as enqueued from the first read and
        // haven't been built yet, so the trigger's dedupe coalesces them into
        // no-ops — nothing new comes out of the queue.
        Assert.Empty(secondRead);
    }

    // Unlike GetTopSeriesSectionAsync above, GetRewatchedSeriesSectionAsync
    // deliberately does not enqueue background series builds (design.md
    // D10/task 9.5/11.6) — the same profile page's Top series read already
    // does, and queueing the same ids twice only contends on one queue.
    [Fact]
    public async Task RewatchedSeriesReadEnqueuesNoBackgroundBuilds()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no series stored at all -> every list entry would be "missing"
        var trigger = new SeriesBuildTrigger();
        var entries = Enumerable.Range(1, 5).Select(Entry).ToList();

        await CreateService(db, entries, trigger).GetRewatchedSeriesSectionAsync();
        var drained = await DrainAsync(trigger, maxItems: 10);

        Assert.Empty(drained);
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeEpisodeScheduleService : IEpisodeScheduleService
    {
        public Task<ResolvedEpisode?> ResolveOnLocalDateAsync(AnimeMetadata anime, DateOnly localDate, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> NextAiringInstantAsync(AnimeMetadata anime, DateTimeOffset afterUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<int?> EpisodesAiredAsOfAsync(AnimeMetadata anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<AnimeMetadata> anime, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
        public Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default) =>
            Task.FromResult(new Dictionary<int, int>());
    }

    // The backfill dedupe behaviour under test doesn't order by ranking — a
    // stub returning the empty ranking is enough
    // (tier-season-refresh-and-top-series-order task 4.7).
    private sealed class UnusedAnimeRankingService : IAnimeRankingService
    {
        public Task<AnimeRankingSnapshot> GetSnapshotAsync(CancellationToken ct = default) =>
            Task.FromResult(AnimeRankingSnapshot.Empty);
        public Task<List<AnimeRankingScoreCountDto>> GetScoreCountsAsync(string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<AnimeRankingTierDto?> GetTierAsync(int score, string mediaTypeScope, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ApplyTierOrderAsync(List<AnimeRankingTierOrderRequest> tiers, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MoveAdjacentAsync(int promotedAnimeId, int demotedAnimeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task PlaceLastInTierAsync(int animeId, int score, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
