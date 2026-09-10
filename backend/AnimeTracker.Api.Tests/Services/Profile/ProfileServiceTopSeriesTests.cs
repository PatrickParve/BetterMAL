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

// GetTopSeriesSectionAsync composes SeriesRankingLookup (tested on its own in
// SeriesRankingLookupTests) with the entry repository to build the Top
// series section's default order — the full my-score chain
// (tier-season-refresh-and-top-series-order design.md D4): my average
// descending, then average ranking position ascending (nulls last), then
// main-line episodes aired descending, then raw title case-insensitively.
// Uses the real AnimeRankingService, over the same entries this test passes
// to the entry repository, so the ranking figures these tests assert on are
// the genuine derived ranking rather than a stand-in for it — which is why
// every entries query below includes the Anime navigation:
// RankBandResolver reads it directly, and a bare AsNoTracking() projection
// would leave it null.
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
            new FakeEpisodeScheduleService(),
            new AnimeRankingService(new FakeUserAnimeEntryRepository(entries), new FakeTopAnimeSelectionRepository()));

    // seriesId is the root entry's own anime id (key-series-by-root-anime-id
    // design.md D1) — rootAnimeId is both the series' Id and its sole member.
    private static void AddSeries(
        AnimeTrackerDbContext db, int rootAnimeId, string title, int myScore, string airingStatus = "finished_airing")
    {
        db.Series.Add(new SeriesModel { Id = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = rootAnimeId, Title = title, MalScore = 7.0, AiringStatus = airingStatus });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = rootAnimeId, SeriesId = rootAnimeId, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = rootAnimeId, Status = WatchStatus.Completed, MyScore = myScore });
    }

    // Multi-member variant for the watched-coverage tests below: one Series
    // row plus per-member airing status and (optional) list membership, so a
    // franchise's main line can be built up entry by entry.
    private static void AddSeriesShell(AnimeTrackerDbContext db, int rootAnimeId) =>
        db.Series.Add(new SeriesModel { Id = rootAnimeId, BuiltAt = DateTimeOffset.UtcNow });

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
        AddSeries(db, 100, "Bravo", myScore: 8);
        AddSeries(db, 200, "Alpha", myScore: 9);
        AddSeries(db, 300, "Charlie", myScore: 7);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([200, 100, 300], section.Items.Select(i => i.SeriesId));
    }

    // The four tie-break steps of the full my-score chain
    // (tier-season-refresh-and-top-series-order design.md D4/task 4.8),
    // exercised in order: average ranking position, then whether a position
    // exists at all, then main-line episodes aired, then title. Scored
    // main-line count — the old tie-break — no longer enters the my-score
    // ordering at all; it now belongs to the MAL basis alone (see
    // MalBasisTieBreakFigureIsUnaffectedByMyScoreOrdering below).

    [Fact]
    public async Task BaseOrdering_TiedAverageBrokenByAverageRankingPositionAscending()
    {
        using var db = CreateDb();
        // Series A: one main-line member scored 8.
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Series A", AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 100, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 100, Status = WatchStatus.Completed, MyScore = 8 });

        // Series B: two main-line members scored 9 and 7 -> same average (8),
        // tied with series A, but its entries sit at overall ranks 1 and 4
        // (a filler entry, not part of any series, takes rank 3 ahead of the
        // score-7 member on title), for an average position of 2.5 against
        // series A's own rank of 2.
        db.Series.Add(new SeriesModel { Id = 200, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 200, Title = "Series B One", AiringStatus = "finished_airing" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 201, Title = "Series B Two", AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 200, SeriesId = 200, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 201, SeriesId = 200, IsMainLine = true, Order = 1 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 200, Status = WatchStatus.Completed, MyScore = 9 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 201, Status = WatchStatus.Completed, MyScore = 7 });

        // Filler: not a member of any series, only present to skew the
        // within-score-7 title tie-break ahead of series B's own entry.
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 999, Title = "AAA Filler", AiringStatus = "finished_airing" });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 999, Status = WatchStatus.Completed, MyScore = 7 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([100, 200], section.Items.Select(i => i.SeriesId));
    }

    [Fact]
    public async Task BaseOrdering_TiedSeriesWithNoRankedEntrySortsAfterOneThatHasAPosition()
    {
        using var db = CreateDb();
        // Series C: scored 8, Completed -> hand-ordered, so it holds a rank.
        db.Series.Add(new SeriesModel { Id = 300, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 300, Title = "Series C", AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 300, SeriesId = 300, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 300, Status = WatchStatus.Completed, MyScore = 8 });

        // Series D: same average (8) but PlanToWatch -> Unranked band, so it
        // holds no position at all despite carrying a my-score.
        db.Series.Add(new SeriesModel { Id = 400, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 400, Title = "Series D", AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 400, SeriesId = 400, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 400, Status = WatchStatus.PlanToWatch, MyScore = 8 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([300, 400], section.Items.Select(i => i.SeriesId));
        Assert.NotNull(section.Items.Single(i => i.SeriesId == 300).MainLineAverageRank);
        Assert.Null(section.Items.Single(i => i.SeriesId == 400).MainLineAverageRank);
    }

    [Fact]
    public async Task BaseOrdering_StillTiedFallsToMainLineEpisodesAiredDescending()
    {
        using var db = CreateDb();
        // Both series tie on average (8) and on average rank (both
        // PlanToWatch -> Unranked -> null), so the tie falls to main-line
        // episodes aired: series E's 24 beats series F's 12.
        db.Series.Add(new SeriesModel { Id = 500, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 500, Title = "Series E", AiringStatus = "finished_airing", TotalEpisodes = 24 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 500, SeriesId = 500, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 500, Status = WatchStatus.PlanToWatch, MyScore = 8 });

        db.Series.Add(new SeriesModel { Id = 600, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 600, Title = "Series F", AiringStatus = "finished_airing", TotalEpisodes = 12 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 600, SeriesId = 600, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 600, Status = WatchStatus.PlanToWatch, MyScore = 8 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([500, 600], section.Items.Select(i => i.SeriesId));
    }

    [Fact]
    public async Task BaseOrdering_StillTiedFallsToTitle()
    {
        using var db = CreateDb();
        // Tied on average (8), on average rank (both null), and on
        // main-line episodes aired (both 12) -> falls to display title.
        db.Series.Add(new SeriesModel { Id = 700, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 700, Title = "Zulu", AiringStatus = "finished_airing", TotalEpisodes = 12 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 700, SeriesId = 700, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 700, Status = WatchStatus.PlanToWatch, MyScore = 8 });

        db.Series.Add(new SeriesModel { Id = 800, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 800, Title = "Alpha", AiringStatus = "finished_airing", TotalEpisodes = 12 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 800, SeriesId = 800, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 800, Status = WatchStatus.PlanToWatch, MyScore = 8 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Equal([800, 700], section.Items.Select(i => i.SeriesId));
    }

    // The MAL basis's own tie-break — scored main-line count — is unchanged
    // (design.md non-goal); the response still carries the figure it needs
    // (MalMain.ScoredCount) even though the server's own default order is
    // the my-score chain, not the MAL one (task 4.9).
    [Fact]
    public async Task MalBasisTieBreakFigureIsUnaffectedByMyScoreOrdering()
    {
        using var db = CreateDb();
        // Series K: one MAL-scored main-line member, higher my-score.
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Series K", MalScore = 7.0, AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 100, IsMainLine = true, Order = 0 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 100, Status = WatchStatus.Completed, MyScore = 9 });

        // Series L: two MAL-scored main-line members, same MAL average (7)
        // but a larger scored count, and a lower my-score than series K.
        db.Series.Add(new SeriesModel { Id = 200, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 200, Title = "Series L One", MalScore = 7.0, AiringStatus = "finished_airing" });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 201, Title = "Series L Two", MalScore = 7.0, AiringStatus = "finished_airing" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 200, SeriesId = 200, IsMainLine = true, Order = 0 });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 201, SeriesId = 200, IsMainLine = true, Order = 1 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 200, Status = WatchStatus.Completed, MyScore = 6 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 201, Status = WatchStatus.Completed, MyScore = 6 });
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        // The response is in my-score order (9 > 6) — not by MAL scored count.
        Assert.Equal([100, 200], section.Items.Select(i => i.SeriesId));

        var seriesK = section.Items.Single(i => i.SeriesId == 100);
        var seriesL = section.Items.Single(i => i.SeriesId == 200);
        Assert.Equal(7.0, seriesK.MalMain.Value);
        Assert.Equal(1, seriesK.MalMain.ScoredCount);
        Assert.Equal(7.0, seriesL.MalMain.Value);
        Assert.Equal(2, seriesL.MalMain.ScoredCount); // still derivable for the MAL basis's own client-side tie-break
    }

    [Fact]
    public async Task SeriesWithNoListMemberIsExcluded()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        db.AnimeMetadata.Add(new AnimeMetadata { Id = 100, Title = "Not In My List" });
        db.SeriesMembers.Add(new SeriesMember { AnimeId = 100, SeriesId = 100, IsMainLine = true, Order = 0 });
        await db.SaveChangesAsync();

        var section = await CreateService(db, []).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task BothAveragesArePresentOnEveryItem()
    {
        using var db = CreateDb();
        AddSeries(db, 100, "Only Series", myScore: 8);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
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
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddMember(db, 100, 102, isMainLine: true, order: 2);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_ThreeAiredMainLineTwoInList_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 100, 101, isMainLine: true, order: 1, status: WatchStatus.Completed, myScore: 7);
        AddMember(db, 100, 102, isMainLine: true, order: 2);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_OneAiredPlusUnairedSequelBothCounted_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 100, 101, isMainLine: true, order: 1, airingStatus: "not_yet_aired");
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_TwoAiredOneCompletedOnePlanToWatch_Included()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0, status: WatchStatus.Completed, myScore: 8);
        AddMember(db, 100, 101, isMainLine: true, order: 1, status: WatchStatus.PlanToWatch, myScore: null);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Single(section.Items);
    }

    [Fact]
    public async Task WatchedCoverage_TwoAiredMainLineOnlyAnExtraInList_Excluded()
    {
        using var db = CreateDb();
        AddSeriesShell(db, 100);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddMember(db, 100, 102, isMainLine: false, order: 2, status: WatchStatus.Completed, myScore: 6);
        await db.SaveChangesAsync();

        var entries = await db.UserAnimeEntries.AsNoTracking().Include(e => e.Anime).ToListAsync();
        var section = await CreateService(db, entries).GetTopSeriesSectionAsync();

        Assert.Empty(section.Items);
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

    // GetOrderedAnimeIdsAsync now backs the real AnimeRankingService the
    // ordering tests below construct alongside ProfileService (task 4.7), so
    // it returns an empty stored order (title-only tie-break within a score
    // band) rather than throwing.
    private sealed class FakeTopAnimeSelectionRepository : ITopAnimeSelectionRepository
    {
        public Task<List<int>> GetOrderedAnimeIdsAsync(CancellationToken ct = default) => Task.FromResult(new List<int>());
        public Task ReplaceOrderAsync(IReadOnlyList<int> orderedAnimeIds, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task ReplaceAllAsync(IReadOnlyList<int> animeIds, DateTimeOffset modifiedAt, CancellationToken ct = default) =>
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
