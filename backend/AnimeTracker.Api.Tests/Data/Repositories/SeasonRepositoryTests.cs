using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace AnimeTracker.Api.Tests.Data.Repositories;

// SeasonRepository's listing read (design.md D1/D3, tasks.md 1.1-1.3, 11.1,
// 11.2): GetListingAsync now returns a season's or year's *whole* listing —
// no offset/limit/sort/includeMyList/type arguments, since paging and the
// two page filters moved client-side and sorting moved to a per-item
// SortOrder key. These tests assert the four SortOrder positions reproduce
// exactly the orderings the old server-side sort used to produce (so the
// move to keys cannot silently reorder anything), and that those positions
// are dense, order-preserving-under-filtering indices.
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

    private static List<int> OrderBy(List<SeasonAnimeItem> items, Func<SeasonSortOrder, int> key) =>
        items.OrderBy(i => key(i.SortOrder)).Select(i => i.AnimeId).ToList();

    [Fact]
    public async Task FullListingReturnsTheUnionWithNoDuplicates()
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        Assert.Equal([1, 2, 3, 4], items.Select(i => i.AnimeId).OrderBy(i => i));
        Assert.Equal(4, items.Select(i => i.AnimeId).Distinct().Count());
    }

    [Fact]
    public async Task HideHentaiExcludesOnlyRatingRxAndNeverAnUnratedAnime()
    {
        using var db = CreateDb();
        Seed(db, 1, "Explicit", rating: "rx");
        Seed(db, 2, "Mature", rating: "r+");
        Seed(db, 3, "Unrated", rating: null);
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        List(db, 3, 2020, "summer");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var items = await repository.GetListingAsync(Year2020, hideHentai: true);

        Assert.Equal([2, 3], items.Select(i => i.AnimeId).OrderBy(i => i));
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        Assert.Equal([2, 1, 3, 4], OrderBy(items, s => s.Popularity));
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        Assert.Equal([2, 1, 4, 3], OrderBy(items, s => s.MalScore));
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        Assert.Equal(["Alpha", "Mike", "Zeta"], items.OrderBy(i => i.SortOrder.Alphabetical).Select(i => i.Title));
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        // Scored anime first by descending score, then every unscored anime
        // falls through to the unranked-last popularity ordering.
        Assert.Equal([2, 1, 3, 4], OrderBy(items, s => s.MyScore));
    }

    // anime-ranking capability read into the season/year my-score sort
    // (design.md D3): equal scores broken by the ranking's stored position
    // rather than popularity, dropped members banded below hand-ordered ones
    // of the same score.
    [Fact]
    public async Task MyScoreSortBreaksTiedScoresByStoredRankingPosition()
    {
        using var db = CreateDb();
        var placedSecond = Seed(db, 1, "Placed second", popularityRank: 1);
        var placedFirst = Seed(db, 2, "Placed first", popularityRank: 2);
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 1, Anime = placedSecond, MyScore = 7 });
        db.UserAnimeEntries.Add(new UserAnimeEntry { AnimeId = 2, Anime = placedFirst, MyScore = 7 });
        db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = 2, Anime = placedFirst, Position = 0 });
        db.TopAnimeSelections.Add(new TopAnimeSelection { AnimeId = 1, Anime = placedSecond, Position = 1 });
        List(db, 1, 2020, "winter");
        List(db, 2, 2020, "spring");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        Assert.Equal([2, 1], OrderBy(items, s => s.MyScore));
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

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        // The dropped anime's title would sort first alphabetically, but its
        // band puts it after the hand-ordered one regardless.
        Assert.Equal([2, 1], OrderBy(items, s => s.MyScore));
    }

    // tasks.md 11.2: the four SortOrder fields are dense positions (0..n-1,
    // one per item, no gaps or repeats) in a total order over the whole
    // listing — the property that lets the client re-sort a filtered subset
    // without disturbing relative order.
    [Fact]
    public async Task SortOrderPositionsAreDenseOverTheWholeListing()
    {
        using var db = CreateDb();
        for (var i = 1; i <= 6; i++)
            Seed(db, i, $"Show {i:00}", popularityRank: i, malScore: i);
        foreach (var i in Enumerable.Range(1, 6))
            List(db, i, 2020, "winter");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);

        var expected = Enumerable.Range(0, 6).OrderBy(i => i).ToList();
        Assert.Equal(expected, items.Select(i => i.SortOrder.Popularity).OrderBy(p => p));
        Assert.Equal(expected, items.Select(i => i.SortOrder.MalScore).OrderBy(p => p));
        Assert.Equal(expected, items.Select(i => i.SortOrder.Alphabetical).OrderBy(p => p));
        Assert.Equal(expected, items.Select(i => i.SortOrder.MyScore).OrderBy(p => p));
    }

    // tasks.md 11.2: removing items from the listing (what the client's type/
    // in-my-list filters do) preserves the relative order of what's left,
    // under every sort — no ordering rule needs to be reapplied client-side.
    [Fact]
    public async Task FilteringItemsOutPreservesRelativeOrderUnderEverySort()
    {
        using var db = CreateDb();
        Seed(db, 1, "Charlie", popularityRank: 3, malScore: 3);
        Seed(db, 2, "Alpha", popularityRank: 1, malScore: 1);
        Seed(db, 3, "Delta", popularityRank: 4, malScore: 4);
        Seed(db, 4, "Bravo", popularityRank: 2, malScore: 2);
        foreach (var i in Enumerable.Range(1, 4))
            List(db, i, 2020, "winter");
        await db.SaveChangesAsync();
        var repository = new SeasonRepository(db);

        var items = await repository.GetListingAsync(Year2020, hideHentai: false);
        var fullPopularityOrder = OrderBy(items, s => s.Popularity);
        var fullAlphabeticalOrder = items.OrderBy(i => i.SortOrder.Alphabetical).Select(i => i.AnimeId).ToList();

        // Simulate a client-side filter dropping anime 4 (Bravo) out of the
        // already-loaded listing.
        var filtered = items.Where(i => i.AnimeId != 4).ToList();

        Assert.Equal(fullPopularityOrder.Where(id => id != 4), OrderBy(filtered, s => s.Popularity));
        Assert.Equal(fullAlphabeticalOrder.Where(id => id != 4), filtered.OrderBy(i => i.SortOrder.Alphabetical).Select(i => i.AnimeId));
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
