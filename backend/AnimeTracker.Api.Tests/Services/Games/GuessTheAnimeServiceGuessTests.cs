using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeServiceTests;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime tasks 5.4-5.6: a guess's checks and fetch, the three endings,
// and what each leaves behind. The reads and Start are in GuessTheAnimeServiceTests.
// Most cases write the game straight to its table (AddGuessRunAsync) so they can
// stand it exactly where the rule they check applies.
public class GuessTheAnimeServiceGuessTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateOnly Aired = new(2013, 4, 7);

    /// <summary>The answer, anime 1, and <paramref name="others"/> more (ids 2
    /// upward), every one holding every stat.</summary>
    private static async Task SeedAsync(AnimeTrackerDbContext db, int others = 12)
    {
        db.AnimeMetadata.Add(Whole(1, score: 8.0, airedFrom: Aired, episodes: 24, genres: ["Action", "Drama"]));
        for (var id = 2; id < 2 + others; id++)
            db.AnimeMetadata.Add(Whole(id, score: 7.0, airedFrom: Aired, episodes: 12, genres: ["Action", "Comedy"]));
        await db.SaveChangesAsync();
    }

    private static Task<GuessTheAnimeRun?> StoredRunAsync(AnimeTrackerDbContext db) =>
        db.GuessTheAnimeRuns.AsNoTracking().SingleOrDefaultAsync();

    // ---- a show with no total is compared on how many episodes have aired ----

    private static EpisodeAiring Aired_(int animeId, int episode, DateTimeOffset at) =>
        new() { AnimeId = animeId, Episode = episode, AirsAtUtc = at, FetchedAt = at };

    [Fact]
    public async Task AGuessWithNoTotalIsComparedOnHowManyEpisodesHaveAired()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        // Anime 30 has no total (still airing), 1,180 of whatever it will be aired.
        var longRunner = Whole(30, airedFrom: Aired, episodes: null);
        longRunner.AiringStatus = "currently_airing";
        db.AnimeMetadata.Add(longRunner);
        var past = DateTimeOffset.UtcNow.AddDays(-30);
        db.EpisodeAirings.AddRange(Aired_(30, 1, past.AddDays(-14)), Aired_(30, 2, past.AddDays(-7)), Aired_(30, 3, past), Aired_(30, 4, DateTimeOffset.UtcNow.AddDays(5)));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);

        var (service, details) = Make(db);
        var result = await service.GuessAsync(run.Id, 1, 30);

        // Three aired and a fourth still to come: 3, below the answer's 24.
        Assert.Equal(3, result.Row.Episodes.Value);
        Assert.Equal(GuessCompared.AnswerHigher, result.Row.Episodes.Compared);
        Assert.Empty(details.Calls);
    }

    [Fact]
    public async Task TheAnswerWithNoTotalIsComparedOnHowManyEpisodesHaveAiredToo()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var longRunner = Whole(30, airedFrom: Aired, episodes: null);
        longRunner.AiringStatus = "currently_airing";
        db.AnimeMetadata.Add(longRunner);
        var past = DateTimeOffset.UtcNow.AddDays(-30);
        db.EpisodeAirings.AddRange(Aired_(30, 1, past), Aired_(30, 2, past.AddDays(7)), Aired_(30, 3, past.AddDays(14)));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 30);

        var guessed = await Make(db).Service.GuessAsync(run.Id, 1, 2); // 12 episodes

        Assert.Equal(12, guessed.Row.Episodes.Value);
        Assert.Equal(GuessCompared.AnswerLower, guessed.Row.Episodes.Compared);
    }

    [Fact]
    public async Task AShowWithNoTotalAndNoAiredEpisodeStoredStaysUnknown()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var quiet = Whole(30, airedFrom: Aired, episodes: null);
        quiet.AiringStatus = "currently_airing";
        db.AnimeMetadata.Add(quiet);
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);

        var result = await Make(db).Service.GuessAsync(run.Id, 1, 30);

        Assert.Null(result.Row.Episodes.Value);
        Assert.Null(result.Row.Episodes.Compared);
    }

    [Fact]
    public async Task AGuessedRowOnTheStateReadAlsoUsesTheAiredCount()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var longRunner = Whole(30, airedFrom: Aired, episodes: null);
        longRunner.AiringStatus = "currently_airing";
        db.AnimeMetadata.Add(longRunner);
        var past = DateTimeOffset.UtcNow.AddDays(-30);
        db.EpisodeAirings.AddRange(Aired_(30, 1, past), Aired_(30, 2, past.AddDays(7)));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 1, guessed: [30]);

        var state = await Make(db).Service.GetStateAsync(hideHentai: false);

        Assert.Equal(2, state.Game!.Rows.Single().Episodes.Value);
    }

    // ---- what is refused (5.4) ----

    [Fact]
    public async Task AGameNotInProgressIsNotFound()
    {
        using var db = CreateDb();
        await SeedAsync(db);

        await Assert.ThrowsAsync<GuessTheAnimeRunNotFoundException>(() => Make(db).Service.GuessAsync(404, 1, 2));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(5)]
    public async Task AStaleNumberIsRefusedAndNothingIsFetched(int number)
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.AnimeMetadata.Add(Lean(40));
        await db.SaveChangesAsync();
        // One guess made, so the next is number 2 and any other number is stale.
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2]);
        var (service, details) = Make(db);

        // 40 is missing stats and so would be fetched, were the guess not refused.
        await Assert.ThrowsAsync<GuessTheAnimeStaleGuessException>(() => service.GuessAsync(run.Id, number, 40));

        Assert.Empty(details.Calls);
        Assert.Equal(1, (await StoredRunAsync(db))!.GuessCount);
    }

    [Fact]
    public async Task AnAnimeAlreadyGuessedIsRefused()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2, 3]);

        await Assert.ThrowsAsync<GuessTheAnimeAlreadyGuessedException>(() => Make(db).Service.GuessAsync(run.Id, 3, 2));

        Assert.Equal([2, 3], (await StoredRunAsync(db))!.GuessedAnimeIds);
    }

    [Fact]
    public async Task ATabBehindTheGameIsToldItIsBehindNotThatItIsWrong()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2]);

        // Number 1 again, naming the anime already guessed: stale, not a repeat.
        await Assert.ThrowsAsync<GuessTheAnimeStaleGuessException>(() => Make(db).Service.GuessAsync(run.Id, 1, 2));
    }

    [Fact]
    public async Task ADoublePressCountsOnce()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var service = Make(db).Service;

        await service.GuessAsync(run.Id, 1, 2);
        await Assert.ThrowsAsync<GuessTheAnimeStaleGuessException>(() => service.GuessAsync(run.Id, 1, 2));

        var stored = await StoredRunAsync(db);
        Assert.Equal(1, stored!.GuessCount);
        Assert.Equal([2], stored.GuessedAnimeIds);
    }

    // ---- an ordinary wrong guess ----

    [Fact]
    public async Task AWrongGuessIsSavedAndAnswersItsRowOnly()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);

        var result = await Make(db).Service.GuessAsync(run.Id, 1, 2);

        Assert.Equal(2, result.Row.AnimeId);
        Assert.Null(result.End);
        Assert.Null(result.Answer);
        var stored = await StoredRunAsync(db);
        Assert.Equal(1, stored!.GuessCount);
        Assert.Equal([2], stored.GuessedAnimeIds);
    }

    [Fact]
    public async Task TheRowIsComparedWithTheAnswer()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);

        var row = (await Make(db).Service.GuessAsync(run.Id, 1, 2)).Row;

        Assert.Equal(GuessCompared.Same, row.Year.Compared);
        Assert.Equal(GuessCompared.AnswerHigher, row.Score.Compared);
        Assert.Equal(GuessCompared.AnswerHigher, row.Episodes.Compared);
        Assert.Equal([("Action", true), ("Comedy", false)], row.Genres.Select(g => (g.Name, g.Same)));
    }

    [Fact]
    public async Task GuessesAreCountedInOrder()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var service = Make(db).Service;

        for (var number = 1; number <= 4; number++)
            await service.GuessAsync(run.Id, number, number + 1);

        var stored = await StoredRunAsync(db);
        Assert.Equal(4, stored!.GuessCount);
        Assert.Equal([2, 3, 4, 5], stored.GuessedAnimeIds);
    }

    [Fact]
    public async Task TheNinthWrongGuessCarriesOn()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2, 3, 4, 5, 6, 7, 8, 9]);

        var result = await Make(db).Service.GuessAsync(run.Id, 9, 10);

        Assert.Null(result.End);
        Assert.Equal(9, (await StoredRunAsync(db))!.GuessCount);
    }

    [Fact]
    public async Task AnAnimeOutsideTheSettingsIsANormalRow()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();
        // Only my list: anime 2 is not on it, and can't be the answer, but can be guessed.
        var run = await AddGuessRunAsync(db, answer: 1, settings: AnyAnime with { List = GameListScope.OnlyMyList });

        var result = await Make(db).Service.GuessAsync(run.Id, 1, 2);

        Assert.Equal(2, result.Row.AnimeId);
        Assert.Equal(GuessCompared.AnswerHigher, result.Row.Score.Compared);
        Assert.Equal(1, (await StoredRunAsync(db))!.GuessCount);
    }

    [Fact]
    public async Task AGuessIsNeverFetchedForWhenItHoldsEveryStat()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);

        await service.GuessAsync(run.Id, 1, 2);

        Assert.Empty(details.Calls);
    }

    // ---- what is fetched (5.4) ----

    [Fact]
    public async Task AGuessMissingStatsIsFetchedAndItsRowShowsThem()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.AnimeMetadata.Add(Lean(40));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);

        var row = (await service.GuessAsync(run.Id, 1, 40)).Row;

        Assert.Equal([40], details.Calls);
        Assert.Equal([("Action", true)], row.Genres.Select(g => (g.Name, g.Same)));
        Assert.Equal(["Madhouse"], row.Studios.Select(s => s.Name));
        Assert.Equal(2013, row.Year.Value);
    }

    [Fact]
    public async Task TheFetchComesBeforeTheRunIsTouched()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.AnimeMetadata.Add(Lean(40));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);
        int? countDuringTheFetch = null;
        details.OnFetch = async _ =>
        {
            countDuringTheFetch = (await db.GuessTheAnimeRuns.AsNoTracking().SingleAsync()).GuessCount;
        };

        await service.GuessAsync(run.Id, 1, 40);

        Assert.Equal(0, countDuringTheFetch);
        Assert.Equal(1, (await StoredRunAsync(db))!.GuessCount);
    }

    [Fact]
    public async Task AnAnimeThatIsNotStoredIsFetchedAndCompared()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);

        var result = await service.GuessAsync(run.Id, 1, 500);

        Assert.Equal([500], details.Calls);
        Assert.Equal(500, result.Row.AnimeId);
        Assert.True(await db.AnimeMetadata.AnyAsync(a => a.Id == 500));
        Assert.Equal([500], (await StoredRunAsync(db))!.GuessedAnimeIds);
    }

    [Fact]
    public async Task AFailedFetchOfANeverFetchedAnimeCountsNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.AnimeMetadata.Add(Lean(40));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2]);
        var (service, details) = Make(db);
        details.Failures[40] = new HttpRequestException("MAL is down");

        var refused = await Assert.ThrowsAsync<GuessTheAnimeDetailsUnavailableException>(() => service.GuessAsync(run.Id, 2, 40));

        Assert.Contains("couldn't be reached", refused.Message);
        var stored = await StoredRunAsync(db);
        Assert.Equal(1, stored!.GuessCount);
        Assert.Equal([2], stored.GuessedAnimeIds);
    }

    [Fact]
    public async Task AFailedFetchOfAnAnimeThatIsNotStoredCountsNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);
        details.Failures[500] = new AnimeMetadataNotFoundException(500);

        var refused = await Assert.ThrowsAsync<GuessTheAnimeDetailsUnavailableException>(() => service.GuessAsync(run.Id, 1, 500));

        Assert.Contains("no such anime", refused.Message);
        Assert.Equal(0, (await StoredRunAsync(db))!.GuessCount);
        Assert.False(await db.AnimeMetadata.AnyAsync(a => a.Id == 500));
    }

    [Fact]
    public async Task AFailedFetchOfAnAnimeFetchedBeforeCountsWithWhatIsStored()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var gap = Whole(40, airedFrom: Aired);
        gap.Genres = null;
        db.AnimeMetadata.Add(gap);
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);
        details.Failures[40] = new HttpRequestException("MAL is down");

        var result = await service.GuessAsync(run.Id, 1, 40);

        Assert.Equal([40], details.Calls);
        Assert.Empty(result.Row.Genres);
        Assert.Equal(2013, result.Row.Year.Value);
        Assert.Equal(1, (await StoredRunAsync(db))!.GuessCount);
    }

    [Fact]
    public async Task TheAnswerNeedsNoFetchWhenGuessed()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        // A fallback answer can be missing a stat; drawing it dealt with that.
        (await db.AnimeMetadata.SingleAsync(a => a.Id == 1)).Genres = null;
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);
        var (service, details) = Make(db);

        var result = await service.GuessAsync(run.Id, 1, 1);

        Assert.Empty(details.Calls);
        Assert.Equal(GuessTheAnimeEnd.Guessed, result.End);
    }

    [Fact]
    public async Task AGuessWaitingOnAFetchWhileAnotherTabGuessesIsStale()
    {
        const string name = "guess-while-fetching";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        await SeedAsync(tabOne);
        tabOne.AnimeMetadata.Add(Lean(40));
        await tabOne.SaveChangesAsync();
        var run = await AddGuessRunAsync(tabOne, answer: 1);
        var (one, details) = Make(tabOne);
        // While tab one waits on MyAnimeList, tab two makes the first guess.
        details.OnFetch = async _ => await Make(tabTwo).Service.GuessAsync(run.Id, 1, 3);

        await Assert.ThrowsAsync<GuessTheAnimeStaleGuessException>(() => one.GuessAsync(run.Id, 1, 40));

        var stored = await StoredRunAsync(tabTwo);
        Assert.Equal(1, stored!.GuessCount);
        Assert.Equal([3], stored.GuessedAnimeIds);
    }

    // ---- the endings (5.4) ----

    [Fact]
    public async Task TheRightGuessWinsAndEndsTheGame()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2, 3]);

        var result = await Make(db).Service.GuessAsync(run.Id, 3, 1);

        Assert.Equal(GuessTheAnimeEnd.Guessed, result.End);
        Assert.Equal(1, result.Row.AnimeId);
        // The answer's row is the guess's own: the same on the wire, since the lists
        // inside a record compare by reference.
        Assert.Equal(JsonSerializer.Serialize(result.Row, Wire), JsonSerializer.Serialize(result.Answer!.Row, Wire));
        Assert.True(result.Answer.InMyList);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task TheWinningRowHasEveryStatTheSame()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);

        var row = (await Make(db).Service.GuessAsync(run.Id, 1, 1)).Row;

        Assert.Equal(GuessCompared.Same, row.Year.Compared);
        Assert.Equal(GuessCompared.Same, row.Episodes.Compared);
        Assert.Equal(GuessCompared.Same, row.Score.Compared);
        Assert.True(row.Season.Same);
        Assert.True(row.MediaType.Same);
        Assert.All(row.Genres, g => Assert.True(g.Same));
        Assert.All(row.Studios, s => Assert.True(s.Same));
    }

    [Fact]
    public async Task AWinOnTheFirstGuess()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);

        var result = await Make(db).Service.GuessAsync(run.Id, 1, 1);

        Assert.Equal(GuessTheAnimeEnd.Guessed, result.End);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task AWinOnTheTenthGuessIsAWinNotRunningOut()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2, 3, 4, 5, 6, 7, 8, 9, 10]);

        var result = await Make(db).Service.GuessAsync(run.Id, 10, 1);

        Assert.Equal(GuessTheAnimeEnd.Guessed, result.End);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task TheTenthWrongGuessEndsTheGameAndShowsTheAnswer()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2, 3, 4, 5, 6, 7, 8, 9, 10]);

        var result = await Make(db).Service.GuessAsync(run.Id, 10, 11);

        Assert.Equal(GuessTheAnimeEnd.OutOfGuesses, result.End);
        Assert.Equal(11, result.Row.AnimeId);
        Assert.Equal(1, result.Answer!.Row.AnimeId);
        Assert.Equal(GuessCompared.Same, result.Answer.Row.Score.Compared);
        Assert.False(result.Answer.InMyList);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task TheAnswerIsInNoResponseBeforeTheEnd()
    {
        using var db = CreateDb();
        var answer = Whole(7771, score: 9.37, airedFrom: new DateOnly(2021, 10, 3), episodes: 37,
            genres: ["Zeta"], studios: ["Zeta Studio"]);
        db.AnimeMetadata.AddRange(answer, WholeAired(2));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 7771);

        var result = await Make(db).Service.GuessAsync(run.Id, 1, 2);

        var json = JsonSerializer.Serialize(result, Wire);
        Assert.DoesNotContain("7771", json);
        Assert.DoesNotContain(answer.Title, json);
        Assert.DoesNotContain(answer.PictureUrl!, json);
        Assert.DoesNotContain("Zeta", json);
        Assert.DoesNotContain("9.37", json);
    }

    [Fact]
    public async Task TwoConcurrentGuessesFailOnTheConcurrencyToken()
    {
        const string name = "concurrent-guess";
        using var tabOne = CreateDb(name);
        await SeedAsync(tabOne);
        var run = await AddGuessRunAsync(tabOne, answer: 1);
        // Tab one has read the game at guess 1 when tab two's guess lands, just
        // before tab one saves.
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheAnimeRuns.Single();
            stored.GuessedAnimeIds = [3];
            stored.GuessCount = 1;
            tabTwo.SaveChanges();
        };

        await Assert.ThrowsAsync<GuessTheAnimeStaleGuessException>(() => Make(tabOne).Service.GuessAsync(run.Id, 1, 2));

        using var check = CreateDb(name);
        var kept = await StoredRunAsync(check);
        Assert.Equal(1, kept!.GuessCount);
        Assert.Equal([3], kept.GuessedAnimeIds);
    }

    [Fact]
    public async Task AGuessAfterAnotherTabEndedTheGameFindsItGone()
    {
        const string name = "guess-after-end";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        await SeedAsync(tabOne);
        var run = await AddGuessRunAsync(tabOne, answer: 1);
        await Make(tabTwo).Service.EndAsync(run.Id);

        await Assert.ThrowsAsync<GuessTheAnimeRunNotFoundException>(() => Make(tabOne).Service.GuessAsync(run.Id, 1, 2));
    }

    // ---- End game (5.5) ----

    [Fact]
    public async Task EndGameDeletesTheGameAndAnswersWithTheAnswer()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [2]);

        var end = await Make(db).Service.EndAsync(run.Id);

        Assert.Equal(1, end.Answer.Row.AnimeId);
        Assert.Equal(GuessCompared.Same, end.Answer.Row.Score.Compared);
        Assert.False(end.Answer.InMyList);
        Assert.Null(await StoredRunAsync(db));
    }

    [Fact]
    public async Task EndGameSaysWhetherTheAnswerIsOnMyList()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);

        Assert.True((await Make(db).Service.EndAsync(run.Id)).Answer.InMyList);
    }

    [Fact]
    public async Task EndingAGameNotInProgressIsNotFound()
    {
        using var db = CreateDb();
        await SeedAsync(db);

        await Assert.ThrowsAsync<GuessTheAnimeRunNotFoundException>(() => Make(db).Service.EndAsync(404));
    }

    [Fact]
    public async Task EndGameRetriesOnceWhenAGuessMovedTheGameOn()
    {
        const string name = "end-after-guess";
        using var tabOne = CreateDb(name);
        await SeedAsync(tabOne);
        var run = await AddGuessRunAsync(tabOne, answer: 1);
        var landed = false;
        tabOne.SavingChanges += (_, _) =>
        {
            if (landed)
                return;
            landed = true;
            using var tabTwo = CreateDb(name);
            var stored = tabTwo.GuessTheAnimeRuns.Single();
            stored.GuessedAnimeIds = [3];
            stored.GuessCount = 1;
            tabTwo.SaveChanges();
        };

        var end = await Make(tabOne).Service.EndAsync(run.Id);

        Assert.Equal(1, end.Answer.Row.AnimeId);
        using var check = CreateDb(name);
        Assert.Null(await StoredRunAsync(check));
    }

    // ---- what each ending leaves behind (5.6) ----

    [Theory]
    [InlineData("a win")]
    [InlineData("running out")]
    [InlineData("End game")]
    public async Task NoRunRowIsLeftAndTheAppearanceSurvives(string ending)
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var (service, _) = Make(db);
        var game = await service.StartAsync(AnyAnime);
        var answerId = (await StoredRunAsync(db))!.AnswerAnimeId;
        var wrong = Enumerable.Range(1, 13).Where(id => id != answerId).ToList();

        switch (ending)
        {
            case "a win":
                await service.GuessAsync(game.Id, 1, answerId);
                break;
            case "running out":
                for (var number = 1; number <= 10; number++)
                    await service.GuessAsync(game.Id, number, wrong[number - 1]);
                break;
            default:
                await service.EndAsync(game.Id);
                break;
        }

        Assert.Empty(db.GuessTheAnimeRuns);
        Assert.Equal([answerId], await db.GuessTheAnimeAppearances.AsNoTracking().Select(a => a.AnimeId).ToListAsync());
    }

    [Fact]
    public async Task AGuessAndAnEndLeaveHigherOrLowersGameAlone()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var higherOrLower = await AddRunAsync(db, 2, 3, round: 4, streak: 3);
        db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = 2 });
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1);
        var service = Make(db).Service;

        await service.GuessAsync(run.Id, 1, 4);
        await service.EndAsync(run.Id);

        var theirs = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal((higherOrLower.Id, 4, 3), (theirs.Id, theirs.Round, theirs.Streak));
        Assert.Equal([2], await db.HigherOrLowerAppearances.AsNoTracking().Select(a => a.AnimeId).ToListAsync());
    }

    [Fact]
    public async Task AGuessedAnimeIsNeverRecordedAsAnAppearance()
    {
        using var db = CreateDb();
        await SeedAsync(db);
        var run = await AddGuessRunAsync(db, answer: 1);
        var service = Make(db).Service;

        await service.GuessAsync(run.Id, 1, 2);
        await service.GuessAsync(run.Id, 2, 3);

        Assert.Empty(db.GuessTheAnimeAppearances);
    }
}
