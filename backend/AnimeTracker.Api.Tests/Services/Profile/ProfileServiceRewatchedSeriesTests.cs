using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Profile;

// GetRewatchedSeriesSectionAsync (design.md D8/D9/D10, tasks.md 9.5/11.4):
// "Most rewatched"'s Series scope — a franchise's total rewatch time, summed
// over every member (main line and extras alike), using the same per-entry
// arithmetic WatchMath uses for the profile's own rewatch-inclusive stats.
public class ProfileServiceRewatchedSeriesTests
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
            new FakeEpisodeScheduleService());

    private static void AddSeriesShell(AnimeTrackerDbContext db, int seriesId, int rootAnimeId) =>
        db.Series.Add(new SeriesModel { Id = seriesId, RootAnimeId = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });

    private static void AddMember(
        AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order, string title,
        int? totalEpisodes = null, int? averageEpisodeDurationSeconds = null,
        int? rewatchCount = null, int? episodesWatched = null, WatchStatus status = WatchStatus.Completed)
    {
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = animeId,
            Title = title,
            AiringStatus = "finished_airing",
            TotalEpisodes = totalEpisodes,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
        });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });
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
    public async Task SumsAcrossMainLineAndExtras()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        // Main line: 12 published episodes, 25min each, rewatched once -> 12 * 1500s = 18000s.
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 12);
        // Extra: 1 episode, 120min, rewatched 3 times -> 3 * 1 * 7200s = 21600s.
        AddMember(db, 1, 101, isMainLine: false, order: 1, title: "Extra", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 3, episodesWatched: 1);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(18000L + 21600L, item.RewatchSeconds);
    }

    [Fact]
    public async Task FirstWatchesDoNotCount()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Never Rewatched", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task NoPublishedTotalFallsBackToEpisodesWatched()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Still Airing", totalEpisodes: null,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 8);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(8L * 1500, item.RewatchSeconds);
    }

    [Fact]
    public async Task NoPublishedDurationFallsBackToTheAssumedDuration()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "No Duration", totalEpisodes: 12,
            averageEpisodeDurationSeconds: null, rewatchCount: 1, episodesWatched: 12);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(12L * 24 * 60, item.RewatchSeconds); // the profile's 24-minute assumption
    }

    // Unlike Top series' watched-coverage rule (two aired main-line entries
    // required), a rewatch total is a sum, not an average, so a franchise
    // with only one of three aired main-line entries in my list is still
    // listed here (design.md D9).
    [Fact]
    public async Task FranchiseWithOneOfThreeAiredMainLineEntriesRewatchedIsListed()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Season 1", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 12);
        AddMember(db, 1, 101, isMainLine: true, order: 1, title: "Season 2"); // not in my list
        AddMember(db, 1, 102, isMainLine: true, order: 2, title: "Season 3"); // not in my list
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(100, item.RootAnimeId);
    }

    // --- An in-progress rewatch (design.md D1, tasks.md 2.4) ---

    [Fact]
    public async Task AnInProgressRewatchAddsToASeriesTotal()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        // 12-episode season, rewatch count 2 (24 episodes' worth completed)
        // plus 3 episodes watched of the run in progress -> 27 * 1500s.
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 2, episodesWatched: 3,
            status: WatchStatus.Rewatching);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(27L * 1500, item.RewatchSeconds);
    }

    [Fact]
    public async Task AFirstRewatchInProgressMakesASeriesEligible()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        // Rewatch count still 0 - previously contributed nothing at all.
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 5,
            status: WatchStatus.Rewatching);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.Equal(5L * 1500, item.RewatchSeconds);
    }

    [Fact]
    public async Task ACompletedMemberWithZeroRewatchesStillContributesNothing()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 0, episodesWatched: 12,
            status: WatchStatus.Completed);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task TheAllScopesBadgeIsUnchangedByAnInProgressRewatch()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Main", totalEpisodes: 12,
            averageEpisodeDurationSeconds: 1500, rewatchCount: 1, episodesWatched: 4,
            status: WatchStatus.Rewatching);
        await db.SaveChangesAsync();

        // BuildRewatchedSection reads e.Anime.MediaType directly, unlike the
        // Series-scope path (which goes through SeriesRankingLookup's own
        // join) — needs the navigation included.
        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSectionAsync(TopAnimeMediaTypeScope.All);

        var item = Assert.Single(section.Items);
        Assert.Equal(1, item.RewatchCount); // the completed-runs count only, unaffected by the run in progress
    }

    [Fact]
    public async Task OrderedByTotalDescendingThenTitle()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, title: "Charlie", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 3600, rewatchCount: 1, episodesWatched: 1); // 3600s
        AddSeriesShell(db, 2, 200);
        AddMember(db, 2, 200, isMainLine: true, order: 0, title: "Bravo", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 1, episodesWatched: 1); // 7200s
        AddSeriesShell(db, 3, 300);
        AddMember(db, 3, 300, isMainLine: true, order: 0, title: "Alpha", totalEpisodes: 1,
            averageEpisodeDurationSeconds: 7200, rewatchCount: 1, episodesWatched: 1); // 7200s, ties with Bravo
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetRewatchedSeriesSectionAsync();

        // Alpha and Bravo tie at 7200s -> broken alphabetically; Charlie trails at 3600s.
        Assert.Equal([300, 200, 100], section.Items.Select(i => i.RootAnimeId));
    }

    private sealed class FakeUserAnimeEntryRepository(List<UserAnimeEntry> entries) : IUserAnimeEntryRepository
    {
        public Task<UserAnimeEntry?> GetByAnimeIdAsync(int animeId, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task<List<UserAnimeEntry>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(entries);
        public Task<(int PendingCount, DateTimeOffset? LastSyncedAt)> GetSyncStatusAsync(CancellationToken ct = default) =>
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
}
