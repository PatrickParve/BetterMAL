using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.AnimeThemes;
using AnimeTracker.Api.Services.Games;
using AnimeTracker.Api.Tests.Services.Games;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Controllers;

// add-anime-themes-and-guess-the-theme task 7.6: each route's success and each refusal, the
// enums as strings, repeated `types` query values, and the clip's headers. Query binding and
// JSON enums only happen in a real pipeline, so these requests go over HTTP to the controller
// alone, hosted on Kestrel in the process, with a fake service and the real hold over a fake relay. The
// host mirrors Program.cs's one JSON option (enums as strings).
public class GuessTheThemeControllerTests
{
    private static readonly GuessTheThemeCardDto Card = new(7, "Card title", "Card english", "https://img/7.jpg");

    private static readonly GuessTheThemeRevealDto Reveal = new(
        9, "Reveal title", "Reveal english", "https://img/9.jpg", AnimeThemeKind.Ending, "ED2",
        "Silhouette", ["KANA-BOON"], "https://v.animethemes.moe/9.webm", true, 3.0, "/api/games/guess-the-theme/video?anime=9&theme=91");

    private static GuessTheThemeGameDto Game(
        GuessTheThemePhase phase = GuessTheThemePhase.Listening, GuessTheThemeRevealDto? reveal = null, int? next = null) =>
        new(3, 4, GuessTheThemeLadder.Seconds, 5, 2, phase, reveal, next, [7, 8], [Card]);

    // ---- GET api/games/guess-the-theme ----

    [Fact]
    public async Task TheStateRouteAnswersTheGameTheYearsThePostersAndWhetherTheCatalogueLoaded()
    {
        var fake = new FakeService
        {
            State = new GuessTheThemeStateDto(Game(), new GameYearsDto(1963, 2026), ["https://img/1.jpg", "https://img/2.jpg"], true),
        };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/guess-the-theme");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await Json(response);
        var root = json.RootElement;
        var game = root.GetProperty("game");
        Assert.Equal(3, game.GetProperty("id").GetInt32());
        Assert.Equal(4, game.GetProperty("move").GetInt32());
        Assert.Equal(5, game.GetProperty("round").GetInt32());
        Assert.Equal(2, game.GetProperty("step").GetInt32());
        Assert.Equal(6, game.GetProperty("lengths").GetArrayLength());
        Assert.Equal(0.1, game.GetProperty("lengths")[0].GetDouble());
        Assert.Equal(10, game.GetProperty("lengths")[5].GetDouble());
        Assert.Equal([7, 8], game.GetProperty("guessedIds").EnumerateArray().Select(e => e.GetInt32()));
        Assert.Equal(7, game.GetProperty("guessed")[0].GetProperty("animeId").GetInt32());
        Assert.Equal("Card title", game.GetProperty("guessed")[0].GetProperty("title").GetString());
        Assert.Equal("Card english", game.GetProperty("guessed")[0].GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/7.jpg", game.GetProperty("guessed")[0].GetProperty("pictureUrl").GetString());
        Assert.Equal(1963, root.GetProperty("years").GetProperty("min").GetInt32());
        Assert.Equal(2026, root.GetProperty("years").GetProperty("max").GetInt32());
        Assert.Equal(2, root.GetProperty("pictures").GetArrayLength());
        Assert.True(root.GetProperty("catalogueReady").GetBoolean());
    }

    [Fact]
    public async Task ARoundHasItsPhaseAsAStringAndNoRevealOrNextRound()
    {
        var fake = new FakeService { State = new GuessTheThemeStateDto(Game(), null, [], false) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/guess-the-theme"));

        var game = json.RootElement.GetProperty("game");
        Assert.Equal("Listening", game.GetProperty("phase").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("reveal").ValueKind);
        Assert.Equal(JsonValueKind.Null, game.GetProperty("nextRound").ValueKind);
    }

    [Fact]
    public async Task ARevealIsNamedByTheFieldsTheClientReadsWithItsEnumsAsStrings()
    {
        var fake = new FakeService
        {
            State = new GuessTheThemeStateDto(Game(GuessTheThemePhase.Revealed, Reveal, next: 6), null, [], true),
        };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/guess-the-theme"));

        var game = json.RootElement.GetProperty("game");
        Assert.Equal("Revealed", game.GetProperty("phase").GetString());
        Assert.Equal(6, game.GetProperty("nextRound").GetInt32());
        var reveal = game.GetProperty("reveal");
        Assert.Equal(9, reveal.GetProperty("animeId").GetInt32());
        Assert.Equal("Reveal title", reveal.GetProperty("title").GetString());
        Assert.Equal("Reveal english", reveal.GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/9.jpg", reveal.GetProperty("pictureUrl").GetString());
        Assert.Equal("Ending", reveal.GetProperty("kind").GetString());
        Assert.Equal("ED2", reveal.GetProperty("label").GetString());
        Assert.Equal("Silhouette", reveal.GetProperty("songTitle").GetString());
        Assert.Equal("KANA-BOON", reveal.GetProperty("artists")[0].GetString());
        Assert.Equal("https://v.animethemes.moe/9.webm", reveal.GetProperty("videoUrl").GetString());
        Assert.Equal("/api/games/guess-the-theme/video?anime=9&theme=91", reveal.GetProperty("playUrl").GetString());
        Assert.True(reveal.GetProperty("inMyList").GetBoolean());
        Assert.Equal(3.0, reveal.GetProperty("heardSeconds").GetDouble());
    }

    [Fact]
    public async Task WithNoGameTheStateHasANullGameAndNullYears()
    {
        var fake = new FakeService { State = new GuessTheThemeStateDto(null, null, [], false) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/guess-the-theme"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("game").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("years").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("pictures").GetArrayLength());
        Assert.False(json.RootElement.GetProperty("catalogueReady").GetBoolean());
    }

    [Theory]
    [InlineData("api/games/guess-the-theme?hideHentai=true", true)]
    [InlineData("api/games/guess-the-theme?hideHentai=false", false)]
    [InlineData("api/games/guess-the-theme", false)]
    public async Task TheStateRoutePassesHideHentaiThrough(string url, bool expected)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync(url);

        Assert.Equal(expected, fake.StateHideHentai);
    }

    // ---- GET api/games/guess-the-theme/pool ----

    [Fact]
    public async Task ThePoolRouteBindsEverySettingFromTheQuery()
    {
        var fake = new FakeService { Pool = new GamePoolDto(1234, true) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync(
            "api/games/guess-the-theme/pool?yearFrom=2010&yearTo=2015&types=tv&types=movie&list=OnlyMyList&themes=Endings&hideHentai=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.PoolSettings!;
        Assert.Equal(2010, settings.YearFrom);
        Assert.Equal(2015, settings.YearTo);
        Assert.Equal(["tv", "movie"], settings.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, settings.List);
        Assert.Equal(GameThemeChoice.Endings, settings.Themes);
        Assert.True(settings.HideHentai);
        using var json = await Json(response);
        Assert.Equal(1234, json.RootElement.GetProperty("count").GetInt32());
        Assert.True(json.RootElement.GetProperty("playable").GetBoolean());
    }

    [Fact]
    public async Task RepeatedTypesValuesAreEachOneType()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/games/guess-the-theme/pool?types=ova&types=special&types=ona&types=tv");

        Assert.Equal(["ova", "special", "ona", "tv"], fake.PoolSettings!.MediaTypes);
    }

    [Fact]
    public async Task WithNoSettingsInTheQueryThePoolIsEverySong()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/games/guess-the-theme/pool");

        var settings = fake.PoolSettings!;
        Assert.Null(settings.YearFrom);
        Assert.Null(settings.YearTo);
        Assert.True(settings.MediaTypes is null or { Count: 0 });
        Assert.Equal(GameListScope.All, settings.List);
        Assert.Equal(GameThemeChoice.All, settings.Themes);
        Assert.False(settings.HideHentai);
    }

    [Theory]
    [InlineData("All", GameThemeChoice.All)]
    [InlineData("Openings", GameThemeChoice.Openings)]
    [InlineData("Endings", GameThemeChoice.Endings)]
    public async Task TheThemeSettingIsReadByName(string value, GameThemeChoice expected)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync($"api/games/guess-the-theme/pool?themes={value}");

        Assert.Equal(expected, fake.PoolSettings!.Themes);
    }

    [Theory]
    [InlineData("themes=Banana")]
    [InlineData("list=Banana")]
    public async Task AnUnknownNameInThePoolQueryIsABadRequest(string query)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync($"api/games/guess-the-theme/pool?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.PoolSettings);
    }

    [Fact]
    public async Task ThePoolRouteAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("The release years run backwards: 2020 is after 2010.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/pool?yearFrom=2020&yearTo=2010");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The release years run backwards: 2020 is after 2010.", await ErrorAsync(response));
    }

    // ---- POST api/games/guess-the-theme/runs ----

    [Fact]
    public async Task StartBindsTheSettingsFromTheBodyAndAnswersTheGame()
    {
        var fake = new FakeService { Started = Game() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs", new
        {
            yearFrom = 2010,
            yearTo = 2015,
            mediaTypes = new[] { "tv", "movie" },
            list = "NotOnMyList",
            hideHentai = true,
            themes = "Openings",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.StartSettings!;
        Assert.Equal(2010, settings.YearFrom);
        Assert.Equal(2015, settings.YearTo);
        Assert.Equal(["tv", "movie"], settings.MediaTypes);
        Assert.Equal(GameListScope.NotOnMyList, settings.List);
        Assert.True(settings.HideHentai);
        Assert.Equal(GameThemeChoice.Openings, settings.Themes);
        using var json = await Json(response);
        Assert.Equal(3, json.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("Listening", json.RootElement.GetProperty("phase").GetString());
    }

    [Fact]
    public async Task StartTakesNullsAsAnyYearAndEveryType()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs", new
        {
            yearFrom = (int?)null,
            yearTo = (int?)null,
            mediaTypes = (string[]?)null,
            list = "All",
            hideHentai = false,
            themes = "All",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.StartSettings!;
        Assert.Null(settings.YearFrom);
        Assert.Null(settings.YearTo);
        Assert.Null(settings.MediaTypes);
        Assert.Equal(GameThemeChoice.All, settings.Themes);
    }

    [Fact]
    public async Task StartWithNoThemeIsAllThemes()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs", new { list = "All" });

        Assert.Equal(GameThemeChoice.All, fake.StartSettings!.Themes);
    }

    [Theory]
    [InlineData("themes")]
    [InlineData("list")]
    public async Task StartRefusesAnUnknownNameWithoutCallingTheService(string field)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/guess-the-theme/runs",
            new StringContent($$"""{"{{field}}":"Banana"}""", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartRefusesAnEmptyBody()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/guess-the-theme/runs", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("0 is not a release year.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs", new { yearFrom = 0, list = "All" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("0 is not a release year.", await ErrorAsync(response));
    }

    [Fact]
    public async Task StartAnswersConflictWhenTheSettingsLeaveNoSong()
    {
        var fake = new FakeService { Throw = new GuessTheThemeNotPlayableException() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs", new { list = "All" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("There aren't enough songs for these settings.", await ErrorAsync(response));
    }

    // ---- POST .../guesses, .../skips ----

    [Fact]
    public async Task AGuessPassesTheGameTheMoveAndTheAnimeThrough()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/guesses", new { move = 5, animeId = 9 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 5, 9), fake.GuessCall);
    }

    [Fact]
    public async Task AWrongGuessAnswersItsOutcomeAsAStringAndTheGame()
    {
        var fake = new FakeService { Moved = new GuessTheThemeMoveDto(GuessTheThemeOutcome.Wrong, Game(), null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/guess-the-theme/runs/3/guesses", new { move = 4, animeId = 7 }));

        var root = json.RootElement;
        Assert.Equal("Wrong", root.GetProperty("outcome").GetString());
        Assert.Equal(3, root.GetProperty("game").GetProperty("id").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("reveal").ValueKind);
    }

    [Theory]
    [InlineData(GuessTheThemeOutcome.Skipped, "Skipped")]
    [InlineData(GuessTheThemeOutcome.Right, "Right")]
    [InlineData(GuessTheThemeOutcome.Lost, "Lost")]
    [InlineData(GuessTheThemeOutcome.Cleared, "Cleared")]
    public async Task EveryOutcomeIsItsNameOnTheWire(GuessTheThemeOutcome outcome, string expected)
    {
        var fake = new FakeService { Moved = new GuessTheThemeMoveDto(outcome, null, Reveal) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/guess-the-theme/runs/3/skips", new { move = 4 }));

        Assert.Equal(expected, json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("game").ValueKind);
        Assert.Equal(9, json.RootElement.GetProperty("reveal").GetProperty("animeId").GetInt32());
    }

    [Fact]
    public async Task ASkipPassesTheGameAndTheMoveThrough()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/skips", new { move = 8 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 8), fake.SkipCall);
    }

    [Fact]
    public async Task AGuessOfAnAnimeAlreadyGuessedIsABadRequest()
    {
        var fake = new FakeService { Throw = new GuessTheThemeAlreadyGuessedException(55) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/3/guesses", new { move = 2, animeId = 55 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Anime 55 has already been guessed this round.", await ErrorAsync(response));
    }

    [Theory]
    [InlineData("guesses", "{\"move\":2,\"animeId\":7}")]
    [InlineData("skips", "{\"move\":2}")]
    [InlineData("next", "{\"move\":2}")]
    [InlineData("replace", "{\"round\":2}")]
    public async Task AMoveForAGameNoLongerInProgressIsNotFound(string route, string body)
    {
        var fake = new FakeService { Throw = new GuessTheThemeRunNotFoundException(3) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            $"api/games/guess-the-theme/runs/3/{route}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 3 is not in progress.", await ErrorAsync(response));
    }

    [Theory]
    [InlineData("guesses", "{\"move\":2,\"animeId\":7}")]
    [InlineData("skips", "{\"move\":2}")]
    [InlineData("next", "{\"move\":2}")]
    public async Task AMoveTheGameHasPassedIsAConflict(string route, string body)
    {
        var fake = new FakeService { Throw = new GuessTheThemeStaleMoveException(2) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            $"api/games/guess-the-theme/runs/3/{route}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Move 2 is no longer the current move.", await ErrorAsync(response));
    }

    [Theory]
    [InlineData("guesses", "{\"move\":2,\"animeId\":7}")]
    [InlineData("skips", "{\"move\":2}")]
    [InlineData("next", "{\"move\":2}")]
    public async Task AMoveOutOfItsPhaseIsAConflict(string route, string body)
    {
        var fake = new FakeService { Throw = new GuessTheThemeWrongPhaseException("This song has been revealed.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            $"api/games/guess-the-theme/runs/3/{route}", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("This song has been revealed.", await ErrorAsync(response));
    }

    [Theory]
    [InlineData("guesses")]
    [InlineData("skips")]
    [InlineData("next")]
    [InlineData("replace")]
    public async Task AMoveWithNoBodyIsABadRequestAndNeverReachesTheService(string route)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            $"api/games/guess-the-theme/runs/3/{route}", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.GuessCall);
        Assert.Null(fake.SkipCall);
        Assert.Null(fake.NextCall);
        Assert.Null(fake.ReplaceCall);
    }

    [Fact]
    public async Task AGameIdThatIsNotANumberMatchesNoRoute()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/abc/guesses", new { move = 1, animeId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(fake.GuessCall);
    }

    // ---- POST .../next ----

    [Fact]
    public async Task NextPassesTheGameAndTheMoveThroughAndAnswersTheGame()
    {
        var fake = new FakeService { Next = Game() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/next", new { move = 6 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 6), fake.NextCall);
        using var json = await Json(response);
        Assert.Equal(5, json.RootElement.GetProperty("round").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("step").GetInt32());
    }

    // ---- POST .../end ----

    [Fact]
    public async Task EndAnswersTheSongWhenItEndedARound()
    {
        var fake = new FakeService { Ended = new GuessTheThemeEndDto(Reveal) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/guess-the-theme/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(12, fake.EndedId);
        using var json = await Json(response);
        Assert.Equal(9, json.RootElement.GetProperty("reveal").GetProperty("animeId").GetInt32());
    }

    [Fact]
    public async Task EndAnswersNoSongWhenItEndedAReveal()
    {
        var fake = new FakeService { Ended = new GuessTheThemeEndDto(null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsync("api/games/guess-the-theme/runs/12/end", content: null));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("reveal").ValueKind);
    }

    [Fact]
    public async Task EndingAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new GuessTheThemeRunNotFoundException(12) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/guess-the-theme/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 12 is not in progress.", await ErrorAsync(response));
    }

    // ---- POST .../replace ----

    [Fact]
    public async Task ReplacePassesTheGameAndTheRoundThroughAndAnswersTheGame()
    {
        var fake = new FakeService { Replaced = new GuessTheThemeReplaceDto(null, Game()) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/replace", new { round = 5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 5), fake.ReplaceCall);
        using var json = await Json(response);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("outcome").ValueKind);
        Assert.Equal(3, json.RootElement.GetProperty("game").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task ReplacingWithNoSongLeftAnswersClearedAndNoGame()
    {
        var fake = new FakeService { Replaced = new GuessTheThemeReplaceDto(GuessTheThemeOutcome.Cleared, null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/replace", new { round = 5 }));

        Assert.Equal("Cleared", json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("game").ValueKind);
    }

    [Fact]
    public async Task ReplacingASongTheGameDoesNotHoldIsNotFound()
    {
        var fake = new FakeService { Throw = new GuessTheThemeRoundNotFoundException(9) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/replace", new { round = 9 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("This game has no song 9 to play.", await ErrorAsync(response));
    }

    [Fact]
    public async Task ReplacingASongWhoseFileIsStillThereIsAConflict()
    {
        var fake = new FakeService { Throw = new GuessTheThemeFileStillThereException() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/replace", new { round = 5 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The song's file is still there.", await ErrorAsync(response));
    }

    [Fact]
    public async Task ReplacingWhenAnimeThemesDidNotAnswerIsABadGatewayWithTheReason()
    {
        var fake = new FakeService { Throw = new GuessTheThemeCannotCheckException() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/runs/12/replace", new { round = 5 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("AnimeThemes didn't answer, so the song couldn't be checked.", await ErrorAsync(response));
    }

    // ---- GET .../clip ----

    [Fact]
    public async Task TheClipIsAudioOggTheBytesAndNothingElse()
    {
        var bytes = Enumerable.Range(0, 5000).Select(i => (byte)(i % 251)).ToArray();
        var fake = new FakeService { ClipSource = "https://a.animethemes.moe/DeathNote-OP1.ogg" };
        var relay = new FakeMediaRelay { Bytes = bytes };
        await using var host = await TestHost.StartAsync(fake, relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 5), fake.ClipCall);
        Assert.Equal(["https://a.animethemes.moe/DeathNote-OP1.ogg"], relay.Opened);
        Assert.Equal(bytes, await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TheClipIsServedAsAudioOggAndToldNotToBeStored()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay());

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal("audio/ogg", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(response.Content.Headers.Contains("Content-Disposition"));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task TheClipNeverNamesTheShowOrTheFile()
    {
        var fake = new FakeService { ClipSource = "https://a.animethemes.moe/DeathNote-OP1.ogg" };
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay());

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        var headers = string.Join("\n", response.Headers.Concat(response.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        Assert.DoesNotContain("DeathNote", headers);
        Assert.DoesNotContain("animethemes", headers, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("filename", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheClipRouteAddressesTheRoundByTheQueryAlone()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay());

        await host.Client.GetAsync("api/games/guess-the-theme/runs/7/clip?round=11");

        Assert.Equal((7, 11), fake.ClipCall);
    }

    [Fact]
    public async Task AClipForASongTheGameDoesNotHoldIsNotFoundAndTheHostIsNotAsked()
    {
        var fake = new FakeService { Throw = new GuessTheThemeRoundNotFoundException(5) };
        var relay = new FakeMediaRelay();
        await using var host = await TestHost.StartAsync(fake, relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("This game has no song 5 to play.", await ErrorAsync(response));
        Assert.Empty(relay.Opened);
    }

    [Fact]
    public async Task AClipForAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new GuessTheThemeRunNotFoundException(12) };
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay());

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AFileThatHasGoneIsGone()
    {
        var relay = new FakeMediaRelay { OpenFailure = new AnimeThemesMediaGoneException() };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("The song's file is no longer there.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AnAnimeThemesFailureIsABadGatewayWithAJsonReason()
    {
        var relay = new FakeMediaRelay { OpenFailure = new AnimeThemesMediaUnavailableException("AnimeThemes took too long to answer.") };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("AnimeThemes took too long to answer.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AClipThatFailsPartWayIsABadGatewayNotAShortSong()
    {
        var relay = new FakeMediaRelay { Source = new FailingStream(failAfter: 20_000) };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("AnimeThemes stopped sending the song.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AClipAskedForTwiceReadsTheHostOnce()
    {
        var relay = new FakeMediaRelay();
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");
        await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip?round=5");

        Assert.Single(relay.Opened);
    }

    [Fact]
    public async Task AClipNeedsNoRoundInTheQueryToBeFoundAsNotFound()
    {
        // A missing round reads as 0, which no game holds.
        var fake = new FakeService { Throw = new GuessTheThemeRoundNotFoundException(0) };
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay());

        var response = await host.Client.GetAsync("api/games/guess-the-theme/runs/12/clip");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal((12, 0), fake.ClipCall);
    }

    // ---- GET .../video ----

    private const int Held = AnimeThemesMediaHold.VideoStartBytes;

    /// <summary>A file longer than what is held, each byte telling where it is.</summary>
    private static byte[] AVideo(int length = Held + 5000)
    {
        var bytes = new byte[length];
        for (var i = 0; i < length; i++)
            bytes[i] = (byte)(i % 251);
        return bytes;
    }

    private static Task<HttpResponseMessage> ReadRangeAsync(TestHost host, string range) =>
        host.Client.SendAsync(new HttpRequestMessage(HttpMethod.Get, "api/games/guess-the-theme/video?anime=9&theme=91")
        {
            Headers = { { "Range", range } },
        });

    [Fact]
    public async Task TheVideoIsFoundByTheSongTheQueryNames()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay { Video = AVideo(100) });

        await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal((9, 91), fake.VideoCall);
    }

    [Fact]
    public async Task AWholeVideoComesFromWhatIsHeldAndThenFromTheHost()
    {
        var video = AVideo();
        var relay = new FakeMediaRelay { Video = video };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(video.Length, response.Content.Headers.ContentLength);
        var body = await response.Content.ReadAsByteArrayAsync();
        Assert.True(video.AsSpan().SequenceEqual(body), "the bytes must be the file's, in order");
        Assert.Equal(
            [("https://v.animethemes.moe/Test-OP1.webm", 0L, (long)Held - 1), ("https://v.animethemes.moe/Test-OP1.webm", (long)Held, video.Length - 1L)],
            relay.VideoReads);
    }

    [Fact]
    public async Task TheVideoIsServedAsWebmAcceptsRangesAndIsToldNotToBeStored()
    {
        await using var host = await TestHost.StartAsync(new FakeService(), new FakeMediaRelay { Video = AVideo(100) });

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal("video/webm", response.Content.Headers.ContentType?.ToString());
        Assert.Equal("bytes", response.Headers.AcceptRanges.Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.False(response.Content.Headers.Contains("Content-Disposition"));
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task ARangeInsideWhatIsHeldNeverAsksTheHostAgain()
    {
        var video = AVideo();
        var relay = new FakeMediaRelay { Video = video };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await ReadRangeAsync(host, "bytes=1000-1999");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal($"bytes 1000-1999/{video.Length}", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(1000, response.Content.Headers.ContentLength);
        Assert.Equal(video[1000..2000], await response.Content.ReadAsByteArrayAsync());
        Assert.Single(relay.VideoReads);
    }

    [Fact]
    public async Task ThePairOfBytesSafariAsksForFirstIsAnswered()
    {
        var video = AVideo(100);
        await using var host = await TestHost.StartAsync(new FakeService(), new FakeMediaRelay { Video = video });

        var response = await ReadRangeAsync(host, "bytes=0-1");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 0-1/100", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(video[..2], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ARangeAcrossTheEndOfWhatIsHeldIsSeamless()
    {
        var video = AVideo();
        var relay = new FakeMediaRelay { Video = video };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await ReadRangeAsync(host, $"bytes={Held - 100}-{Held + 99}");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(video[(Held - 100)..(Held + 100)], await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(("https://v.animethemes.moe/Test-OP1.webm", (long)Held, (long)Held + 99), relay.VideoReads[^1]);
    }

    [Fact]
    public async Task ARangePastWhatIsHeldComesFromTheHostAtThatPlace()
    {
        var video = AVideo();
        var relay = new FakeMediaRelay { Video = video };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await ReadRangeAsync(host, $"bytes={Held + 1000}-");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal($"bytes {Held + 1000}-{video.Length - 1}/{video.Length}", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(video[(Held + 1000)..], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ARangeCountedFromTheEndIsTheLastOfTheFile()
    {
        var video = AVideo(1000);
        await using var host = await TestHost.StartAsync(new FakeService(), new FakeMediaRelay { Video = video });

        var response = await ReadRangeAsync(host, "bytes=-10");

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 990-999/1000", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(video[990..], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ARangeEndingPastTheFileStopsAtItsEnd()
    {
        var video = AVideo(1000);
        await using var host = await TestHost.StartAsync(new FakeService(), new FakeMediaRelay { Video = video });

        var response = await ReadRangeAsync(host, "bytes=900-5000");

        Assert.Equal("bytes 900-999/1000", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(video[900..], await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task ARangeTheFileDoesNotHaveIsRefusedWithItsSize()
    {
        await using var host = await TestHost.StartAsync(new FakeService(), new FakeMediaRelay { Video = AVideo(1000) });

        var response = await ReadRangeAsync(host, "bytes=1000-");

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal("bytes */1000", response.Content.Headers.ContentRange?.ToString());
    }

    [Fact]
    public async Task TheVideoNeverNamesTheHostOrTheFile()
    {
        var fake = new FakeService { VideoSource = "https://v.animethemes.moe/DeathNote-OP1.webm" };
        await using var host = await TestHost.StartAsync(fake, new FakeMediaRelay { Video = AVideo(100) });

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        var headers = string.Join("\n", response.Headers.Concat(response.Content.Headers)
            .Select(h => $"{h.Key}: {string.Join(",", h.Value)}"));
        Assert.DoesNotContain("DeathNote", headers);
        Assert.DoesNotContain("animethemes", headers, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ASongWithNoVideoIsNotFoundAndTheHostIsNotAsked()
    {
        var relay = new FakeMediaRelay();
        await using var host = await TestHost.StartAsync(new FakeService { VideoSource = null }, relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Empty(relay.VideoReads);
    }

    [Fact]
    public async Task AVideoThatHasGoneIsGone()
    {
        var relay = new FakeMediaRelay { VideoFailure = new AnimeThemesMediaGoneException() };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task AVideoAnimeThemesWouldNotGiveIsABadGatewayWithAReason()
    {
        var relay = new FakeMediaRelay { VideoFailure = new AnimeThemesMediaUnavailableException("AnimeThemes is busy right now.") };
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var response = await host.Client.GetAsync("api/games/guess-the-theme/video?anime=9&theme=91");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("AnimeThemes is busy right now.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AVideoWhoseRestFailsPartWayIsCutOffNotEndedAsIfComplete()
    {
        var video = AVideo();
        var relay = new FakeMediaRelay { Video = video };
        relay.VideoBody = first => first == 0 ? new MemoryStream(video[..Held]) : new FailingStream(failAfter: 1000);
        await using var host = await TestHost.StartAsync(new FakeService(), relay);

        var failed = false;
        try
        {
            using var response = await host.Client.GetAsync(
                "api/games/guess-the-theme/video?anime=9&theme=91", HttpCompletionOption.ResponseHeadersRead);
            await response.Content.ReadAsByteArrayAsync();
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            failed = true;
        }

        Assert.True(failed, "a video cut short must not end as if it were complete");
    }

    // ---- POST .../prepare ----

    [Fact]
    public async Task PrepareBindsTheSettingsAndAnswersNoContent()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/prepare", new
        {
            yearFrom = 2010,
            yearTo = 2015,
            mediaTypes = new[] { "tv" },
            list = "OnlyMyList",
            hideHentai = true,
            themes = "Endings",
        });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var settings = fake.PrepareSettings!;
        Assert.Equal(2010, settings.YearFrom);
        Assert.Equal(["tv"], settings.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, settings.List);
        Assert.True(settings.HideHentai);
        Assert.Equal(GameThemeChoice.Endings, settings.Themes);
    }

    [Fact]
    public async Task PrepareAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("0 is not a release year.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-theme/prepare", new { yearFrom = 0, list = "All" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("0 is not a release year.", await ErrorAsync(response));
    }

    // ---- the routes themselves ----

    [Theory]
    [InlineData("POST", "api/games/guess-the-theme")]
    [InlineData("POST", "api/games/guess-the-theme/pool")]
    [InlineData("GET", "api/games/guess-the-theme/runs")]
    [InlineData("GET", "api/games/guess-the-theme/runs/3/guesses")]
    [InlineData("GET", "api/games/guess-the-theme/runs/3/skips")]
    [InlineData("GET", "api/games/guess-the-theme/runs/3/next")]
    [InlineData("GET", "api/games/guess-the-theme/runs/3/end")]
    [InlineData("GET", "api/games/guess-the-theme/runs/3/replace")]
    [InlineData("PUT", "api/games/guess-the-theme/runs/3/end")]
    [InlineData("POST", "api/games/guess-the-theme/runs/3/clip")]
    [InlineData("POST", "api/games/guess-the-theme/video")]
    [InlineData("GET", "api/games/guess-the-theme/prepare")]
    public async Task EveryRouteAnswersOnlyItsOwnMethod(string method, string url)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.SendAsync(new HttpRequestMessage(new HttpMethod(method), url));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
    }

    // ---- plumbing ----

    private static async Task<JsonDocument> Json(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    private static async Task<string?> ErrorAsync(HttpResponseMessage response)
    {
        using var json = await Json(response);
        return json.RootElement.GetProperty("error").GetString();
    }

    /// <summary>A body that gives some bytes and then fails, as a dropped connection to the media
    /// host does.</summary>
    private sealed class FailingStream(long failAfter) : Stream
    {
        private long _position;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => _position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken ct = default)
        {
            if (_position >= failAfter)
                throw new IOException("The connection to the media host dropped.");

            var count = (int)Math.Min(buffer.Length, Math.Min(4096, failAfter - _position));
            buffer.Span[..count].Fill(7);
            _position += count;
            return ValueTask.FromResult(count);
        }
    }

    /// <summary>Stands in for the game: records what the controller passed it and answers what a
    /// test set, or throws the exception it set.</summary>
    private sealed class FakeService : IGuessTheThemeService
    {
        public Exception? Throw { get; init; }
        public GuessTheThemeStateDto State { get; init; } = new(null, null, [], false);
        public GamePoolDto Pool { get; init; } = new(0, false);
        public GuessTheThemeGameDto Started { get; init; } = Game();
        public GuessTheThemeMoveDto Moved { get; init; } = new(GuessTheThemeOutcome.Wrong, Game(), null);
        public GuessTheThemeGameDto Next { get; init; } = Game();
        public GuessTheThemeEndDto Ended { get; init; } = new(null);
        public GuessTheThemeReplaceDto Replaced { get; init; } = new(null, Game());
        public string ClipSource { get; init; } = "https://a.animethemes.moe/Test-OP1.ogg";
        public string? VideoSource { get; init; } = "https://v.animethemes.moe/Test-OP1.webm";

        public bool? StateHideHentai { get; private set; }
        public GuessTheThemeSettings? PoolSettings { get; private set; }
        public GuessTheThemeSettings? StartSettings { get; private set; }
        public (int Id, int Move, int AnimeId)? GuessCall { get; private set; }
        public (int Id, int Move)? SkipCall { get; private set; }
        public (int Id, int Move)? NextCall { get; private set; }
        public (int Id, int Round)? ReplaceCall { get; private set; }
        public (int Id, int Round)? ClipCall { get; private set; }
        public (int Anime, int Theme)? VideoCall { get; private set; }
        public GuessTheThemeSettings? PrepareSettings { get; private set; }
        public int? EndedId { get; private set; }

        private Task<T> Answer<T>(T value) => Throw is null ? Task.FromResult(value) : Task.FromException<T>(Throw);

        public Task<GuessTheThemeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
        {
            StateHideHentai = hideHentai;
            return Task.FromResult(State);
        }

        public Task<GamePoolDto> GetPoolAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
        {
            PoolSettings = settings;
            return Answer(Pool);
        }

        public Task<GuessTheThemeGameDto> StartAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
        {
            StartSettings = settings;
            return Answer(Started);
        }

        public Task<GuessTheThemeMoveDto> GuessAsync(int runId, int move, int animeId, CancellationToken ct = default)
        {
            GuessCall = (runId, move, animeId);
            return Answer(Moved);
        }

        public Task<GuessTheThemeMoveDto> SkipAsync(int runId, int move, CancellationToken ct = default)
        {
            SkipCall = (runId, move);
            return Answer(Moved);
        }

        public Task<GuessTheThemeGameDto> NextAsync(int runId, int move, CancellationToken ct = default)
        {
            NextCall = (runId, move);
            return Answer(Next);
        }

        public Task<GuessTheThemeEndDto> EndAsync(int runId, CancellationToken ct = default)
        {
            EndedId = runId;
            return Answer(Ended);
        }

        public Task<string> GetClipSourceAsync(int runId, int round, CancellationToken ct = default)
        {
            ClipCall = (runId, round);
            return Answer(ClipSource);
        }

        public Task<GuessTheThemeReplaceDto> ReplaceAsync(int runId, int round, CancellationToken ct = default)
        {
            ReplaceCall = (runId, round);
            return Answer(Replaced);
        }

        public Task<string?> GetVideoSourceAsync(int animeId, int themeId, CancellationToken ct = default)
        {
            VideoCall = (animeId, themeId);
            return Answer(VideoSource);
        }

        public Task PrepareAsync(GuessTheThemeSettings settings, CancellationToken ct = default)
        {
            PrepareSettings = settings;
            return Throw is null ? Task.CompletedTask : Task.FromException(Throw);
        }
    }

    /// <summary>Only the one controller, so no other route or dependency is involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "GuessTheThemeController";
        public IEnumerable<TypeInfo> Types => types.Select(t => t.GetTypeInfo());
    }

    private sealed class TestHost : IAsyncDisposable
    {
        private readonly WebApplication _app;

        private TestHost(WebApplication app, HttpClient client)
        {
            _app = app;
            Client = client;
        }

        public HttpClient Client { get; }

        public static async Task<TestHost> StartAsync(IGuessTheThemeService service, IAnimeThemesMediaRelay? relay = null)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(service);
            var media = relay ?? new FakeMediaRelay();
            builder.Services.AddSingleton(media);
            builder.Services.AddSingleton<IAnimeThemesMediaHold>(new AnimeThemesMediaHold(media));
            // The one JSON option Program.cs sets: enums travel as their names.
            builder.Services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(GuessTheThemeController)));
                })
                .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

            var app = builder.Build();
            app.MapControllers();
            await app.StartAsync();

            var client = new HttpClient { BaseAddress = new Uri(app.Urls.First().TrimEnd('/') + "/") };
            return new TestHost(app, client);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
