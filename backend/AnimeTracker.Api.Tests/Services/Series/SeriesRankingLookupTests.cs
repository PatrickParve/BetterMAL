using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
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
        AnimeTrackerDbContext db, int id, double? malScore = null, string airingStatus = "finished_airing") =>
        db.AnimeMetadata.Add(new AnimeMetadata { Id = id, Title = $"Anime {id}", MalScore = malScore, AiringStatus = airingStatus });

    private static void AddMember(AnimeTrackerDbContext db, int seriesId, int animeId, bool isMainLine, int order) =>
        db.SeriesMembers.Add(new SeriesMember { AnimeId = animeId, SeriesId = seriesId, IsMainLine = isMainLine, Order = order });

    private static void AddEntry(AnimeTrackerDbContext db, int animeId, WatchStatus status, int? myScore) =>
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = animeId, Status = status, MyScore = myScore });

    [Fact]
    public async Task EmptyStore_ReturnsEmptyResultWithoutSeries()
    {
        using var db = CreateDb();
        await db.SaveChangesAsync(); // no Series rows at all -> the cheap empty path

        var index = await new SeriesRankingLookup(db).LoadAsync();

        Assert.Empty(index.EligibleSeries());
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
        var series = Assert.Single(index.EligibleSeries());

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
        var series = Assert.Single(index.EligibleSeries());

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

        Assert.Empty(index.EligibleSeries());
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
        var series = Assert.Single(index.EligibleSeries());

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
        var series = Assert.Single(index.EligibleSeries());

        Assert.False(series.MalRevealed);
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
}
