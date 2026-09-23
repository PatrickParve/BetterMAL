using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// The card's episode total, year span, entry count, and my-progress figures
// (design.md D7, spec "Series card content" / "Series sorting"'s My progress
// row).
public class SeriesListFiguresTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, string airingStatus, int? totalEpisodes = null,
        DateOnly? airedFrom = null, DateOnly? airedTo = null, int? averageEpisodeDurationSeconds = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id,
            Title = $"Anime {id}",
            AiringStatus = airingStatus,
            TotalEpisodes = totalEpisodes,
            AiredFrom = airedFrom,
            AiredTo = airedTo,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
        });

    private static void AddMember(
        AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order,
        int? versionSlotKey = null, int? branchHeadAnimeId = null) =>
        db.SeriesMembers.Add(new SeriesMember
        {
            AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order,
            VersionSlotKey = versionSlotKey, BranchHeadAnimeId = branchHeadAnimeId,
        });

    private static void AddEntry(
        AnimeTrackerDbContext db, int animeId, WatchStatus status, int episodesWatched = 0, int? myScore = null,
        int? rewatchCount = null) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = animeId, Status = status, EpisodesWatched = episodesWatched, MyScore = myScore,
            RewatchCount = rewatchCount ?? 0,
        });

    // A ranked (band 0-2) UserAnimeEntry for AnimeRankingSnapshot.Build, built
    // the same way AnimeRankingSnapshotTests does — independent of the
    // db-backed series fixture above, since RankOf only needs the anime id to
    // match (tasks.md 2.9/2.9a).
    private static UserAnimeEntry RankingEntry(int animeId, int myScore, WatchStatus status = WatchStatus.Completed) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Ranked {animeId}", AiringStatus = "finished_airing" },
            Status = status,
            MyScore = myScore,
        };

    [Fact]
    public async Task EpisodeTotal_KnownCountsSumInFull()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 13);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(25, listed.MainLineEpisodeTotal);
        Assert.False(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task EpisodeTotal_UnknownContributesAiredSoFarAndMarksTheLowerBound()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing", totalEpisodes: null);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 5);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 5 }, AnimeRankingSnapshot.Empty));

        Assert.Equal(17, listed.MainLineEpisodeTotal); // 12 known + 5 aired-so-far
        Assert.True(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task EpisodeTotal_AllUnknownReadsZeroWithTheLowerBoundFlag()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "currently_airing", totalEpisodes: null);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Watching);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // 100 absent from the aired-episodes dictionary too: nothing known at all.
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(0, listed.MainLineEpisodeTotal);
        Assert.True(listed.HasUnknownEpisodeCounts);
    }

    [Fact]
    public async Task YearSpan_SpansEveryMemberIncludingExtras()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", airedFrom: new DateOnly(2013, 4, 1), airedTo: new DateOnly(2013, 9, 1));
        AddAnime(db, 101, "finished_airing", airedFrom: new DateOnly(2022, 1, 1), airedTo: new DateOnly(2023, 3, 1)); // extra
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(2013, listed.FirstYear);
        Assert.Equal(2023, listed.LastYear); // extra's AiredTo, later than the main-line member's
    }

    [Fact]
    public async Task YearSpan_SingleYearWhenEveryEntryAiredInOneYear()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", airedFrom: new DateOnly(2019, 1, 1), airedTo: new DateOnly(2019, 6, 1));
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(2019, listed.FirstYear);
        Assert.Equal(2019, listed.LastYear);
    }

    [Fact]
    public async Task EntryCount_CoversExtras()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "finished_airing");
        AddAnime(db, 102, "finished_airing"); // extra
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddMember(db, 100, 102, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(3, listed.EntryCount);
    }

    [Fact]
    public async Task MyProgressFigures_WatchedAndAiredAreMainLineOnly()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, "currently_airing", totalEpisodes: 24);
        AddAnime(db, 102, "finished_airing", totalEpisodes: 6); // extra — must not count
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddMember(db, 100, 102, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, episodesWatched: 8);
        AddEntry(db, 102, WatchStatus.Completed, episodesWatched: 6);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(new Dictionary<int, int> { [101] = 10 }, AnimeRankingSnapshot.Empty));

        Assert.Equal(20, listed.MainLineWatchedEpisodes); // 12 + 8, extra's 6 excluded
        Assert.Equal(22, listed.MainLineAiredEpisodes); // 12 (finished, full total) + min(10, 24)
    }

    [Fact]
    public async Task AiredCount_NotYetAiredMemberExcluded_ExtrasNeverRaiseIt()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing"); // main line, aired
        AddAnime(db, 101, "not_yet_aired"); // main line, announced sequel — hasn't aired
        AddAnime(db, 102, "finished_airing"); // extra, aired — must not count
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddMember(db, 100, 102, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(1, listed.MainLineAiredCount);
    }

    [Fact]
    public async Task AverageRank_MeanOverRankedMembersOnly()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "finished_airing");
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.Completed, myScore: 7);
        await db.SaveChangesAsync();

        var ranking = AnimeRankingSnapshot.Build([RankingEntry(100, 9), RankingEntry(101, 7)], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], ranking));

        Assert.Equal(1, ranking.RankOf(100));
        Assert.Equal(2, ranking.RankOf(101));
        Assert.Equal(1.5, listed.MainLineAverageRank);
    }

    [Fact]
    public async Task AverageRank_NoRankedMainLineMemberReportsNull()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Watching); // in my list, but unscored — no rank

        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // AnimeRankingSnapshot.Empty covers nothing, matching "no main-line
        // member of this series appears in my rankings".
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Null(listed.MainLineAverageRank);
    }

    [Fact]
    public async Task AverageRank_UnrankedPlanToWatchMemberExcludedFromMean_ButStillCountsTowardMyAverage()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing");
        AddAnime(db, 101, "finished_airing");
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 8);
        AddEntry(db, 101, WatchStatus.PlanToWatch, myScore: 6); // scored but plan-to-watch — unranked
        await db.SaveChangesAsync();

        // 101 is PlanToWatch, so RankBandResolver puts it in Unranked and
        // AnimeRankingSnapshot.Build never assigns it a rank.
        var ranking = AnimeRankingSnapshot.Build(
            [RankingEntry(100, 8), RankingEntry(101, 6, status: WatchStatus.PlanToWatch)], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], ranking));

        Assert.Null(ranking.RankOf(101));
        Assert.Equal(1.0, listed.MainLineAverageRank); // only 100's rank counts toward the mean
        Assert.Equal(7.0, listed.MineMain.Value); // (8 + 6) / 2 — 101's score still counts toward my-average
    }

    [Fact]
    public async Task AverageRank_UnscoredEntryIsExcludedFromTheDivisorNotCountedAsZero()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 200, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 200, "finished_airing"); // ranked 4th
        AddAnime(db, 201, "finished_airing"); // ranked 6th
        AddAnime(db, 202, "finished_airing"); // never scored — no rank
        AddAnime(db, 203, "finished_airing"); // never scored — no rank
        AddMember(db, 200, 200, isMainLine: true, order: 0);
        AddMember(db, 200, 201, isMainLine: true, order: 1);
        AddMember(db, 200, 202, isMainLine: true, order: 2);
        AddMember(db, 200, 203, isMainLine: true, order: 3);
        AddEntry(db, 200, WatchStatus.Completed, myScore: 7);
        AddEntry(db, 201, WatchStatus.Completed, myScore: 5);
        await db.SaveChangesAsync();

        // Three fillers score above 200 and one between 200 and 201, so 200
        // lands at rank 4 and 201 at rank 6 — the exact shape tasks.md 2.9a
        // asks for.
        var ranking = AnimeRankingSnapshot.Build(
            [
                RankingEntry(901, 10), RankingEntry(902, 9), RankingEntry(903, 8),
                RankingEntry(200, 7), RankingEntry(904, 6), RankingEntry(201, 5),
            ], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], ranking));

        Assert.Equal(4, ranking.RankOf(200));
        Assert.Equal(6, ranking.RankOf(201));
        Assert.Equal(5.0, listed.MainLineAverageRank); // (4 + 6) / 2, not (4 + 6 + 0 + 0) / 4
    }

    [Fact]
    public async Task WatchedSeconds_SumsEveryMemberIncludingExtrasAndRewatches()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 1, averageEpisodeDurationSeconds: 7200); // extra
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12, rewatchCount: 1); // first viewing 12 + one rewatch of 12
        AddEntry(db, 101, WatchStatus.Completed, episodesWatched: 1);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(24L * 1500 + 1L * 7200, listed.WatchedSeconds);
    }

    [Fact]
    public async Task WatchedSeconds_ExtraOnlySeriesStillCarriesItsTotal()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12); // main line, not in my list
        AddAnime(db, 101, "finished_airing", totalEpisodes: 1, averageEpisodeDurationSeconds: 7200); // extra
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: false, order: 0);
        AddEntry(db, 101, WatchStatus.Completed, episodesWatched: 1);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        // The browser lists (and totals) this series even though "Most time
        // spent" would omit it — the main-line gate is a profile-only rule.
        Assert.Equal(1L * 7200, listed.WatchedSeconds);
    }

    [Fact]
    public async Task WatchedSeconds_NothingWatchedIsZero()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.PlanToWatch, episodesWatched: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(0, listed.WatchedSeconds);
    }

    // Same slot shape as ANonDefaultAlternativeQualifies (ProfileServiceTime-
    // SpentSeriesTests): rule 1 (watch progress) makes 101 the default since
    // it has more watched. WatchedSeconds still covers 102's episodes; the
    // card-scale MainLineWatchedEpisodes, scoped to the default combination,
    // does not (design.md D4).
    [Fact]
    public async Task WatchedSeconds_CoversANonDefaultAlternative()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing"); // trunk, not in my list
        AddAnime(db, 101, "finished_airing", totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddAnime(db, 102, "finished_airing", totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: true, order: 1, versionSlotKey: 101, branchHeadAnimeId: 101);
        AddMember(db, 100, 102, isMainLine: true, order: 2, versionSlotKey: 101, branchHeadAnimeId: 102);
        AddEntry(db, 101, WatchStatus.Completed, episodesWatched: 12);
        AddEntry(db, 102, WatchStatus.Watching, episodesWatched: 3);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(12L * 1500 + 3L * 1500, listed.WatchedSeconds); // both alternatives' watch time
        Assert.Equal(12, listed.MainLineWatchedEpisodes); // only 101, the default combination
    }

    // Pins design D4's guarantee: ListedSeries() and TimeSpentSeries() read
    // one shared per-series total, computed from the same loaded index, so a
    // series listed on both surfaces can never carry two different figures.
    [Fact]
    public async Task WatchedSeconds_EqualsTheProfilesTimeSpentTotal()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, "finished_airing", totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddAnime(db, 101, "finished_airing", totalEpisodes: 1, averageEpisodeDurationSeconds: 7200); // extra
        AddMember(db, 100, 100, isMainLine: true, order: 0);
        AddMember(db, 100, 101, isMainLine: false, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, episodesWatched: 12, rewatchCount: 1);
        AddEntry(db, 101, WatchStatus.Completed, episodesWatched: 1);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries([], AnimeRankingSnapshot.Empty));
        var timeSpent = Assert.Single(index.TimeSpentSeries());

        Assert.Equal(listed.SeriesId, timeSpent.SeriesId);
        Assert.Equal(listed.WatchedSeconds, timeSpent.WatchedSeconds);
    }
}
