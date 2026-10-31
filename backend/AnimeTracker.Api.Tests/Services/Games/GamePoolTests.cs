using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower design D4: GamePool.Query is the one
// definition of "eligible" behind the count, the start and every pick, so each
// setting is pinned here against rows that sit either side of it.
public class GamePoolTests
{
    private static async Task<List<int>> IdsAsync(AnimeTrackerDbContext db, GameSettings settings) =>
        await GamePool.Query(db, settings, Now).OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();

    // ---- what makes a row eligible at all ----

    [Fact]
    public async Task AnAnimeWithNoPictureIsLeftOut()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, picture: null));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AnAnimeWithNoScoreOrAScoreOfZeroIsLeftOut()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, score: null), Anime(3, score: 0));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AnAnimeFromAnySourceIsEligible()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2), Anime(3), Anime(4));
        // 1 is stored only because it's on a top list, 2 only for a season listing,
        // 3 for neither, and 4 is on my list.
        db.TopAnimeRankingEntries.Add(new TopAnimeRankingEntry { RankingType = "all", AnimeId = 1, Rank = 1 });
        db.SeasonAnimeListings.Add(new SeasonAnimeListing { Year = 2024, Season = "fall", AnimeId = 2 });
        db.UserAnimeEntries.Add(OnList(4));
        await db.SaveChangesAsync();

        Assert.Equal([1, 2, 3, 4], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task ARowWithAPictureAndAScoreButNoPopularityRankIsEligible()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, rank: null));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    // ---- episodes that have aired ----

    private static EpisodeAiring Episode(int animeId, int episode, DateTimeOffset airsAtUtc) =>
        new() { AnimeId = animeId, Episode = episode, AirsAtUtc = airsAtUtc, FetchedAt = Now };

    [Fact]
    public async Task AnAnimeNotYetAiredIsLeftOut()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, airingStatus: "not_yet_aired"));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Theory]
    [InlineData("finished_airing")]
    [InlineData("currently_airing")]
    [InlineData(null)]
    public async Task AnyOtherAiringStatusPassesIncludingNone(string? status)
    {
        // Most stored rows are listing-only and carry no status; they have to pass.
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airingStatus: status));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AStoredAiredEpisodeBeatsAStaleNotYetAiredStatus()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airingStatus: "not_yet_aired"));
        db.EpisodeAirings.Add(Episode(1, 1, Now.AddDays(-2)));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task EpisodesThatHaveNotAiredYetDoNotMakeAnAnimeAired()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airingStatus: "not_yet_aired"));
        db.EpisodeAirings.AddRange(Episode(1, 1, Now.AddDays(3)), Episode(1, 2, Now.AddDays(10)));
        await db.SaveChangesAsync();

        Assert.Empty(await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AnEpisodeAiringAtThisVeryInstantHasAired()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, airingStatus: "not_yet_aired"), Anime(2, airingStatus: "not_yet_aired"));
        db.EpisodeAirings.AddRange(Episode(1, 1, Now), Episode(2, 1, Now.AddSeconds(1)));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AnotherAnimesEpisodesDoNotCount()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, airingStatus: "not_yet_aired"), Anime(2, airingStatus: "not_yet_aired"));
        db.EpisodeAirings.Add(Episode(2, 1, Now.AddDays(-1)));
        await db.SaveChangesAsync();

        Assert.Equal([2], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AnAnimeNotYetAiredIsLeftOutEvenOnMyList()
    {
        // A Plan to watch show that hasn't premiered is not a question anyone can answer.
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, airingStatus: "not_yet_aired"));
        db.UserAnimeEntries.AddRange(OnList(1), OnList(2));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime with { List = GameListScope.OnlyMyList }));
        Assert.Empty(await IdsAsync(db, AnyAnime with { List = GameListScope.NotOnMyList }));
    }

    // ---- release years ----

    private static readonly DateOnly[] Edges =
    [
        new(2009, 12, 31), new(2010, 1, 1), new(2012, 6, 15), new(2015, 12, 31), new(2016, 1, 1),
    ];

    private static async Task SeedEdgesAsync(AnimeTrackerDbContext db)
    {
        for (var i = 0; i < Edges.Length; i++)
            db.AnimeMetadata.Add(Anime(i + 1, airedFrom: Edges[i]));
        db.AnimeMetadata.Add(Anime(6, airedFrom: null));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task BothYearBoundsAreInclusive()
    {
        using var db = CreateDb();
        await SeedEdgesAsync(db);

        var ids = await IdsAsync(db, AnyAnime with { YearFrom = 2010, YearTo = 2015 });

        // The last day of 2009 and the first of 2016 are out; 1 January 2010 and
        // 31 December 2015 are in.
        Assert.Equal([2, 3, 4], ids);
    }

    [Fact]
    public async Task OneYearHoldsOnlyThatYear()
    {
        using var db = CreateDb();
        await SeedEdgesAsync(db);

        Assert.Equal([2], await IdsAsync(db, AnyAnime with { YearFrom = 2010, YearTo = 2010 }));
        Assert.Equal([3], await IdsAsync(db, AnyAnime with { YearFrom = 2012, YearTo = 2012 }));
        Assert.Equal([4], await IdsAsync(db, AnyAnime with { YearFrom = 2015, YearTo = 2015 }));
    }

    [Fact]
    public async Task OnlyAFromBoundLeavesTheEndOpen()
    {
        using var db = CreateDb();
        await SeedEdgesAsync(db);

        Assert.Equal([2, 3, 4, 5], await IdsAsync(db, AnyAnime with { YearFrom = 2010 }));
    }

    [Fact]
    public async Task OnlyAToBoundLeavesTheStartOpen()
    {
        using var db = CreateDb();
        await SeedEdgesAsync(db);

        Assert.Equal([1, 2, 3, 4], await IdsAsync(db, AnyAnime with { YearTo = 2015 }));
    }

    [Theory]
    [InlineData(2010, null)]
    [InlineData(null, 2015)]
    [InlineData(2010, 2015)]
    [InlineData(2020, 2020)]
    public async Task AnUnknownStartDateNeverMatchesASetBound(int? from, int? to)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airedFrom: null));
        await db.SaveChangesAsync();

        Assert.Empty(await IdsAsync(db, AnyAnime with { YearFrom = from, YearTo = to }));
    }

    [Fact]
    public async Task AnUnknownStartDateMatchesWhenBothYearsAreAny()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, airedFrom: null));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime));
    }

    // ---- types ----

    [Fact]
    public async Task OnlyTheChosenTypesMatch()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, mediaType: "tv"), Anime(2, mediaType: "movie"), Anime(3, mediaType: "ova"),
            Anime(4, mediaType: "music"), Anime(5, mediaType: "tv"));
        await db.SaveChangesAsync();

        Assert.Equal([1, 2, 5], await IdsAsync(db, AnyAnime with { MediaTypes = ["tv", "movie"] }));
        Assert.Equal([3], await IdsAsync(db, AnyAnime with { MediaTypes = ["ova"] }));
    }

    [Fact]
    public async Task NoChosenTypesMeansEveryTypeIncludingAnUnknownOne()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, mediaType: "tv"), Anime(2, mediaType: "unknown"), Anime(3, mediaType: null), Anime(4, mediaType: "cm"));
        await db.SaveChangesAsync();

        Assert.Equal([1, 2, 3, 4], await IdsAsync(db, AnyAnime));
    }

    [Fact]
    public async Task AChosenTypeNeverMatchesAnAnimeWithNoType()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, mediaType: null), Anime(2, mediaType: "tv"));
        await db.SaveChangesAsync();

        Assert.Equal([2], await IdsAsync(db, AnyAnime with { MediaTypes = ["tv"] }));
    }

    // ---- my list ----

    private static readonly WatchStatus[] AllStatuses = Enum.GetValues<WatchStatus>();

    private static async Task SeedEveryStatusAsync(AnimeTrackerDbContext db)
    {
        // 1..6 are on my list in each of the six statuses; 7 and 8 are not.
        for (var id = 1; id <= 8; id++)
            db.AnimeMetadata.Add(Anime(id));
        for (var i = 0; i < AllStatuses.Length; i++)
            db.UserAnimeEntries.Add(OnList(i + 1, AllStatuses[i]));
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task EveryStatusCountsAsOnMyList()
    {
        Assert.Equal(6, AllStatuses.Length);
        using var db = CreateDb();
        await SeedEveryStatusAsync(db);

        var ids = await IdsAsync(db, AnyAnime with { List = GameListScope.OnlyMyList });

        Assert.Equal([1, 2, 3, 4, 5, 6], ids);
    }

    [Fact]
    public async Task NotInMyListLeavesOutEveryStatus()
    {
        using var db = CreateDb();
        await SeedEveryStatusAsync(db);

        var ids = await IdsAsync(db, AnyAnime with { List = GameListScope.NotOnMyList });

        Assert.Equal([7, 8], ids);
    }

    [Fact]
    public async Task AllAnimeTakesBothSides()
    {
        using var db = CreateDb();
        await SeedEveryStatusAsync(db);

        Assert.Equal(Enumerable.Range(1, 8), await IdsAsync(db, AnyAnime with { List = GameListScope.All }));
    }

    [Fact]
    public async Task AnAnimeOnMyListStillNeedsAPictureAndAScore()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1), Anime(2, picture: null), Anime(3, score: null));
        db.UserAnimeEntries.AddRange(OnList(1), OnList(2), OnList(3));
        await db.SaveChangesAsync();

        Assert.Equal([1], await IdsAsync(db, AnyAnime with { List = GameListScope.OnlyMyList }));
    }

    // ---- Hide NSFW ----

    [Fact]
    public async Task HideNsfwLeavesOutRxAndKeepsANullRating()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, rating: "rx"), Anime(2, rating: null), Anime(3, rating: "r+"), Anime(4, rating: "pg_13"));
        await db.SaveChangesAsync();

        Assert.Equal([2, 3, 4], await IdsAsync(db, AnyAnime with { HideHentai = true }));
    }

    [Fact]
    public async Task WithoutHideNsfwRxIsEligible()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rating: "rx"), Anime(2, rating: null));
        await db.SaveChangesAsync();

        Assert.Equal([1, 2], await IdsAsync(db, AnyAnime with { HideHentai = false }));
    }

    // ---- settings together ----

    [Fact]
    public async Task EverySettingNarrowsTogether()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, mediaType: "tv", airedFrom: new DateOnly(2020, 4, 1)),                      // matches
            Anime(2, mediaType: "tv", airedFrom: new DateOnly(2020, 4, 1)),                      // on my list
            Anime(3, mediaType: "movie", airedFrom: new DateOnly(2020, 4, 1)),                   // wrong type
            Anime(4, mediaType: "tv", airedFrom: new DateOnly(2019, 4, 1)),                      // wrong year
            Anime(5, mediaType: "tv", airedFrom: new DateOnly(2020, 4, 1), rating: "rx"));       // hidden
        db.UserAnimeEntries.Add(OnList(2));
        await db.SaveChangesAsync();

        var settings = new GameSettings(2020, 2020, ["tv"], GameListScope.NotOnMyList, true);

        Assert.Equal([1], await IdsAsync(db, settings));
    }

    // ---- the projection a draw reads ----

    [Fact]
    public async Task CandidatesCarryTheScoreTheRankAndWhetherTheAnimeIsOnMyList()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 8.31, rank: 42), Anime(2, score: 6.5, rank: null));
        db.UserAnimeEntries.Add(OnList(2));
        await db.SaveChangesAsync();

        var candidates = await GamePool.Query(db, AnyAnime, Now).OrderBy(a => a.Id).ToCandidates().ToListAsync();

        Assert.Equal(
            [new PickCandidate(1, 8.31, 42, false), new PickCandidate(2, 6.5, null, true)],
            candidates);
    }
}
