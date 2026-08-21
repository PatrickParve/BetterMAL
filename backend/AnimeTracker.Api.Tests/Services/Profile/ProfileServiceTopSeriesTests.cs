using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Airing;
using AnimeTracker.Api.Services.Profile;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Profile;

// GetTopSeriesSectionAsync composes SeriesRankingLookup (tested on its own in
// SeriesRankingLookupTests) with the entry repository to build the Top
// series section's base ordering (design.md decision 3/task 3.3) — my
// average descending, then scored main-line count descending, then raw
// title case-insensitively.
public class ProfileServiceTopSeriesTests
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

    private static void AddSeries(
        AnimeTrackerDbContext db, int seriesId, int rootAnimeId, string title, int myScore, string airingStatus = "finished_airing")
    {
        db.Series.Add(new SeriesModel { Id = seriesId, RootAnimeId = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = rootAnimeId, Title = title, MalScore = 7.0, AiringStatus = airingStatus });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = rootAnimeId, SeriesId = seriesId, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = rootAnimeId, Status = WatchStatus.Completed, MyScore = myScore });
    }

    // Multi-member variant for the watched-coverage tests below: one Series
    // row plus per-member airing status and (optional) list membership, so a
    // franchise's main line can be built up entry by entry.
    private static void AddSeriesShell(AnimeTrackerDbContext db, int seriesId, int rootAnimeId) =>
        db.Series.Add(new SeriesModel { Id = seriesId, RootAnimeId = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });

    private static void AddMember(
        AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order,
        string airingStatus = "finished_airing", WatchStatus? status = null, int? myScore = null)
    {
        db.AnimeMetadata.Add(new AnimeMetadata { Id = animeId, Title = $"Anime {animeId}", MalScore = 7.0, AiringStatus = airingStatus });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });
        if (status is not null)
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status.Value, MyScore = myScore });
    }

    [Fact]
    public async Task BaseOrdering_MyAverageDescendingThenTitle()
    {
        using var db = CreateDb();
        AddSeries(db, 1, 100, "Bravo", myScore: 8);
        AddSeries(db, 2, 200, "Alpha", myScore: 9);
        AddSeries(db, 3, 300, "Charlie", myScore: 7);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([200, 100, 300], section.Items.Select(i => i.RootAnimeId));
    }

    [Fact]
    public async Task BaseOrdering_TiedAverageBrokenByScoredMainLineCountDescending()
    {
        using var db = CreateDb();
        // Series 1: one scored main-line member at 8.
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "One Season" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 1, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 100, Status = WatchStatus.Completed, MyScore = 8 });

        // Series 2: two scored main-line members, both 8 -> same average, more scored entries.
        db.Series.Add(new SeriesModel { Id = 2, RootAnimeId = 200, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 200, Title = "Two Seasons" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 201, Title = "Two Seasons S2" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 200, SeriesId = 2, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 201, SeriesId = 2, IsMainLine = true, Order = 1 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 200, Status = WatchStatus.Completed, MyScore = 8 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 201, Status = WatchStatus.Completed, MyScore = 8 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([200, 100], section.Items.Select(i => i.RootAnimeId));
    }

    [Fact]
    public async Task SeriesWithNoListMemberIsExcluded()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Not In My List" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 1, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();

        var section = await CreateService(db, []).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task BothAveragesArePresentOnEveryItem()
    {
        using var db = CreateDb();
        AddSeries(db, 1, 100, "Only Series", myScore: 8);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        var item = Assert.Single(section.Items);
        Assert.NotNull(item.MalMain);
        Assert.NotNull(item.MineMain);
        Assert.Equal(8.0, item.MineMain.Value);
        Assert.Equal(7.0, item.MalMain.Value);
    }

    // Watched-coverage rule (design.md decisions 6/7, task 6.1): a franchise
    // whose main line has two or more aired entries needs at least two of
    // them in my list, any status, or it's excluded from Top series.
    [Fact]
    public async Task WatchedCoverage_ThreeAiredMainLineOneInList_Excluded()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: true, order: 2);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_ThreeAiredMainLineTwoInList_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 1, 101, isMainLine: true, order: 1, status: WatchStatus.Completed, myScore: 7);
        AddMember(db, 1, 102, isMainLine: true, order: 2);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_OneAiredPlusUnairedSequelBothCounted_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 1, 101, isMainLine: true, order: 1, airingStatus: "not_yet_aired");
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_TwoAiredOneCompletedOnePlanToWatch_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 1, 101, isMainLine: true, order: 1, status: WatchStatus.PlanToWatch, myScore: null);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_TwoAiredMainLineOnlyAnExtraInList_Excluded()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 1, 100);
        AddMember(db, 1, 100, isMainLine: true, order: 0);
        AddMember(db, 1, 101, isMainLine: true, order: 1);
        AddMember(db, 1, 102, isMainLine: false, order: 2, status: WatchStatus.Completed, myScore: 6);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
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
    }
}
