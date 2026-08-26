using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// SeasonRepository's point-set overloads (design.md D1, tasks.md 1.1-1.5):
// GetPageAsync/HasListingAsync generalised from a single (year, season) to a
// set of them, so the year page's four-season read is one query rather than
// four merged in memory. The single-point overloads are exercised
// indirectly here too, since they now delegate straight into the point-set
// ones (task 1.3) — the regression signal for that is the full existing
// suite (task 1.6), not duplicated here.
public class SeasonRepositoryTests
{
    private static readonly (int Year, string Season)[] Year2020 =
        [(2020, "winter"), (2020, "spring"), (2020, "summer"), (2020, "fall")];

    private static AnimeTrackerDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static AnimeMetadata Seed(
        AnimeTrackerDbContext db,
        int id,
        string title,
        int? popularityRank = null,
        double? malScore = null,
        string? mediaType = null,
        string? rating = null)
    {
        var anime = new AnimeMetadata
        {
            Id = id,
            Title = title,
            PopularityRank = popularityRank,
            MalScore = malScore,
            MediaType = mediaType,
            Rating = rating,
        };
        db.AnimeMetadata.Add(anime);
        return anime;
    }

    private static void List(AnimeTrackerDbContext db, int animeId, int year, string season) =>
        db.SeasonAnimeListings.Add(new SeasonAnimeListing { Year = year, Season = season, AnimeId = animeId });

    [Fact]
    public async Task FourPointReadReturnsTheUnionWithNoDuplicates()
    {
        using var db = CreateDb();
        Seed(db, 1, "Winter show");
        Seed(db, 2, "Spring show");
        Seed(db, 3, "Summer show");
        Seed(db, 4, "Fall show");
        Seed(db, 5, "Other year show");
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        List(db, 4, 2020, "fall");
        List(db, 5, 2021, "winter");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, totalCount) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Alphabetical, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        Assert.Equal(4, totalCount);
        Assert.Equal([1, 2, 3, 4], items.Select(i => i.AnimeId).OrderBy(i => i));
        Assert.Equal(4, items.Select(i => i.AnimeId).Distinct().Count());
    }

    [Fact]
    public async Task PopularitySortOrdersAcrossAllFourPointsWithUnrankedLast()
    {
        using var db = CreateDb();
        Seed(db, 1, "Rank 2", popularityRank: 2);
        Seed(db, 2, "Rank 1", popularityRank: 1);
        Seed(db, 3, "Unranked A", popularityRank: null);
        Seed(db, 4, "Unranked B", popularityRank: 0);
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        List(db, 4, 2020, "fall");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Popularity, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        Assert.Equal([2, 1, 3, 4], items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task MalScoreSortOrdersAcrossAllFourPoints()
    {
        using var db = CreateDb();
        Seed(db, 1, "Mid", malScore: 7.5);
        Seed(db, 2, "High", malScore: 9.1);
        Seed(db, 3, "None", malScore: null);
        Seed(db, 4, "Low", malScore: 5.0);
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        List(db, 4, 2020, "fall");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MalScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        Assert.Equal([2, 1, 4, 3], items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task AlphabeticalSortOrdersAcrossAllFourPoints()
    {
        using var db = CreateDb();
        Seed(db, 1, "Zeta");
        Seed(db, 2, "Alpha");
        Seed(db, 3, "Mike");
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "summer");
        List(db, 3, 2020, "fall");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Alphabetical, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        Assert.Equal(["Alpha", "Mike", "Zeta"], items.Select(i => i.Title));
    }

    [Fact]
    public async Task MyScoreSortGroupsScoredFirstAcrossAllFourPoints()
    {
        using var db = CreateDb();
        var scoredLow = Seed(db, 1, "Scored low", popularityRank: 1);
        var scoredHigh = Seed(db, 2, "Scored high", popularityRank: 2);
        Seed(db, 3, "Unscored ranked", popularityRank: 1);
        Seed(db, 4, "Unscored unranked", popularityRank: null);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = scoredLow, MyScore = 6 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = scoredHigh, MyScore = 9 });
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        List(db, 4, 2020, "fall");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        // Scored anime first by descending score, then every unscored anime
        // falls through to the unranked-last popularity ordering.
        Assert.Equal([2, 1, 3, 4], items.Select(i => i.AnimeId));
    }

    // anime-ranking capability read into the season/year my-score sort
    // (design.md D3, tasks.md 3.6/3.7): equal scores broken by the ranking's
    // stored position rather than popularity, dropped members banded below
    // hand-ordered ones of the same score, and the grouping/order holding
    // across paged loads.
    [Fact]
    public async Task MyScoreSortBreaksTiedScoresByStoredRankingPosition()
    {
        using var db = CreateDb();
        var placedSecond = Seed(db, 1, "Placed second", popularityRank: 1);
        var placedFirst = Seed(db, 2, "Placed first", popularityRank: 2);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = placedSecond, MyScore = 7 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = placedFirst, MyScore = 7 });
        db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = 2, Anime = placedFirst, Position = 0, SelectedAt = DateTimeOffset.UtcNow });
        db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = 1, Anime = placedSecond, Position = 1, SelectedAt = DateTimeOffset.UtcNow });
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        Assert.Equal([2, 1], items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task MyScoreSortBandsADroppedMemberBelowAHandOrderedOneOfTheSameScore()
    {
        using var db = CreateDb();
        var dropped = Seed(db, 1, "Aardvark dropped", popularityRank: 1);
        var handOrdered = Seed(db, 2, "Zebra hand ordered", popularityRank: 2);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = dropped, Status = WatchStatus.Dropped, MyScore = 7 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = handOrdered, Status = WatchStatus.Completed, MyScore = 7 });
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);

        // The dropped anime's title would sort first alphabetically, but its
        // band puts it after the hand-ordered one regardless.
        Assert.Equal([2, 1], items.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task MyScoreSortHoldsItsOrderAcrossPagedLoads()
    {
        using var db = CreateDb();
        for (var i = 1; i <= 4; i++)
        {
            var anime = Seed(db, i, $"Scored {i:00}", popularityRank: i);
            db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = i, Anime = anime, MyScore = 10 - i }); // descending scores 9,8,7,6
            db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = i, Anime = anime, Position = 4 - i, SelectedAt = DateTimeOffset.UtcNow });
            List(db, i, 2020, "winter");
        }
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (singlePage, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 100);
        var (firstPage, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 2);
        var (secondPage, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.MyScore, includeMyList: true, hideHentai: false, types: null, offset: 2, limit: 2);

        Assert.Equal(singlePage.Select(i => i.AnimeId), firstPage.Select(i => i.AnimeId).Concat(secondPage.Select(i => i.AnimeId)));
    }

    [Fact]
    public async Task TotalCountIsComputedAfterFilters()
    {
        using var db = CreateDb();
        Seed(db, 1, "Movie", mediaType: "movie");
        Seed(db, 2, "TV", mediaType: "tv");
        Seed(db, 3, "Another movie", mediaType: "movie");
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (items, totalCount) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Alphabetical, includeMyList: true, hideHentai: false, types: ["movie"], offset: 0, limit: 100);

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task SkipAndTakePageContinuouslyAcrossThePointSet()
    {
        using var db = CreateDb();
        Seed(db, 1, "A", popularityRank: 1);
        Seed(db, 2, "B", popularityRank: 2);
        Seed(db, 3, "C", popularityRank: 3);
        Seed(db, 4, "D", popularityRank: 4);
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        List(db, 4, 2020, "fall");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var (firstPage, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Popularity, includeMyList: true, hideHentai: false, types: null, offset: 0, limit: 2);
        var (secondPage, _) = await repository.GetPageAsync(
            Year2020, SeasonSortKey.Popularity, includeMyList: true, hideHentai: false, types: null, offset: 2, limit: 2);

        Assert.Equal([1, 2], firstPage.Select(i => i.AnimeId));
        Assert.Equal([3, 4], secondPage.Select(i => i.AnimeId));
    }

    [Fact]
    public async Task HasListingAsyncIsTrueWhenAnyOfTheFourPointsHasARow()
    {
        using var db = CreateDb();
        Seed(db, 1, "Only summer entry");
        List(db, 1, 2020, "summer");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        Assert.True(await repository.HasListingAsync(Year2020));
        Assert.False(await repository.HasListingAsync([(2021, "winter"), (2021, "spring"), (2021, "summer"), (2021, "fall")]));
    }
}
