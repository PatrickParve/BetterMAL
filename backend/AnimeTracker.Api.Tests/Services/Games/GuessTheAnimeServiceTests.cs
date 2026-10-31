using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Data.Repositories;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using AnimeTracker.Api.Services.Metadata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using static AnimeTracker.Api.Tests.Services.Games.GuessTheAnimeTestKit;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-guess-the-anime tasks 5.1-5.3 and 5.6: GuessTheAnimeService's reads and Start
// on InMemory, with a fake standing in for MyAnimeList. Guessing and ending are in
// GuessTheAnimeServiceGuessTests.
public class GuessTheAnimeServiceTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static readonly DateOnly Aired = new(2013, 4, 7);

    // The lobby reads share the service's Random, as they do in the app.
    internal static (GuessTheAnimeService Service, FakeGameDetails Details) Make(AnimeTrackerDbContext db, int seed = 11)
    {
        var random = new Random(seed);
        var details = new FakeGameDetails(db);
        var service = new GuessTheAnimeService(
            db, random, new GameLobbyReads(db, random), details, new EpisodeAiringRepository(db), NullLogger<GuessTheAnimeService>.Instance);
        return (service, details);
    }

    /// <summary>A weight so small beside rank 1's that a draw lands on it about
    /// once in ten thousand.</summary>
    private const int Obscure = 900_000;

    private static async Task<GuessTheAnimeRun> StoredRunAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheAnimeRuns.AsNoTracking().SingleAsync();

    private static async Task<List<int>> AppearancesAsync(AnimeTrackerDbContext db) =>
        await db.GuessTheAnimeAppearances.AsNoTracking().OrderBy(a => a.Id).Select(a => a.AnimeId).ToListAsync();

    // ---- the read both pages open on (5.1) ----

    [Fact]
    public async Task WithNoGameTheReadHasNoGame()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WholeAired(1));
        await db.SaveChangesAsync();

        var state = await Make(db).Service.GetStateAsync(hideHentai: false);

        Assert.Null(state.Game);
    }

    [Fact]
    public async Task AGameInProgressIsInTheReadWithItsRowsInTheOrderGuessed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WholeAired(1), WholeAired(2), WholeAired(3), WholeAired(4));
        await db.SaveChangesAsync();
        var run = await AddGuessRunAsync(db, answer: 1, guessed: [3, 2, 4]);

        var game = (await Make(db).Service.GetStateAsync(false)).Game;

        Assert.NotNull(game);
        Assert.Equal(run.Id, game.Id);
        Assert.Equal(10, game.MaxGuesses);
        Assert.Equal([3, 2, 4], game.Rows.Select(r => r.AnimeId));
    }

    [Fact]
    public async Task EachRowIsComparedWithTheAnswer()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Whole(1, score: 8.0, airedFrom: Aired, episodes: 24, genres: ["Action", "Drama"]),
            Whole(2, score: 7.0, airedFrom: Aired, episodes: 12, genres: ["Action", "Comedy"]));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 1, guessed: [2]);

        var row = (await Make(db).Service.GetStateAsync(false)).Game!.Rows.Single();

        Assert.Equal(GuessCompared.Same, row.Year.Compared);
        Assert.Equal(GuessCompared.AnswerHigher, row.Score.Compared);
        Assert.Equal(GuessCompared.AnswerHigher, row.Episodes.Compared);
        Assert.Equal([("Action", true), ("Comedy", false)], row.Genres.Select(g => (g.Name, g.Same)));
    }

    [Fact]
    public async Task ARowIsComparedAfreshOnEveryRead()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Whole(1, score: 8.0, airedFrom: Aired), Whole(2, score: 7.0, airedFrom: Aired));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 1, guessed: [2]);
        var service = Make(db).Service;
        Assert.Equal(
            GuessCompared.AnswerHigher, (await service.GetStateAsync(false)).Game!.Rows.Single().Score.Compared);

        // A refresh moves the answer's score below the guess's.
        (await db.AnimeMetadata.SingleAsync(a => a.Id == 1)).MalScore = 6.0;
        await db.SaveChangesAsync();

        Assert.Equal(
            GuessCompared.AnswerLower, (await service.GetStateAsync(false)).Game!.Rows.Single().Score.Compared);
    }

    [Fact]
    public async Task TheAnswerIsNowhereInTheRead()
    {
        using var db = CreateDb();
        var answer = Whole(7771, score: 9.37, airedFrom: new DateOnly(2021, 10, 3), episodes: 37,
            genres: ["Zeta"], studios: ["Zeta Studio"], rank: 1);
        // The years read covers every eligible anime, so its two ends are kept clear
        // of the answer's year here.
        db.AnimeMetadata.AddRange(
            answer, Whole(2, airedFrom: new DateOnly(2005, 1, 5)), Whole(3, airedFrom: new DateOnly(2024, 1, 5)));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 7771, guessed: [2, 3]);

        var state = await Make(db).Service.GetStateAsync(false);

        var json = JsonSerializer.Serialize(state, Wire);
        Assert.DoesNotContain("7771", json);
        Assert.DoesNotContain(answer.Title, json);
        Assert.DoesNotContain(answer.PictureUrl!, json);
        Assert.DoesNotContain("Zeta", json);
        Assert.DoesNotContain("9.37", json);
        Assert.DoesNotContain("2021", json);
    }

    [Fact]
    public async Task ThePostersLeaveOutTheAnswerEvenWhenItIsTheMostPopular()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Whole(1, rank: 1), Whole(2, rank: 2), Whole(3, rank: 3));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 1);

        for (var seed = 0; seed < 20; seed++)
        {
            var pictures = (await Make(db, seed).Service.GetStateAsync(false)).Pictures;

            Assert.Equal(2, pictures.Count);
            Assert.DoesNotContain("https://img/1.jpg", pictures);
        }
    }

    [Fact]
    public async Task ARowAroundAGuessedIdThatNoLongerExistsIsSkipped()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WholeAired(1), WholeAired(2), WholeAired(4));
        await db.SaveChangesAsync();
        await AddGuessRunAsync(db, answer: 1, guessed: [2, 3, 4]);

        var game = (await Make(db).Service.GetStateAsync(false)).Game!;

        Assert.Equal([2, 4], game.Rows.Select(r => r.AnimeId));
    }

    [Fact]
    public async Task TheReadCarriesTheYearsAndTwoPosters()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Whole(1, airedFrom: new DateOnly(1998, 4, 3)), Whole(2, airedFrom: new DateOnly(2024, 1, 5)),
            Whole(3, airedFrom: new DateOnly(2010, 7, 1)));
        await db.SaveChangesAsync();

        var state = await Make(db).Service.GetStateAsync(false);

        Assert.Equal(new GameYearsDto(1998, 2024), state.Years);
        Assert.Equal(2, state.Pictures.Count);
    }

    [Fact]
    public async Task AGameKeepsTheHideNsfwItStartedWith()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WholeAired(1));
        await db.SaveChangesAsync();
        var (service, _) = Make(db);

        var started = await service.StartAsync(AnyAnime with { HideHentai = true });
        var state = await service.GetStateAsync(hideHentai: false);

        Assert.Equal(started.Id, state.Game!.Id);
        Assert.True((await StoredRunAsync(db)).HideHentai);
    }

    // ---- the pool count (5.2) ----

    [Fact]
    public async Task ThePoolCountsTheAnimeTheSettingsLeave()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 6; id++)
            db.AnimeMetadata.Add(Anime(id, mediaType: id <= 4 ? "tv" : "movie"));
        await db.SaveChangesAsync();

        var pool = await Make(db).Service.GetPoolAsync(AnyAnime with { MediaTypes = ["tv"] });

        Assert.Equal(new GamePoolDto(4, true), pool);
    }

    [Fact]
    public async Task OneAnimeIsEnoughToPlay()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        Assert.Equal(new GamePoolDto(1, true), await Make(db).Service.GetPoolAsync(AnyAnime));
    }

    [Fact]
    public async Task NoAnimeIsNotPlayable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, score: null));
        await db.SaveChangesAsync();

        Assert.Equal(new GamePoolDto(0, false), await Make(db).Service.GetPoolAsync(AnyAnime));
    }

    [Fact]
    public async Task TheCountIsTheOneHigherOrLowerShowsForTheSameSettings()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 30; id++)
        {
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 100.0, mediaType: id % 3 == 0 ? "movie" : "tv",
                airedFrom: new DateOnly(2000 + id % 20, 5, 1), rating: id % 7 == 0 ? "rx" : "pg_13"));
            if (id % 5 == 0)
                db.UserAnimeEntries.Add(OnList(id));
        }

        await db.SaveChangesAsync();
        var random = new Random(1);
        var higherOrLower = new HigherOrLowerService(db, random, new GameLobbyReads(db, random));
        var settings = new GameSettings(2004, 2015, ["tv"], GameListScope.NotOnMyList, true);

        var theirs = await higherOrLower.GetPoolAsync(settings);
        var ours = await Make(db).Service.GetPoolAsync(settings);

        Assert.Equal(theirs.Count, ours.Count);
        Assert.True(ours.Count > 0);
    }

    [Fact]
    public async Task SettingsThatCannotDescribeAGameAreRefused()
    {
        using var db = CreateDb();
        var service = Make(db).Service;

        await Assert.ThrowsAsync<GameSettingsException>(() => service.GetPoolAsync(AnyAnime with { YearFrom = 2020, YearTo = 2010 }));
        await Assert.ThrowsAsync<GameSettingsException>(() => service.GetPoolAsync(AnyAnime with { List = (GameListScope)99 }));
        await Assert.ThrowsAsync<GameSettingsException>(() => service.StartAsync(AnyAnime with { YearFrom = 2020, YearTo = 2010 }));
    }

    // ---- starting a game: what is fetched (5.3) ----

    [Fact]
    public async Task AnAnimeHoldingEveryStatMakesNoFetch()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WholeAired(1));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);

        var game = await service.StartAsync(AnyAnime);

        Assert.Empty(details.Calls);
        Assert.Equal(10, game.MaxGuesses);
        Assert.Empty(game.Rows);
        var run = await StoredRunAsync(db);
        Assert.Equal(game.Id, run.Id);
        Assert.Equal(1, run.AnswerAnimeId);
        Assert.Equal(0, run.GuessCount);
        Assert.Empty(run.GuessedAnimeIds);
    }

    [Fact]
    public async Task AnAnimeMissingAStatIsFetchedAndIsTheAnswer()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Lean(1));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);

        await service.StartAsync(AnyAnime);

        Assert.Equal([1], details.Calls);
        Assert.Equal(1, (await StoredRunAsync(db)).AnswerAnimeId);
        Assert.Equal(["Action"], (await db.AnimeMetadata.AsNoTracking().SingleAsync()).Genres);
    }

    [Fact]
    public async Task StartStoresTheSettingsTheGameStartedWith()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WholeAired(1));
        await db.SaveChangesAsync();

        await Make(db).Service.StartAsync(new GameSettings(2013, 2013, ["tv", "ova"], GameListScope.NotOnMyList, true));

        var run = await StoredRunAsync(db);
        Assert.Equal(2013, run.YearFrom);
        Assert.Equal(2013, run.YearTo);
        Assert.Equal(["tv", "ova"], run.MediaTypes);
        Assert.Equal(GameListScope.NotOnMyList, run.ListScope);
        Assert.True(run.HideHentai);
    }

    [Fact]
    public async Task AFetchShowingRxUnderHideNsfwPassesTheAnimeOver()
    {
        using var db = CreateDb();
        // No stored rating, so it passes the pool; its fetch shows it is rx.
        db.AnimeMetadata.AddRange(Lean(1, rank: 1), Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Fills[1] = a => a.Rating = "rx";

        await service.StartAsync(AnyAnime with { HideHentai = true });

        Assert.Equal([1], details.Calls);
        Assert.Equal(2, (await StoredRunAsync(db)).AnswerAnimeId);
        Assert.Equal([2], await AppearancesAsync(db));
    }

    [Fact]
    public async Task RxIsFineWhenHideNsfwIsOff()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1, rank: 1), Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Fills[1] = a => a.Rating = "rx";

        await service.StartAsync(AnyAnime);

        Assert.Equal(1, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task AFetchShowingNotYetAiredPassesTheAnimeOver()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1, rank: 1), Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Fills[1] = a => a.AiringStatus = "not_yet_aired";

        await service.StartAsync(AnyAnime);

        Assert.Equal([1], details.Calls);
        Assert.Equal(2, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task AFailedFetchOfANeverFetchedAnimeFallsBackToFetchedAnimeWithNoError()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1, rank: 1), Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Failures[1] = new HttpRequestException("MAL is down");

        await service.StartAsync(AnyAnime);

        Assert.Equal([1], details.Calls);
        Assert.Equal(2, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task ANotFoundFetchFallsBackToo()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1, rank: 1), Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Failures[1] = new AnimeMetadataNotFoundException(1);

        await service.StartAsync(AnyAnime);

        Assert.Equal(2, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task AFailedFetchOfAnAnimeFetchedBeforeKeepsItAsTheAnswer()
    {
        using var db = CreateDb();
        var fetchedBefore = Whole(1, airedFrom: Aired, rank: 1);
        fetchedBefore.Genres = null;
        db.AnimeMetadata.AddRange(fetchedBefore, Whole(2, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Failures[1] = new HttpRequestException("MAL is down");

        await service.StartAsync(AnyAnime);

        Assert.Equal([1], details.Calls);
        Assert.Equal(1, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task ThreeFetchesAtMostAreMadeBeforeSettlingForFetchedAnime()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 6; id++)
            db.AnimeMetadata.Add(Lean(id, rank: 1));
        db.AnimeMetadata.Add(Whole(7, airedFrom: Aired, rank: Obscure));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        for (var id = 1; id <= 6; id++)
            details.Fills[id] = a => a.Rating = "rx";

        await service.StartAsync(AnyAnime with { HideHentai = true });

        Assert.Equal(3, details.Calls.Count);
        Assert.Equal(7, (await StoredRunAsync(db)).AnswerAnimeId);
    }

    [Fact]
    public async Task NothingFetchedLeftIsARefusalThatSaysWhy()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1), Lean(2));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);
        details.Failures[1] = new HttpRequestException("MAL is down");
        details.Failures[2] = new HttpRequestException("MAL is down");

        var refused = await Assert.ThrowsAsync<GuessTheAnimeNoAnswerException>(() => service.StartAsync(AnyAnime));

        Assert.Contains("couldn't be reached", refused.Message);
        Assert.Single(details.Calls);
        Assert.Empty(db.GuessTheAnimeRuns);
        Assert.Empty(db.GuessTheAnimeAppearances);
    }

    [Fact]
    public async Task ARefusedStartLeavesAGameInProgressAsItWas()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Lean(1), Lean(2));
        await db.SaveChangesAsync();
        var inProgress = await AddGuessRunAsync(db, answer: 2, guessed: [1]);
        var (service, details) = Make(db);
        details.Failures[1] = new HttpRequestException("MAL is down");
        details.Failures[2] = new HttpRequestException("MAL is down");

        await Assert.ThrowsAsync<GuessTheAnimeNoAnswerException>(() => service.StartAsync(AnyAnime));
        await Assert.ThrowsAsync<GuessTheAnimeNotPlayableException>(() => service.StartAsync(AnyAnime with { MediaTypes = ["movie"] }));

        var run = await StoredRunAsync(db);
        Assert.Equal(inProgress.Id, run.Id);
        Assert.Equal(2, run.AnswerAnimeId);
        Assert.Equal([1], run.GuessedAnimeIds);
    }

    [Fact]
    public async Task AnEmptyPoolIsNotPlayable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1, score: null));
        await db.SaveChangesAsync();
        var (service, details) = Make(db);

        await Assert.ThrowsAsync<GuessTheAnimeNotPlayableException>(() => service.StartAsync(AnyAnime));

        Assert.Empty(details.Calls);
        Assert.Empty(db.GuessTheAnimeRuns);
    }

    [Fact]
    public async Task AStartedGameReplacesOneInProgress()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WholeAired(1), WholeAired(2), WholeAired(3));
        await db.SaveChangesAsync();
        var old = await AddGuessRunAsync(db, answer: 1, guessed: [2]);

        var game = await Make(db).Service.StartAsync(AnyAnime);

        var run = await StoredRunAsync(db);
        Assert.Equal(game.Id, run.Id);
        Assert.NotEqual(old.Id, run.Id);
        Assert.Empty(run.GuessedAnimeIds);
    }

    [Fact]
    public async Task AnAppearanceIsWrittenForEachAnswerDrawn()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WholeAired(1), WholeAired(2), WholeAired(3));
        await db.SaveChangesAsync();
        var (service, _) = Make(db);

        await service.StartAsync(AnyAnime);
        var first = (await StoredRunAsync(db)).AnswerAnimeId;
        await service.StartAsync(AnyAnime);
        var second = (await StoredRunAsync(db)).AnswerAnimeId;

        Assert.Equal([first, second], await AppearancesAsync(db));
    }

    // ---- starting a game: what is drawn (5.3) ----

    [Fact]
    public async Task ARecentAnswerIsHeldBackAcrossSettings()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 40; id++)
            db.AnimeMetadata.Add(Whole(id, airedFrom: Aired, mediaType: id % 2 == 0 ? "tv" : "movie", rank: 10));
        await db.SaveChangesAsync();
        var (service, _) = Make(db, seed: 5);
        var settings = new[]
        {
            AnyAnime,
            AnyAnime with { MediaTypes = ["tv", "movie"] },
            AnyAnime with { YearFrom = 2013, YearTo = 2013 },
        };

        var last = 0;
        for (var game = 0; game < 30; game++)
        {
            await service.StartAsync(settings[game % settings.Length]);
            var answer = (await StoredRunAsync(db)).AnswerAnimeId;

            Assert.NotEqual(last, answer);
            last = answer;
        }
    }

    [Fact]
    public async Task AWeightComesBackOnceEnoughGamesHavePassed()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(WholeAired(1), WholeAired(2));
        await db.SaveChangesAsync();
        var (service, _) = Make(db, seed: 3);

        var answers = new HashSet<int>();
        for (var game = 0; game < 12; game++)
        {
            await service.StartAsync(AnyAnime);
            answers.Add((await StoredRunAsync(db)).AnswerAnimeId);
        }

        // Each answer was held back only for a while, so both come round again.
        Assert.Equal([1, 2], answers.OrderBy(a => a));
    }

    [Fact]
    public async Task ASmallPoolStillStartsWithTheSameAnswer()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(WholeAired(1));
        await db.SaveChangesAsync();
        var (service, _) = Make(db);

        await service.StartAsync(AnyAnime);
        await service.StartAsync(AnyAnime);

        Assert.Equal(1, (await StoredRunAsync(db)).AnswerAnimeId);
        Assert.Equal([1, 1], await AppearancesAsync(db));
    }

    [Fact]
    public async Task HigherOrLowersListOfShownAnimeHasNoEffect()
    {
        // Anime 1 was the latest card shown in Higher or Lower. If that held it
        // back here it would be drawn about one game in five, not one in two.
        var drawnOne = 0;
        for (var trial = 0; trial < 200; trial++)
        {
            using var db = CreateDb();
            db.AnimeMetadata.AddRange(Whole(1, airedFrom: Aired, rank: 10), Whole(2, airedFrom: Aired, rank: 10));
            db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = 1 });
            await db.SaveChangesAsync();

            await Make(db, seed: trial).Service.StartAsync(AnyAnime);

            if ((await StoredRunAsync(db)).AnswerAnimeId == 1)
                drawnOne++;
        }

        Assert.InRange(drawnOne, 70, 130);
    }

    [Fact]
    public async Task AnAnswerHereHoldsNothingBackInHigherOrLower()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 8; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 10.0));
        db.AnimeMetadata.Add(WholeAired(9));
        await db.SaveChangesAsync();
        await Make(db).Service.StartAsync(AnyAnime with { MediaTypes = ["tv"] });

        Assert.Empty(db.HigherOrLowerAppearances);
    }

    [Fact]
    public async Task AnAnimeOnMyListIsDrawnHoweverUnpopular()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Whole(1, airedFrom: Aired, rank: 20_000), Whole(2, airedFrom: Aired, rank: 500));
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();

        var drawnOne = 0;
        for (var seed = 0; seed < 200; seed++)
        {
            db.GuessTheAnimeAppearances.RemoveRange(db.GuessTheAnimeAppearances);
            await db.SaveChangesAsync();
            await Make(db, seed).Service.StartAsync(AnyAnime);
            if ((await StoredRunAsync(db)).AnswerAnimeId == 1)
                drawnOne++;
        }

        Assert.InRange(drawnOne, 70, 130);
    }

    [Fact]
    public async Task AGameOfHigherOrLowerIsUntouchedByThisGamesStart()
    {
        using var db = CreateDb();
        await SeedAsync(db, 6);
        var higherOrLower = await AddRunAsync(db, 1, 2, round: 3, streak: 2);
        db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = 1 });
        await db.SaveChangesAsync();
        var (service, _) = Make(db);

        var game = await service.StartAsync(AnyAnime);
        await service.EndAsync(game.Id);

        var run = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal((higherOrLower.Id, 3, 2), (run.Id, run.Round, run.Streak));
        Assert.Equal([1], await db.HigherOrLowerAppearances.AsNoTracking().Select(a => a.AnimeId).ToListAsync());
    }
}
