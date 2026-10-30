using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.EntityFrameworkCore;
using static AnimeTracker.Api.Tests.Services.Games.HigherOrLowerTestKit;

namespace AnimeTracker.Api.Tests.Services.Games;

// add-games-higher-or-lower task 3.7, first half: HigherOrLowerService's reads,
// Start and End on InMemory. Picking and the rotation are in
// HigherOrLowerServicePickTests.
public class HigherOrLowerServiceTests
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private static HigherOrLowerService Service(AnimeTrackerDbContext db, int seed = 11) => new(db, new Random(seed));

    // ---- the read both pages open on ----

    [Fact]
    public async Task WithNoGameTheReadHasNoGame()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);

        var state = await Service(db).GetStateAsync(hideHentai: false);

        Assert.Null(state.Game);
    }

    [Fact]
    public async Task AGameInProgressIsInTheRead()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);
        var run = await AddRunAsync(db, 1, 2, round: 4, streak: 3, leftStays: 1);

        var game = (await Service(db).GetStateAsync(false)).Game;

        Assert.NotNull(game);
        Assert.Equal(run.Id, game.Id);
        Assert.Equal(4, game.Round);
        Assert.Equal(3, game.Streak);
        Assert.Equal(1, game.Left.AnimeId);
        Assert.Equal(2, game.Right.AnimeId);
    }

    [Fact]
    public async Task ACardCarriesWhatTheTableShows()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, mediaType: "movie", airedFrom: new DateOnly(2016, 8, 26)),
            Anime(2, mediaType: null, airedFrom: null));
        db.UserAnimeEntries.Add(OnList(1));
        await db.SaveChangesAsync();
        await AddRunAsync(db, 1, 2);

        var game = (await Service(db).GetStateAsync(false)).Game!;

        Assert.Equal(new HigherOrLowerCardDto(1, "Anime 1", "English 1", "https://img/1.jpg", "movie", 2016, true, null), game.Left);
        Assert.Equal(new HigherOrLowerCardDto(2, "Anime 2", "English 2", "https://img/2.jpg", null, null, false, null), game.Right);
    }

    [Fact]
    public async Task InRoundOneNeitherScoreIsInTheRead()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 8.4421), Anime(2, score: 6.3719));
        await db.SaveChangesAsync();
        await AddRunAsync(db, 1, 2, round: 1);

        var state = await Service(db).GetStateAsync(false);

        Assert.Null(state.Game!.Left.Score);
        Assert.Null(state.Game.Right.Score);
        var json = JsonSerializer.Serialize(state, Wire);
        Assert.DoesNotContain("8.4421", json);
        Assert.DoesNotContain("6.3719", json);
    }

    [Fact]
    public async Task FromRoundTwoTheLeftScoreIsInTheReadAndTheRightIsNot()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 8.4421), Anime(2, score: 6.3719));
        await db.SaveChangesAsync();
        await AddRunAsync(db, 1, 2, round: 2);

        var state = await Service(db).GetStateAsync(false);

        Assert.Equal(8.4421, state.Game!.Left.Score);
        Assert.Null(state.Game.Right.Score);
        Assert.DoesNotContain("6.3719", JsonSerializer.Serialize(state, Wire));
    }

    [Fact]
    public async Task TheYearsAreThoseOfTheEligibleAnime()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, airedFrom: new DateOnly(1989, 4, 1)),
            Anime(2, airedFrom: new DateOnly(2024, 10, 5)),
            Anime(3, airedFrom: null),
            Anime(4, airedFrom: new DateOnly(1950, 1, 1), score: null),         // no score: not eligible
            Anime(5, airedFrom: new DateOnly(2031, 1, 1), picture: null));      // no picture: not eligible
        await db.SaveChangesAsync();

        var years = (await Service(db).GetStateAsync(false)).Years;

        Assert.Equal(new HigherOrLowerYearsDto(1989, 2024), years);
    }

    [Fact]
    public async Task HideNsfwLeavesRxOutOfTheYears()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, airedFrom: new DateOnly(2000, 1, 1)),
            Anime(2, airedFrom: new DateOnly(1985, 1, 1), rating: "rx"));
        await db.SaveChangesAsync();

        Assert.Equal(new HigherOrLowerYearsDto(1985, 2000), (await Service(db).GetStateAsync(false)).Years);
        Assert.Equal(new HigherOrLowerYearsDto(2000, 2000), (await Service(db).GetStateAsync(true)).Years);
    }

    [Fact]
    public async Task WithNoKnownStartDateThereAreNoYears()
    {
        using var db = CreateDb();
        await SeedAsync(db, 3);

        Assert.Null((await Service(db).GetStateAsync(false)).Years);
    }

    [Fact]
    public async Task OnAnEmptyDatabaseTheReadIsEmptyRatherThanAFailure()
    {
        using var db = CreateDb();

        var state = await Service(db).GetStateAsync(false);

        Assert.Null(state.Game);
        Assert.Null(state.Years);
        Assert.Empty(state.Pictures);
    }

    // ---- the two posters ----

    [Fact]
    public async Task TheTileHasTwoDifferentPostersFromTheTwoHundredMostPopular()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 300; id++)
            db.AnimeMetadata.Add(Anime(id, rank: id));
        await db.SaveChangesAsync();

        var pictures = (await Service(db).GetStateAsync(false)).Pictures;

        Assert.Equal(2, pictures.Count);
        Assert.NotEqual(pictures[0], pictures[1]);
        Assert.All(pictures, p => Assert.InRange(int.Parse(p[(p.LastIndexOf('/') + 1)..^4]), 1, 200));
    }

    [Fact]
    public async Task ThePostersChangeFromOneReadToTheNext()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 300; id++)
            db.AnimeMetadata.Add(Anime(id, rank: id));
        await db.SaveChangesAsync();
        var service = Service(db);

        var seen = new HashSet<string>();
        for (var i = 0; i < 20; i++)
            seen.UnionWith((await service.GetStateAsync(false)).Pictures);

        Assert.True(seen.Count > 10, $"only {seen.Count} different posters over 20 reads");
    }

    [Fact]
    public async Task ThePostersFollowHideNsfw()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 1, rating: "rx"), Anime(2, rank: 2), Anime(3, rank: 3, rating: "rx"), Anime(4, rank: 4));
        await db.SaveChangesAsync();

        var hidden = (await Service(db).GetStateAsync(true)).Pictures;
        var shown = (await Service(db).GetStateAsync(false)).Pictures;

        Assert.Equal(["https://img/2.jpg", "https://img/4.jpg"], hidden.Order());
        Assert.Equal(2, shown.Count);
    }

    [Fact]
    public async Task ThePostersCarryNoScore()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 8.4421, rank: 1), Anime(2, score: 6.3719, rank: 2));
        await db.SaveChangesAsync();

        var json = JsonSerializer.Serialize(await Service(db).GetStateAsync(false), Wire);

        Assert.DoesNotContain("8.4421", json);
        Assert.DoesNotContain("6.3719", json);
    }

    [Fact]
    public async Task WithFewerThanTwoPopularPostersTheTileGetsWhatThereIs()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, rank: 1), Anime(2, rank: null), Anime(3, rank: 3, picture: null));
        await db.SaveChangesAsync();

        var pictures = (await Service(db).GetStateAsync(false)).Pictures;

        Assert.Equal(["https://img/1.jpg"], pictures);
    }

    // ---- the pool count ----

    [Fact]
    public async Task ThePoolCountsWhatTheSettingsLeave()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, mediaType: "tv", score: 7.1), Anime(2, mediaType: "tv", score: 7.2), Anime(3, mediaType: "movie", score: 7.3));
        await db.SaveChangesAsync();

        var all = await Service(db).GetPoolAsync(AnyAnime);
        var tv = await Service(db).GetPoolAsync(AnyAnime with { MediaTypes = ["tv"] });

        Assert.Equal(new HigherOrLowerPoolDto(3, true), all);
        Assert.Equal(new HigherOrLowerPoolDto(2, true), tv);
    }

    [Fact]
    public async Task OneAnimeIsCountedButNotPlayable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        Assert.Equal(new HigherOrLowerPoolDto(1, false), await Service(db).GetPoolAsync(AnyAnime));
    }

    [Fact]
    public async Task SeveralAnimeWithOneScoreAreCountedButNotPlayable()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 7.5), Anime(2, score: 7.5), Anime(3, score: 7.5));
        await db.SaveChangesAsync();

        Assert.Equal(new HigherOrLowerPoolDto(3, false), await Service(db).GetPoolAsync(AnyAnime));
    }

    [Fact]
    public async Task NothingLeftIsCountedAsZero()
    {
        using var db = CreateDb();

        Assert.Equal(new HigherOrLowerPoolDto(0, false), await Service(db).GetPoolAsync(AnyAnime));
    }

    [Fact]
    public async Task ThePoolRefusesSettingsThatCannotDescribeAGame()
    {
        using var db = CreateDb();

        await Assert.ThrowsAsync<HigherOrLowerSettingsException>(
            () => Service(db).GetPoolAsync(AnyAnime with { YearFrom = 2020, YearTo = 2010 }));
    }

    [Fact]
    public async Task ThePoolWritesNothing()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);

        await Service(db).GetPoolAsync(AnyAnime);
        await Service(db).GetStateAsync(false);

        Assert.Empty(await db.HigherOrLowerRuns.ToListAsync());
        Assert.Empty(await db.HigherOrLowerAppearances.ToListAsync());
    }

    // ---- Start ----

    [Fact]
    public async Task StartDrawsTwoDifferentAnimeAndStoresTheGame()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);

        var game = await Service(db).StartAsync(AnyAnime);

        Assert.Equal(1, game.Round);
        Assert.Equal(0, game.Streak);
        Assert.NotEqual(game.Left.AnimeId, game.Right.AnimeId);
        var run = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal(game.Id, run.Id);
        Assert.Equal(1, run.Round);
        Assert.Equal(0, run.Streak);
        Assert.Equal(0, run.LeftStays);
        Assert.Equal(game.Left.AnimeId, run.LeftAnimeId);
        Assert.Equal(game.Right.AnimeId, run.RightAnimeId);
        Assert.Equal([game.Left.AnimeId, game.Right.AnimeId], run.ShownAnimeIds);
    }

    [Fact]
    public async Task StartRecordsBothAnimeAsShown()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);

        var game = await Service(db).StartAsync(AnyAnime);

        var shown = await db.HigherOrLowerAppearances.AsNoTracking().OrderBy(a => a.Id).Select(a => a.AnimeId).ToListAsync();
        Assert.Equal([game.Left.AnimeId, game.Right.AnimeId], shown);
    }

    [Fact]
    public async Task StartSendsNoScoreAtAll()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 8.4421), Anime(2, score: 6.3719));
        await db.SaveChangesAsync();

        var game = await Service(db).StartAsync(AnyAnime);

        Assert.Null(game.Left.Score);
        Assert.Null(game.Right.Score);
        var json = JsonSerializer.Serialize(game, Wire);
        Assert.DoesNotContain("8.4421", json);
        Assert.DoesNotContain("6.3719", json);
    }

    [Fact]
    public async Task StartStoresTheSettingsTheGameStartedWith()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 6; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 10.0, mediaType: "tv", airedFrom: new DateOnly(2020, 5, 1)));
        await db.SaveChangesAsync();

        await Service(db).StartAsync(new HigherOrLowerSettings(2019, 2021, ["tv", "ova"], HigherOrLowerListScope.NotOnMyList, true));

        var run = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal(2019, run.YearFrom);
        Assert.Equal(2021, run.YearTo);
        Assert.Equal(["tv", "ova"], run.MediaTypes);
        Assert.Equal(HigherOrLowerListScope.NotOnMyList, run.ListScope);
        Assert.True(run.HideHentai);
    }

    [Fact]
    public async Task StartDrawsOnlyFromTheSettings()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 20; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 100.0, mediaType: id <= 4 ? "movie" : "tv"));
        await db.SaveChangesAsync();

        for (var seed = 0; seed < 25; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime with { MediaTypes = ["movie"] });
            Assert.InRange(game.Left.AnimeId, 1, 4);
            Assert.InRange(game.Right.AnimeId, 1, 4);
        }
    }

    [Fact]
    public async Task StartRefusesSettingsThatLeaveTooFewAnime()
    {
        using var db = CreateDb();
        db.AnimeMetadata.Add(Anime(1));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<HigherOrLowerNotPlayableException>(() => Service(db).StartAsync(AnyAnime));

        Assert.Empty(await db.HigherOrLowerRuns.ToListAsync());
        Assert.Empty(await db.HigherOrLowerAppearances.ToListAsync());
    }

    [Fact]
    public async Task StartRefusesAPoolWhereEveryScoreIsTheSame()
    {
        using var db = CreateDb();
        for (var id = 1; id <= 10; id++)
            db.AnimeMetadata.Add(Anime(id, score: 7.5));
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<HigherOrLowerNotPlayableException>(() => Service(db).StartAsync(AnyAnime));
    }

    [Fact]
    public async Task StartRefusesSettingsThatCannotDescribeAGame()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10);

        await Assert.ThrowsAsync<HigherOrLowerSettingsException>(
            () => Service(db).StartAsync(AnyAnime with { YearFrom = 2020, YearTo = 2010 }));

        Assert.Empty(await db.HigherOrLowerRuns.ToListAsync());
    }

    [Fact]
    public async Task ARefusedStartLeavesAGameInProgressAsItWas()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10);
        var run = await AddRunAsync(db, 1, 2, round: 3, streak: 2);

        await Assert.ThrowsAsync<HigherOrLowerNotPlayableException>(
            () => Service(db).StartAsync(AnyAnime with { MediaTypes = ["music"] }));

        var left = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal(run.Id, left.Id);
        Assert.Equal(3, left.Round);
        Assert.Equal(2, left.Streak);
    }

    [Fact]
    public async Task StartingReplacesAGameInProgress()
    {
        using var db = CreateDb();
        await SeedAsync(db, 10);
        var old = await AddRunAsync(db, 1, 2, round: 5, streak: 4);

        var game = await Service(db).StartAsync(AnyAnime);

        var run = await db.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal(game.Id, run.Id);
        Assert.NotEqual(old.Id, run.Id);
        Assert.Equal(1, run.Round);
        Assert.Equal(0, run.Streak);
    }

    [Fact]
    public async Task EveryStartBeginsWithTwoDifferentScores()
    {
        // Twenty anime sharing two scores: any pair drawn blind is the same score
        // about half the time, so the rule has to be doing the work.
        using var db = CreateDb();
        for (var id = 1; id <= 20; id++)
            db.AnimeMetadata.Add(Anime(id, score: id % 2 == 0 ? 7.00 : 8.00));
        await db.SaveChangesAsync();
        var scores = await db.AnimeMetadata.ToDictionaryAsync(a => a.Id, a => a.MalScore);

        for (var seed = 0; seed < 60; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime);
            Assert.NotEqual(scores[game.Left.AnimeId], scores[game.Right.AnimeId]);
        }
    }

    [Fact]
    public async Task TwoStartsWithTheSameSettingsDoNotBeginTheSameWay()
    {
        using var db = CreateDb();
        await SeedAsync(db, 200);

        var firsts = new HashSet<(int, int)>();
        for (var seed = 0; seed < 20; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime);
            firsts.Add((game.Left.AnimeId, game.Right.AnimeId));
        }

        Assert.True(firsts.Count > 10);
    }

    // ---- holding back what was shown, across games and settings ----

    [Fact]
    public async Task AnAnimeFromTheGameBeforeIsHeldBackInTheNextEvenWithOtherSettings()
    {
        using var db = CreateDb();
        // 1-20 are TV, 21-40 are movies. The last game, played with TV only,
        // ended on anime 1, shown most recently of all.
        for (var id = 1; id <= 40; id++)
            db.AnimeMetadata.Add(Anime(id, score: 5 + id / 100.0, mediaType: id <= 20 ? "tv" : "movie"));
        db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = 1 });
        await db.SaveChangesAsync();

        var timesOne = 0;
        var timesTwentyOne = 0;
        for (var seed = 0; seed < 300; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime with { List = HigherOrLowerListScope.All });
            foreach (var id in new[] { game.Left.AnimeId, game.Right.AnimeId })
            {
                if (id == 1) timesOne++;
                if (id == 21) timesTwentyOne++;
            }

            // Forget this game's own showings so each start meets the same history.
            db.HigherOrLowerAppearances.RemoveRange(
                await db.HigherOrLowerAppearances.Where(a => a.Id > 1).ToListAsync());
            await db.SaveChangesAsync();
        }

        // Anime 21 was never shown; 1 was shown last, so it all but cannot appear.
        Assert.True(timesTwentyOne > 5, $"anime 21 drawn only {timesTwentyOne} times");
        Assert.True(timesOne * 5 < timesTwentyOne, $"anime 1 drawn {timesOne} times, anime 21 {timesTwentyOne}");
    }

    [Fact]
    public async Task APoolEveryAnimeOfWhichWasShownRecentlyStillStarts()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);
        for (var id = 1; id <= 20; id++)
            db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = id });
        await db.SaveChangesAsync();

        var game = await Service(db).StartAsync(AnyAnime);

        Assert.NotEqual(game.Left.AnimeId, game.Right.AnimeId);
    }

    [Fact]
    public async Task TheNewestAppearancesAreHeldBackMostAndOlderOnesNotAtAll()
    {
        // 8 anime: the window is 2 cards. Anime 1 was shown long ago, 7 and 8 just now.
        using var db = CreateDb();
        await SeedAsync(db, 8);
        for (var id = 1; id <= 8; id++)
            db.HigherOrLowerAppearances.Add(new HigherOrLowerAppearance { AnimeId = id });
        await db.SaveChangesAsync();

        var counts = new int[9];
        for (var seed = 0; seed < 200; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime);
            counts[game.Left.AnimeId]++;
            counts[game.Right.AnimeId]++;
            db.HigherOrLowerAppearances.RemoveRange(await db.HigherOrLowerAppearances.Where(a => a.Id > 8).ToListAsync());
            await db.SaveChangesAsync();
        }

        Assert.True(counts[8] < counts[1] / 2, $"newest {counts[8]}, oldest {counts[1]}");
    }

    // ---- End ----

    [Fact]
    public async Task EndDeletesTheGameAndAnswersItsStreak()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);
        var run = await AddRunAsync(db, 1, 2, round: 5, streak: 4);

        var result = await Service(db).EndAsync(run.Id);

        Assert.Equal(new HigherOrLowerEndDto(4), result);
        Assert.Empty(await db.HigherOrLowerRuns.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task EndLeavesTheListOfAnimeShown()
    {
        using var db = CreateDb();
        var game = await StartAsync(db);
        var before = await db.HigherOrLowerAppearances.AsNoTracking().CountAsync();

        await Service(db).EndAsync(game.Id);

        Assert.Equal(2, before);
        Assert.Equal(before, await db.HigherOrLowerAppearances.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task EndingAGameThatIsNotInProgressIsNotFound()
    {
        using var db = CreateDb();
        await SeedAsync(db, 5);

        await Assert.ThrowsAsync<HigherOrLowerRunNotFoundException>(() => Service(db).EndAsync(99));
    }

    [Fact]
    public async Task EndingAGameThatWasReplacedIsNotFoundAndLeavesTheNewOne()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);
        var first = await Service(db).StartAsync(AnyAnime);
        var second = await Service(db, 12).StartAsync(AnyAnime);

        await Assert.ThrowsAsync<HigherOrLowerRunNotFoundException>(() => Service(db).EndAsync(first.Id));

        Assert.Equal(second.Id, (await db.HigherOrLowerRuns.AsNoTracking().SingleAsync()).Id);
    }

    [Fact]
    public async Task ReplacingAGameLeavesTheListOfAnimeShownFromBoth()
    {
        using var db = CreateDb();
        await SeedAsync(db, 20);
        await Service(db).StartAsync(AnyAnime);
        await Service(db, 12).StartAsync(AnyAnime);

        Assert.Equal(4, await db.HigherOrLowerAppearances.AsNoTracking().CountAsync());
        Assert.Single(await db.HigherOrLowerRuns.AsNoTracking().ToListAsync());
    }

    // ---- anime with no aired episode ----

    [Fact]
    public async Task ThePoolCountYearsAndStartAllLeaveOutAnimeNotYetAired()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, score: 6.0, airedFrom: new DateOnly(2020, 1, 1)),
            Anime(2, score: 7.0, airedFrom: new DateOnly(2021, 1, 1)),
            Anime(3, score: 8.0, airedFrom: new DateOnly(2031, 1, 1), airingStatus: "not_yet_aired"),
            Anime(4, score: 9.0, airedFrom: new DateOnly(2030, 1, 1), airingStatus: "not_yet_aired"));
        await db.SaveChangesAsync();
        var service = Service(db);

        Assert.Equal(new HigherOrLowerPoolDto(2, true), await service.GetPoolAsync(AnyAnime));
        Assert.Equal(new HigherOrLowerYearsDto(2020, 2021), (await service.GetStateAsync(false)).Years);
        for (var seed = 0; seed < 20; seed++)
        {
            var game = await Service(db, seed).StartAsync(AnyAnime);
            Assert.Equal([1, 2], new[] { game.Left.AnimeId, game.Right.AnimeId }.Order());
        }
    }

    [Fact]
    public async Task OnlyAnimeNotYetAiredLeftMeansNothingToPlay()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(
            Anime(1, score: 6.0), Anime(2, score: 7.0, airingStatus: "not_yet_aired"), Anime(3, score: 8.0, airingStatus: "not_yet_aired"));
        await db.SaveChangesAsync();

        Assert.Equal(new HigherOrLowerPoolDto(1, false), await Service(db).GetPoolAsync(AnyAnime));
        await Assert.ThrowsAsync<HigherOrLowerNotPlayableException>(() => Service(db).StartAsync(AnyAnime));
    }

    [Fact]
    public async Task AnAnimeNotYetAiredAccordingToMalButWithAnAiredEpisodeStillPlays()
    {
        using var db = CreateDb();
        db.AnimeMetadata.AddRange(Anime(1, score: 6.0), Anime(2, score: 7.0, airingStatus: "not_yet_aired"));
        db.EpisodeAirings.Add(new EpisodeAiring
        {
            AnimeId = 2, Episode = 1, AirsAtUtc = DateTimeOffset.UtcNow.AddDays(-1), FetchedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        Assert.Equal(new HigherOrLowerPoolDto(2, true), await Service(db).GetPoolAsync(AnyAnime));
    }

    // ---- two tabs ----

    [Fact]
    public async Task EndStillEndsTheGameWhenAPickMovedItOnJustBefore()
    {
        const string name = "end-after-a-pick";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        tabOne.AnimeMetadata.AddRange(Enumerable.Range(1, 10).Select(id => Anime(id, score: 5 + id / 10.0)));
        await tabOne.SaveChangesAsync();
        // Tab one holds the game at round 1 (AddRunAsync leaves it tracked). Tab two
        // then picks, moving the stored game to round 2 and streak 1.
        var run = await AddRunAsync(tabOne, 1, 2, round: 1, streak: 0);
        await Service(tabTwo).PickAsync(run.Id, 1, 2);

        var result = await Service(tabOne).EndAsync(run.Id);

        Assert.Equal(1, result.Streak);
        Assert.Empty(await tabTwo.HigherOrLowerRuns.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task StartStillReplacesTheGameWhenAPickMovedItOnJustBefore()
    {
        const string name = "start-after-a-pick";
        using var tabOne = CreateDb(name);
        using var tabTwo = CreateDb(name);
        tabOne.AnimeMetadata.AddRange(Enumerable.Range(1, 10).Select(id => Anime(id, score: 5 + id / 10.0)));
        await tabOne.SaveChangesAsync();
        var run = await AddRunAsync(tabOne, 1, 2, round: 1, streak: 0);
        await Service(tabTwo).PickAsync(run.Id, 1, 2);

        var game = await Service(tabOne).StartAsync(AnyAnime);

        var stored = await tabTwo.HigherOrLowerRuns.AsNoTracking().SingleAsync();
        Assert.Equal(game.Id, stored.Id);
        Assert.Equal(1, stored.Round);
    }

    private static async Task<HigherOrLowerGameDto> StartAsync(AnimeTrackerDbContext db)
    {
        await SeedAsync(db, 20);
        return await Service(db).StartAsync(AnyAnime);
    }
}
