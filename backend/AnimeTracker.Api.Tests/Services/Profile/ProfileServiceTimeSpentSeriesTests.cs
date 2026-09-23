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

// GetTimeSpentSeriesSectionAsync (add-time-spent-and-trim-empty-scopes
// design.md D7/D8, tasks.md 5.4-5.6): "Most time spent" — a franchise's total
// watch time, first viewings plus rewatches, summed over every member (main
// line and extras alike), using the same per-entry arithmetic WatchMath uses
// for the profile's own Days stat.
public class ProfileServiceTimeSpentSeriesTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ProfileService CreateService(AnimeTrackerDbContext db, List<UserAnimeEntry> entries) =>
        new(
            new FakeUserAnimeEntryRepository(entries),
            new FakeActivityLogRepository(),
            new FakeTopAnimeSelectionRepository(),
            new SeriesRankingLookup(db),
            new FakeSeriesBuildTrigger(),
            new FakeEpisodeScheduleService(),
            new UnusedAnimeRankingService());

    private static void AddSeriesShell(AnimeTrackerDbContext db, int rootAnimeId) =>
        db.Series.Add(new SeriesModel { Id = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });

    private static void AddMember(
        AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order, string title,
        int? totalEpisodes = null, int? averageEpisodeDurationSeconds = null,
        int? rewatchCount = null, int? episodesWatched = null, WatchStatus status = WatchStatus.Completed,
        string airingStatus = "finished_airing", double? malScore = null, int? versionSlotKey = null,
        int? branchHeadAnimeId = null, string membershipKind = "Core")
    {
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = animeId,
            Title = title,
            AiringStatus = airingStatus,
            TotalEpisodes = totalEpisodes,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
            MalScore = malScore,
        });
        db.SeriesMembers.Add(new SeriesMember
        {
            AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order,
            VersionSlotKey = versionSlotKey, BranchHeadAnimeId = branchHeadAnimeId, MembershipKind = membershipKind,
        });
        if (rewatchCount is not null || episodesWatched is not null)
        {
            db.UserAnimeEntries.Add(new UserAnimeEntry
            {
                AnimeId = animeId,
                Status = status,
                RewatchCount = rewatchCount ?? 0,
                EpisodesWatched = episodesWatched ?? 0,
            });
        }
    }

    [Fact]
    public async Task AMainLineMembersFirstViewingCounts()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(12L * 1500, item.WatchedSeconds);
    }

    [Fact]
    public async Task AnExtraCounts()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 1);
        AddMember(db, 100, 101, isMainLine: false, order: 1, title: "Extra", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 0, episodesWatched: 1);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        // Main line's one watched episode plus the extra's full runtime — the
        // extra still counts once the series qualifies through the main line.
        Assert.Equal(1L * 1500 + 1L * 7200, item.WatchedSeconds);
    }

    [Fact]
    public async Task AFranchiseWithOnlyAnExtraWatchedIsOmitted()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main"); // not in my list
        AddMember(db, 100, 101, isMainLine: false, order: 1, title: "Extra", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 0, episodesWatched: 1);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task APlanToWatchMainLineWithAWatchedExtraIsOmitted()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 0,
            status: WatchStatus.PlanToWatch);
        AddMember(db, 100, 101, isMainLine: false, order: 1, title: "Extra", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 0, episodesWatched: 1);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task ADroppedMainLineEntryWithOneEpisodeQualifies()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 1,
            status: WatchStatus.Dropped);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(1L * 1500, item.WatchedSeconds);
    }

    [Fact]
    public async Task ARewatchingMainLineEntryWithNoNewEpisodesQualifies()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 0,
            status: WatchStatus.Rewatching);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(12L * 1500, item.WatchedSeconds); // first viewing counts as one full run
    }

    // Design D2's edge case: rule 1 (watch progress) sees nothing watched in
    // either branch since 102's episodes-watched has reset to 0 for its
    // in-progress rewatch, so rule 2 (MAL score) makes 101 the default. The
    // gate and the total still see 102's first viewing, since every
    // main-line member counts, not only the default combination.
    [Fact]
    public async Task ANonDefaultAlternativeQualifies()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Trunk"); // BranchHeadAnimeId null, not in my list
        AddMember(db, 100, 101, isMainLine: true, order: 1, title: "Alternative A",
            malScore: 9.0, versionSlotKey: 101, branchHeadAnimeId: 101); // not in my list
        AddMember(db, 100, 102, isMainLine: true, order: 2, title: "Alternative B", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 0,
            status: WatchStatus.Rewatching, malScore: 7.0, versionSlotKey: 101, branchHeadAnimeId: 102);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(12L * 1500, item.WatchedSeconds); // 102's full first run, though 101 is the default alternative
    }

    [Fact]
    public async Task AVersionNeighbourAloneDoesNotQualify()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main"); // not in my list
        AddMember(db, 100, 101, isMainLine: false, order: 1, title: "Neighbour", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12,
            membershipKind: "NeighbourTelling");
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task RewatchesAddOnTop()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        // First viewing: 12 episodes. One completed rewatch adds another 12.
        // 24 episodes total * 1500s.
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 12);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(24L * 1500, item.WatchedSeconds);
    }

    [Fact]
    public async Task AMemberNotInMyListContributesNothing()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Watched", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12);
        AddMember(db, 100, 101, isMainLine: true, order: 1, title: "Not In My List"); // no entry at all
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(12L * 1500, item.WatchedSeconds); // only the listed member's contribution
    }

    [Fact]
    public async Task APartiallyWatchedCurrentlyAiringMemberContributesItsWatchedEpisodes()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Still Airing", totalEpisodes: null,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 5,
            status: WatchStatus.Watching, airingStatus: "currently_airing");
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(5L * 1500, item.WatchedSeconds); // only what's aired and been watched so far
    }

    [Fact]
    public async Task ARewatchingMemberContributesOneFullRunPlusInProgressEpisodes()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        // First viewing counts as one full run (12, since Rewatching), plus
        // one completed rewatch (12) plus 3 episodes of the run in progress:
        // 27 episodes * 1500s.
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 3,
            status: WatchStatus.Rewatching);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(27L * 1500, item.WatchedSeconds);
    }

    [Fact]
    public async Task AFranchiseWithNothingWatchedIsOmittedRatherThanListedAtZero()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Plan To Watch", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 0,
            status: WatchStatus.PlanToWatch);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task AFranchiseWithOneWatchedEntryOutOfManyIsListed()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Watched", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12);
        AddMember(db, 100, 101, isMainLine: true, order: 1, title: "Plan To Watch", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 0, status: WatchStatus.PlanToWatch);
        AddMember(db, 100, 102, isMainLine: true, order: 2, title: "Not In My List");
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(100, item.SeriesId);
        Assert.Equal(12L * 1500, item.WatchedSeconds);
    }

    [Fact]
    public async Task OrderedByTotalDescendingThenTitle()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Charlie", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 3600, rewatchCount: 0, episodesWatched: 1); // 3600s
        AddSeriesShell(db, 200);
        AddMember(db, 200, 200, isMainLine: true, order: 0, title: "Bravo", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 0, episodesWatched: 1); // 7200s
        AddSeriesShell(db, 300);
        AddMember(db, 300, 300, isMainLine: true, order: 0, title: "Alpha", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 0, episodesWatched: 1); // 7200s, ties with Bravo
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTimeSpentSeriesSectionAsync();

        // Alpha and Bravo tie at 7200s -> broken alphabetically; Charlie trails at 3600s.
        Assert.Equal([300, 200, 100], section.Items.Select(i => i.SeriesId));
    }

    // Pins design D7's guarantee: the franchise total and the profile's own
    // Days stat are wired to the same arithmetic (same episodes, same
    // duration), not to two separately-maintained copies of it.
    [Fact]
    public async Task ASingleEntryFranchisesWatchedSecondsEqualsItsContributionToTheDaysStat()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 12);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var service = CreateService(db, entries);

        var section = await service.GetTimeSpentSeriesSectionAsync();
        var profile = await service.GetProfileAsync();

        // 12 first-viewing episodes + 12 rewatch episodes, 1500s each.
        var expectedSeconds = 24L * 1500;
        var item = Assert.Single(section.Items);
        Assert.Equal(expectedSeconds, item.WatchedSeconds);
        Assert.Equal(Math.Round(expectedSeconds / 86400.0, 1), profile.Stats.Days);
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
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetSinceAsync(DateTimeOffset cutoffUtc, int maxRows, CancellationToken ct = default) =>
            Task.FromResult(new List<ActivityLog>());
        public Task<List<ActivityLog>> GetAllAsync(CancellationToken ct = default) => throw new NotImplementedException();
        public Task<List<ActivityLog>> GetEpisodeProgressInRangeAsync(DateTimeOffset fromUtc, DateTimeOffset toUtc, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<ActivityLog>> GetAllOldestFirstAsync(CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) =>
            Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<DateTimeOffset?> GetModifiedAtAsync(CancellationToken ct = default) => throw new NotImplementedException();
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

    // Not touched by GetTimeSpentSeriesSectionAsync or BuildStats — a stub
    // returning the empty ranking is enough (tier-season-refresh-and-top-
    // series-order task 4.7).
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
