using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Services.Profile;

// record-mal-origin-activity (profile-stats spec "Changes that came from
// MyAnimeList appear alongside my own", tasks.md 8.6): MAL-origin rows ride
// the same feed/history lists as local edits, with every existing filtering
// and collapsing rule applying unchanged, and now carry Source through.
public class ProfileServiceMalOriginTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<ActivityLog> recent, List<ActivityLog>? all = null) =>
        new(
            new FakeUserAnimeEntryRepository(),
            new FakeActivityLogRepository(recent, all ?? recent),
            new FakeTopAnimeSelectionRepository(),
            new SeriesRankingLookup(db),
            new FakeSeriesBuildTrigger(),
            new FakeEpisodeScheduleService(),
            new UnusedAnimeRankingService());

    private static AnimeMetadata Anime(int id) => new() { Id = id, Title = $"Anime {id}" };

    private static ActivityLog Log(
        long id, int animeId, ActivityChangeType changeType, DateTimeOffset timestamp,
        ActivityChangeSource source = ActivityChangeSource.BetterMal, string? detail = null, int? previousEpisodesWatched = null) => new()
    {
        Id = id,
        AnimeId = animeId,
        Anime = Anime(animeId),
        ChangeType = changeType,
        ChangeDetail = detail,
        PreviousEpisodesWatched = previousEpisodesWatched,
        Timestamp = timestamp,
        Source = source,
    };

    [Fact]
    public async Task MalOriginRowsAppearInterleavedByTimeWithLocalRowsInTheFeed()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        List<ActivityLog> window =
        [
            Log(3, 1, ActivityChangeType.Added, now, ActivityChangeSource.MalStartupImport, "Added as Watching"),
            Log(2, 2, ActivityChangeType.ScoreChanged, now.AddMinutes(-1), ActivityChangeSource.BetterMal, "Score 9"),
            Log(1, 3, ActivityChangeType.EpisodeIncremented, now.AddMinutes(-2), ActivityChangeSource.MalReconciliation, "Episode 7"),
        ];

        var profile = await CreateService(db, window).GetProfileAsync();

        Assert.Equal([1, 2, 3], profile.RecentActivity.Select(i => i.AnimeId));
        Assert.Equal("MalStartupImport", profile.RecentActivity[0].Source.ToString());
        Assert.Equal("BetterMal", profile.RecentActivity[1].Source.ToString());
        Assert.Equal("MalReconciliation", profile.RecentActivity[2].Source.ToString());
    }

    [Fact]
    public async Task MalOriginRowsAppearInterleavedByTimeWithLocalRowsInTheHistory()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        List<ActivityLog> all =
        [
            Log(2, 1, ActivityChangeType.StatusChanged, now, ActivityChangeSource.MalResync, "Watching -> Dropped"),
            Log(1, 2, ActivityChangeType.ScoreChanged, now.AddMinutes(-1), ActivityChangeSource.BetterMal, "Score 9"),
        ];

        var history = await CreateService(db, recent: [], all: all).GetActivityHistoryAsync();

        Assert.Equal(2, history.Count);
        Assert.Equal("MalResync", history[0].Source.ToString());
        Assert.Equal("BetterMal", history[1].Source.ToString());
    }

    [Fact]
    public async Task TheFeedStillDropsNonCompletionStatusChangesAndDateChangesRegardlessOfOrigin()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        List<ActivityLog> window =
        [
            Log(3, 1, ActivityChangeType.StatusChanged, now, ActivityChangeSource.MalResync, "Watching -> Dropped"),
            Log(2, 2, ActivityChangeType.StartDateChanged, now.AddMinutes(-1), ActivityChangeSource.MalReconciliation, "Start date 2026-01-01"),
            Log(1, 3, ActivityChangeType.FinishDateChanged, now.AddMinutes(-2), ActivityChangeSource.MalResync, "Finish date 2026-02-01"),
        ];

        var profile = await CreateService(db, window).GetProfileAsync();

        Assert.Empty(profile.RecentActivity);
    }

    [Fact]
    public async Task AMalOriginCompletionReachesTheFeed()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        List<ActivityLog> window = [Log(1, 1, ActivityChangeType.Completed, now, ActivityChangeSource.MalResync, "Completed")];

        var profile = await CreateService(db, window).GetProfileAsync();

        var item = Assert.Single(profile.RecentActivity);
        Assert.Equal(ActivityChangeType.Completed, item.ChangeType);
        Assert.Equal("MalResync", item.Source.ToString());
    }

    [Fact]
    public async Task AMalOriginCompletionPlusScoreWrittenInOneApplicationCollapsesToOneRow()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        // Emission order mirrors EntryActivityRecorder.Diff: completion then
        // score, score taking the higher Id (design D5).
        List<ActivityLog> window =
        [
            Log(2, 1, ActivityChangeType.ScoreChanged, now, ActivityChangeSource.MalResync, "Score 9"),
            Log(1, 1, ActivityChangeType.Completed, now, ActivityChangeSource.MalResync, "Completed"),
        ];

        var profile = await CreateService(db, window).GetProfileAsync();

        var item = Assert.Single(profile.RecentActivity);
        Assert.Equal(ActivityChangeType.Completed, item.ChangeType);
        Assert.Contains("9", item.Summary);
    }

    [Fact]
    public async Task ConsecutiveMalOriginEpisodeRowsCollapseInTheHistory()
    {
        using var db = CreateDb();
        var now = DateTimeOffset.UtcNow;
        List<ActivityLog> all =
        [
            Log(3, 1, ActivityChangeType.EpisodeIncremented, now, ActivityChangeSource.MalResync, "Episode 9", previousEpisodesWatched: 8),
            Log(2, 1, ActivityChangeType.EpisodeIncremented, now.AddMinutes(-1), ActivityChangeSource.MalResync, "Episode 8", previousEpisodesWatched: 7),
            Log(1, 1, ActivityChangeType.EpisodeIncremented, now.AddMinutes(-2), ActivityChangeSource.MalResync, "Episode 7", previousEpisodesWatched: 6),
        ];

        var history = await CreateService(db, recent: [], all: all).GetActivityHistoryAsync();

        var item = Assert.Single(history);
        Assert.Equal(ActivityChangeType.EpisodeIncremented, item.ChangeType);
        Assert.Contains("7", item.Summary);
        Assert.Contains("9", item.Summary);
    }

    private sealed class FakeUserAnimeEntryRepository : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<UserAnimeEntry>());
        public Task<(int PendingCount, int HeldCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeActivityLogRepository(List<ActivityLog> recent, List<ActivityLog> all) : IActivityLogRepository
    {
        public Task<List<ActivityLog>> GetRecentAsync(int count, CancellationToken ct = default) => Task.FromResult(recent);
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(all);
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeSeriesBuildTrigger : ISeriesBuildTrigger
    {
        public List<int> Enqueued { get; } = [];
        public void Enqueue(int animeId) => Enqueued.Add(animeId);
        public Task<int> WaitAsync(CancellationToken ct) => throw new NotImplementedException();
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

    // Not touched by the activity feed/history builders — a stub returning
    // the empty ranking is enough (tier-season-refresh-and-top-series-order task 4.7).
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
