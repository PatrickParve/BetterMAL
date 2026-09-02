using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Ranking;
using AnimeTracker.Api.Services.Series;
using Microsoft.EntityFrameworkCore;
using SeriesModel = AnimeTracker.Api.Models.Series;

namespace AnimeTracker.Api.Tests.Services.Series;

// SeriesRankingLookup loads the SeriesMembers ⋈ AnimeMetadata ⋈ UserEntry
// projection Top series ranks over; SeriesRankingIndex then computes
// eligibility, the two main-series averages, and the MAL reveal rule
// in-memory (design.md decision 1/2/5, task 2.1-2.5, task 12.2).
public class SeriesRankingLookupTests
{
    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static void AddAnime(
        AnimeTrackerDbContext db, int id, double? malScore = null, string airingStatus = "finished_airing",
        int? totalEpisodes = null, int? averageEpisodeDurationSeconds = null) =>
        db.AnimeMetadata.Add(new AnimeMetadata
        {
            Id = id,
            Title = $"Anime {id}",
            MalScore = malScore,
            AiringStatus = airingStatus,
            TotalEpisodes = totalEpisodes,
            AverageEpisodeDurationSeconds = averageEpisodeDurationSeconds,
        });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });

    private static void AddEntry(
        AnimeTrackerDbContext db, int animeId, WatchStatus status, int? myScore,
        int rewatchCount = 0, int episodesWatched = 0) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry
        {
            AnimeId = animeId,
            Status = status,
            MyScore = myScore,
            RewatchCount = rewatchCount,
            EpisodesWatched = episodesWatched,
        });

    // A ranked (band 0-2) UserAnimeEntry for AnimeRankingSnapshot.Build,
    // independent of the db-backed series fixture above — RankOf only needs
    // the anime id to match (mirrors SeriesListFiguresTests.RankingEntry).
    private static UserAnimeEntry RankingEntry(int animeId, int myScore, WatchStatus status = WatchStatus.Completed) =>
        new()
        {
            AnimeId = animeId,
            Anime = new AnimeMetadata { Id = animeId, Title = $"Ranked {animeId}", AiringStatus = "finished_airing" },
            Status = status,
            MyScore = myScore,
        };

    [Fact]
    public async Task EmptyStore_ReturnsEmptyResultWithoutSeries()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no Series rows at all -> the cheap empty path

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.EligibleSeries([], AnimeRankingSnapshot.Empty));
        Assert.False(index.HasSeries(1));
    }

    [Fact]
    public async Task Averages_OnlyReflectMainLineMembers()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0);
        AddAnime(db, 101, malScore: 6.0); // extra — must not affect the main-line average
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: false, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.Completed, myScore: 1);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(8.0, series.MalMain.Value);
        Assert.Equal(9.0, series.MineMain.Value);
        Assert.Equal(2, series.EntryCount); // total membership still counts the extra
    }

    [Fact]
    public async Task EligibleThroughAnExtraOnlyListEntry()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0);
        AddAnime(db, 101, malScore: 6.0);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: false, order: 1);
        // Only the extra is in my list — no main-line entry at all.
        AddEntry(db, 101, WatchStatus.Completed, myScore: 7);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(1, series.SeriesId);
        Assert.Null(series.MineMain.Value); // averages are still main-line only
        Assert.Equal(0, series.MineMain.ScoredCount);
    }

    [Fact]
    public async Task IneligibleWhenNoMemberIsInMyList()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.EligibleSeries([], AnimeRankingSnapshot.Empty));
    }

    [Fact]
    public async Task MalRevealed_TrueWhenMainLineCompletedAndNothingAiring()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0, airingStatus: "finished_airing");
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.True(series.MalRevealed);
    }

    [Fact]
    public async Task MalRevealed_FalseWhenAMainLineMemberIsCurrentlyAiring()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0, airingStatus: "finished_airing");
        AddAnime(db, 101, malScore: 7.5, airingStatus: "currently_airing");
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.Watching, myScore: null);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.False(series.MalRevealed);
    }

    [Fact]
    public async Task MainLineAiredCount_ExcludesNotYetAiredMainLineMembers()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0, airingStatus: "finished_airing");
        AddAnime(db, 101, airingStatus: "not_yet_aired"); // announced season 2, zero episodes out
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.PlanToWatch, myScore: null);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.Equal(2, series.EntryCount); // total membership still counts the not-yet-aired entry
        Assert.Equal(1, series.MainLineAiredCount); // but it doesn't count as an aired main-line entry
    }

    // The four columns added for the rewatched-series read (design.md
    // D9/D10, task 9.1/11.5) aren't exposed on SeriesRankingResult directly,
    // so their presence is observed through RewatchedSeries()'s output —
    // a non-zero, correctly-computed total confirms RewatchCount,
    // EpisodesWatched, TotalEpisodes, and AverageEpisodeDurationSeconds all
    // flowed through the join.
    [Fact]
    public async Task RewatchProjection_PopulatesTheFourNewColumns()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9, rewatchCount: 1, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var rewatched = Assert.Single(index.RewatchedSeries());

        Assert.Equal(12L * 1500, rewatched.RewatchSeconds);
    }

    // A member with no UserEntry at all has null RewatchCount/EpisodesWatched
    // in the projection (the same null-guard MyScore/EntryStatus already
    // use) — it must contribute nothing rather than throwing.
    [Fact]
    public async Task RewatchProjection_MemberWithNoUserEntryContributesNothing()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500);
        AddAnime(db, 101, totalEpisodes: 12, averageEpisodeDurationSeconds: 1500); // never in my list at all
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9, rewatchCount: 1, episodesWatched: 12);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        var rewatched = Assert.Single(index.RewatchedSeries());

        Assert.Equal(12L * 1500, rewatched.RewatchSeconds); // only anime 100's rewatch counts
    }

    [Fact]
    public async Task HasSeries_ReflectsStoredMembership()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.True(index.HasSeries(100));
        Assert.False(index.HasSeries(999));
    }

    // EligibleSeries' two new figures (tier-season-refresh-and-top-series-
    // order design.md D1/D2, task 3.7) — mirroring the ListedSeries figure
    // tests in SeriesListFiguresTests.cs, since both read the same shared
    // helpers.
    [Fact]
    public async Task AverageRank_MeanOverRankedMainLineMembersOnly()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 9.0);
        AddAnime(db, 101, malScore: 7.0);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.Completed, myScore: 7);
        await db.SaveChangesAsync();

        var ranking = AnimeRankingSnapshot.Build([RankingEntry(100, 9), RankingEntry(101, 7)], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], ranking));

        Assert.Equal(1, ranking.RankOf(100));
        Assert.Equal(2, ranking.RankOf(101));
        Assert.Equal(1.5, series.MainLineAverageRank);
    }

    [Fact]
    public async Task AverageRank_NoRankedMainLineMemberReportsNull()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 8.0);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddEntry(db, 100, WatchStatus.Watching, myScore: null); // in my list, unscored — no rank
        await db.SaveChangesAsync();

        var index = await new SeriesRankingLookup(db).LoadAsync();
        // AnimeRankingSnapshot.Empty covers nothing, matching "no main-line
        // member of this series appears in my rankings".
        var series = Assert.Single(index.EligibleSeries([], AnimeRankingSnapshot.Empty));

        Assert.Null(series.MainLineAverageRank);
    }

    [Fact]
    public async Task AverageRank_UnrankedMembersExcludedFromTheDivisorNotCountedAsZero()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 200, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 200); // ranked 4th
        AddAnime(db, 201); // ranked 6th
        AddAnime(db, 202); // never scored — no rank
        AddAnime(db, 203); // never scored — no rank
        AddMember(db, seriesId: 1, animeId: 200, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 201, isMainLine: true, order: 1);
        AddMember(db, seriesId: 1, animeId: 202, isMainLine: true, order: 2);
        AddMember(db, seriesId: 1, animeId: 203, isMainLine: true, order: 3);
        AddEntry(db, 200, WatchStatus.Completed, myScore: 7);
        AddEntry(db, 201, WatchStatus.Completed, myScore: 5);
        await db.SaveChangesAsync();

        // Three fillers score above 200 and one between 200 and 201, so 200
        // lands at rank 4 and 201 at rank 6 — the exact shape
        // SeriesListFiguresTests' equivalent test uses.
        var ranking = AnimeRankingSnapshot.Build(
            [
                RankingEntry(901, 10), RankingEntry(902, 9), RankingEntry(903, 8),
                RankingEntry(200, 7), RankingEntry(904, 6), RankingEntry(201, 5),
            ], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], ranking));

        Assert.Equal(4, ranking.RankOf(200));
        Assert.Equal(6, ranking.RankOf(201));
        Assert.Equal(5.0, series.MainLineAverageRank); // (4 + 6) / 2, not (4 + 6 + 0 + 0) / 4
    }

    [Fact]
    public async Task AverageRank_ExtraNeverEntersTheMean()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, malScore: 9.0); // main line — ranked 2nd
        AddAnime(db, 101, malScore: 9.5); // extra — ranked 1st, must not lower the average
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: false, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: 9);
        AddEntry(db, 101, WatchStatus.Completed, myScore: 10);
        await db.SaveChangesAsync();

        var ranking = AnimeRankingSnapshot.Build([RankingEntry(101, 10), RankingEntry(100, 9)], []);
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var series = Assert.Single(index.EligibleSeries([], ranking));

        Assert.Equal(1, ranking.RankOf(101));
        Assert.Equal(2, ranking.RankOf(100));
        Assert.Equal(2.0, series.MainLineAverageRank); // only the main-line member (100) counts
    }

    [Fact]
    public async Task MainLineAiredEpisodes_MatchesListedSeriesForTheSameMembers()
    {
        using var db = CreateDb();
        db.Series.Add(new SeriesModel { Id = 1, RootAnimeId = 100, BuiltAt = DateTimeOffset.UtcNow });
        AddAnime(db, 100, airingStatus: "finished_airing", totalEpisodes: 12);
        AddAnime(db, 101, airingStatus: "currently_airing", totalEpisodes: 24);
        AddMember(db, seriesId: 1, animeId: 100, isMainLine: true, order: 0);
        AddMember(db, seriesId: 1, animeId: 101, isMainLine: true, order: 1);
        AddEntry(db, 100, WatchStatus.Completed, myScore: null, episodesWatched: 12);
        AddEntry(db, 101, WatchStatus.Watching, myScore: null, episodesWatched: 8);
        await db.SaveChangesAsync();

        var airedEpisodesByAnimeId = new Dictionary<int, int> { [101] = 10 };
        var index = await new SeriesRankingLookup(db).LoadAsync();
        var listed = Assert.Single(index.ListedSeries(airedEpisodesByAnimeId, AnimeRankingSnapshot.Empty));
        var eligible = Assert.Single(index.EligibleSeries(airedEpisodesByAnimeId, AnimeRankingSnapshot.Empty));

        Assert.Equal(22, listed.MainLineAiredEpisodes); // 12 (finished, full total) + min(10, 24)
        Assert.Equal(listed.MainLineAiredEpisodes, eligible.MainLineAiredEpisodes);
    }
}
