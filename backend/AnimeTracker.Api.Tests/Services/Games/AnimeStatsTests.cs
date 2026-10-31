using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime design D6: AnimeStats.IsMissing is the one definition of
// "this anime has to be fetched before a game can use it", behind the draw's
// query and a guess's check. Each stat is pinned alone, against a row that holds
// all the others.
public class AnimeStatsTests
{
    private static readonly DateOnly Aired = new(2013, 4, 7);

    private static AnimeMetadata Complete(int id = 1) => Whole(id, airedFrom: Aired);

    /// <summary>Asks both ways the rule is used: as the entity check, and as a
    /// query on the in-memory provider.</summary>
    private static async Task<(bool Check, bool Query)> MissingAsync(AnimeMetadata anime)
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var query = await db.AnimeMetadata.Where(AnimeStats.IsMissing).AnyAsync();
        return (AnimeStats.IsMissingAny(anime), query);
    }

    // ---- an anime holding every stat ----

    [Fact]
    public async Task AnAnimeHoldingEveryStatIsNotMissingAny()
    {
        var (check, query) = await MissingAsync(Complete());

        Assert.False(check);
        Assert.False(query);
    }

    [Fact]
    public async Task HoldingEveryStatCountsHoweverOldTheFetchWas()
    {
        var anime = Complete();
        anime.LastSyncedAt = new DateTimeOffset(2019, 1, 1, 0, 0, 0, TimeSpan.Zero);

        var (check, query) = await MissingAsync(anime);

        Assert.False(check);
        Assert.False(query);
    }

    // ---- each stat on its own ----

    [Fact]
    public async Task NoStartDateIsMissing()
    {
        var anime = Complete();
        anime.AiredFrom = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task NoEpisodeCountIsMissing()
    {
        var anime = Complete();
        anime.TotalEpisodes = null;
        anime.AiringStatus = "finished_airing";

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task NoEpisodeCountOnAnUnfetchedRowIsMissing()
    {
        var anime = Complete();
        anime.TotalEpisodes = null;
        anime.AiringStatus = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task AShowStillAiringWithNoTotalIsNotMissingItsEpisodeCount()
    {
        var anime = Complete();
        anime.TotalEpisodes = null;
        anime.AiringStatus = "currently_airing";

        Assert.Equal((false, false), await MissingAsync(anime));
    }

    [Fact]
    public async Task NoTypeIsMissing()
    {
        var anime = Complete();
        anime.MediaType = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task NoMalScoreIsMissing()
    {
        var anime = Complete();
        anime.MalScore = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task NullGenresAreMissing()
    {
        var anime = Complete();
        anime.Genres = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task EmptyGenresAreMissing()
    {
        var anime = Complete();
        anime.Genres = [];

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task ANullStudioListIsMissing()
    {
        var anime = Complete();
        anime.Studios = null;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    [Fact]
    public async Task AnEmptyStudioListIsKnown()
    {
        // [] is MyAnimeList saying it credits nobody; null is nobody having asked.
        var anime = Complete();
        anime.Studios = [];

        Assert.Equal((false, false), await MissingAsync(anime));
    }

    [Fact]
    public async Task AnAnimeTheBrowseListedAndNobodyFetchedIsMissing()
    {
        // What a listing refresh stores: lean fields only, no genres or studios.
        var anime = Anime(1, airedFrom: Aired);
        anime.TotalEpisodes = 12;

        Assert.Equal((true, true), await MissingAsync(anime));
    }

    // ---- the opposite, and the projection ----

    [Fact]
    public async Task IsCompleteIsExactlyWhatIsNotMissing()
    {
        using var db = CreateDb();
        var noGenres = Complete(2);
        noGenres.Genres = [];
        var noScore = Complete(3);
        noScore.MalScore = null;
        db.AnimeMetadata.AddRange(Complete(1), noGenres, noScore, Complete(4));
        await db.SaveChangesAsync();

        var missing = await db.AnimeMetadata.Where(AnimeStats.IsMissing).OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();
        var complete = await db.AnimeMetadata.Where(AnimeStats.IsComplete).OrderBy(a => a.Id).Select(a => a.Id).ToListAsync();

        Assert.Equal([2, 3], missing);
        Assert.Equal([1, 4], complete);
    }

    [Fact]
    public async Task TheProjectionCarriesTheSevenStatsAndTheTitleAndPicture()
    {
        using var db = CreateDb();
        var anime = Whole(
            7, score: 8.31, mediaType: "movie", airedFrom: new DateOnly(2016, 8, 26), episodes: 1,
            genres: ["Drama", "Romance"], studios: ["CoMix Wave Films"]);
        db.AnimeMetadata.Add(anime);
        await db.SaveChangesAsync();

        var stats = await db.AnimeMetadata.Select(AnimeStats.Select).SingleAsync();

        Assert.Equal(7, stats.Id);
        Assert.Equal("Anime 7", stats.Title);
        Assert.Equal("English 7", stats.EnglishTitle);
        Assert.Equal("https://img/7.jpg", stats.PictureUrl);
        Assert.Equal(new DateOnly(2016, 8, 26), stats.AiredFrom);
        Assert.Equal(["Drama", "Romance"], stats.Genres);
        Assert.Equal(1, stats.Episodes);
        Assert.Equal(["CoMix Wave Films"], stats.Studios);
        Assert.Equal("movie", stats.MediaType);
        Assert.Equal(8.31, stats.MalScore);
    }

    [Fact]
    public void OfProjectsAnEntityTheWayTheQueryDoes()
    {
        var stats = AnimeStats.Of(Whole(4, score: 6.5, studios: []));

        Assert.Equal(4, stats.Id);
        Assert.Equal(6.5, stats.MalScore);
        Assert.NotNull(stats.Studios);
        Assert.Empty(stats.Studios);
    }

    // ---- it translates on Npgsql ----

    private static AnimeTrackerDbContext NpgsqlDb() =>
        new(new DbContextOptionsBuilder<AnimeTrackerDbContext>().UseNpgsql("Host=localhost;Database=none").Options);

    [Fact]
    public void TheMissingRuleTranslatesToSqlOnNpgsql()
    {
        using var db = NpgsqlDb();

        var sql = db.AnimeMetadata.Where(AnimeStats.IsMissing).ToQueryString();

        Assert.Contains("\"AiredFrom\" IS NULL", sql);
        Assert.Contains("\"TotalEpisodes\" IS NULL", sql);
        Assert.Contains("\"MediaType\" IS NULL", sql);
        Assert.Contains("\"MalScore\" IS NULL", sql);
        Assert.Contains("\"Genres\" IS NULL", sql);
        Assert.Contains("\"Studios\" IS NULL", sql);
        // Empty genres, as the array's length.
        Assert.Contains("cardinality(", sql);
    }

    [Fact]
    public void TheCompleteRuleAndTheProjectionTranslateToo()
    {
        using var db = NpgsqlDb();

        var complete = db.AnimeMetadata.Where(AnimeStats.IsComplete).ToQueryString();
        var projected = db.AnimeMetadata.Where(AnimeStats.IsMissing).Select(AnimeStats.Select).ToQueryString();

        // Npgsql turns the negation into the rule's opposite, column by column.
        Assert.Contains("\"AiredFrom\" IS NOT NULL", complete);
        Assert.Contains("\"Studios\" IS NOT NULL", complete);
        Assert.Contains("cardinality(", complete);
        Assert.Contains("\"Genres\"", projected);
        Assert.Contains("\"Studios\"", projected);
    }
}
