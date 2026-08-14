using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
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
            new FakeSeriesBuildTrigger());

    private static void AddSeries(
        AnimeTrackerDbContext db, int seriesId, int rootAnimeId, string title, int myScore, string airingStatus = "finished_airing")
    {
        db.Series.Add(new SeriesModel { Id = seriesId, RootAnimeId = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = rootAnimeId, Title = title, MalScore = 7.0, AiringStatus = airingStatus });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = rootAnimeId, SeriesId = seriesId, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = rootAnimeId, Status = WatchStatus.Completed, MyScore = myScore });
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
}
