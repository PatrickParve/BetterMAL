using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Games;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Controllers;

// add-games-higher-or-lower task 4.4: each route's success and each refusal, the
// enums as strings, and repeated `types` query values. Query binding and JSON
// enums only happen in a real pipeline, so these requests go over HTTP to the
// controller alone, hosted on Kestrel in the process, with a fake service behind
// it. The host mirrors Program.cs's one JSON option (enums as strings).
public class HigherOrLowerControllerTests
{
    private static readonly HigherOrLowerCardDto Left =
        new(7, "Left title", "Left english", "https://img/7.jpg", "tv", 2019, true, 8.31);

    private static readonly HigherOrLowerCardDto Right =
        new(9, "Right title", null, "https://img/9.jpg", "movie", null, false, null);

    private static readonly HigherOrLowerGameDto Game = new(3, 2, 1, Left, Right);

    // ---- GET api/games/higher-or-lower ----

    [Fact]
    public async Task TheStateRouteAnswersTheGameTheYearsAndThePosters()
    {
        var fake = new FakeService
        {
            State = new HigherOrLowerStateDto(Game, new GameYearsDto(1963, 2026), ["https://img/1.jpg", "https://img/2.jpg"]),
        };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/higher-or-lower");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await Json(response);
        var root = json.RootElement;
        Assert.Equal(3, root.GetProperty("game").GetProperty("id").GetInt32());
        Assert.Equal(2, root.GetProperty("game").GetProperty("round").GetInt32());
        Assert.Equal(1, root.GetProperty("game").GetProperty("streak").GetInt32());
        Assert.Equal(1963, root.GetProperty("years").GetProperty("min").GetInt32());
        Assert.Equal(2026, root.GetProperty("years").GetProperty("max").GetInt32());
        Assert.Equal(2, root.GetProperty("pictures").GetArrayLength());
    }

    [Fact]
    public async Task ACardIsNamedByTheFieldsTheClientReads()
    {
        var fake = new FakeService { State = new HigherOrLowerStateDto(Game, null, []) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/higher-or-lower"));

        var left = json.RootElement.GetProperty("game").GetProperty("left");
        Assert.Equal(7, left.GetProperty("animeId").GetInt32());
        Assert.Equal("Left title", left.GetProperty("title").GetString());
        Assert.Equal("Left english", left.GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/7.jpg", left.GetProperty("pictureUrl").GetString());
        Assert.Equal("tv", left.GetProperty("mediaType").GetString());
        Assert.Equal(2019, left.GetProperty("year").GetInt32());
        Assert.True(left.GetProperty("inMyList").GetBoolean());
        Assert.Equal(8.31, left.GetProperty("score").GetDouble());
        var right = json.RootElement.GetProperty("game").GetProperty("right");
        Assert.Equal(JsonValueKind.Null, right.GetProperty("score").ValueKind);
        Assert.Equal(JsonValueKind.Null, right.GetProperty("year").ValueKind);
    }

    [Fact]
    public async Task WithNoGameTheStateHasANullGameAndNullYears()
    {
        var fake = new FakeService { State = new HigherOrLowerStateDto(null, null, []) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/higher-or-lower"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("game").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("years").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("pictures").GetArrayLength());
    }

    [Theory]
    [InlineData("api/games/higher-or-lower?hideHentai=true", true)]
    [InlineData("api/games/higher-or-lower?hideHentai=false", false)]
    [InlineData("api/games/higher-or-lower", false)]
    public async Task TheStateRoutePassesHideHentaiThrough(string url, bool expected)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync(url);

        Assert.Equal(expected, fake.StateHideHentai);
    }

    // ---- GET api/games/higher-or-lower/pool ----

    [Fact]
    public async Task ThePoolRouteBindsEverySettingFromTheQuery()
    {
        var fake = new FakeService { Pool = new GamePoolDto(1234, true) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync(
            "api/games/higher-or-lower/pool?yearFrom=2010&yearTo=2015&types=tv&types=movie&list=OnlyMyList&hideHentai=true");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.PoolSettings!;
        Assert.Equal(2010, settings.YearFrom);
        Assert.Equal(2015, settings.YearTo);
        Assert.Equal(["tv", "movie"], settings.MediaTypes);
        Assert.Equal(GameListScope.OnlyMyList, settings.List);
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

        await host.Client.GetAsync("api/games/higher-or-lower/pool?types=ova&types=special&types=ona&types=tv");

        Assert.Equal(["ova", "special", "ona", "tv"], fake.PoolSettings!.MediaTypes);
    }

    [Fact]
    public async Task ASingleTypesValueIsOneType()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/games/higher-or-lower/pool?types=tv");

        Assert.Equal(["tv"], fake.PoolSettings!.MediaTypes);
    }

    [Fact]
    public async Task WithNoSettingsInTheQueryThePoolIsEveryAnime()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/games/higher-or-lower/pool");

        var settings = fake.PoolSettings!;
        Assert.Null(settings.YearFrom);
        Assert.Null(settings.YearTo);
        Assert.True(settings.MediaTypes is null or { Count: 0 });
        Assert.Equal(GameListScope.All, settings.List);
        Assert.False(settings.HideHentai);
    }

    [Theory]
    [InlineData("All", GameListScope.All)]
    [InlineData("OnlyMyList", GameListScope.OnlyMyList)]
    [InlineData("NotOnMyList", GameListScope.NotOnMyList)]
    public async Task TheListSettingIsReadByName(string value, GameListScope expected)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync($"api/games/higher-or-lower/pool?list={value}");

        Assert.Equal(expected, fake.PoolSettings!.List);
    }

    [Fact]
    public async Task AnUnknownListSettingIsABadRequest()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/higher-or-lower/pool?list=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.PoolSettings);
    }

    [Fact]
    public async Task ThePoolRouteAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("The release years run backwards: 2020 is after 2010.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/higher-or-lower/pool?yearFrom=2020&yearTo=2010");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The release years run backwards: 2020 is after 2010.", await ErrorAsync(response));
    }

    // ---- POST api/games/higher-or-lower/runs ----

    [Fact]
    public async Task StartBindsTheSettingsFromTheBodyAndAnswersTheFirstRound()
    {
        var fake = new FakeService { Started = Game with { Round = 1, Streak = 0 } };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs", new
        {
            yearFrom = 2010,
            yearTo = 2015,
            mediaTypes = new[] { "tv", "movie" },
            list = "NotOnMyList",
            hideHentai = true,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.StartSettings!;
        Assert.Equal(2010, settings.YearFrom);
        Assert.Equal(2015, settings.YearTo);
        Assert.Equal(["tv", "movie"], settings.MediaTypes);
        Assert.Equal(GameListScope.NotOnMyList, settings.List);
        Assert.True(settings.HideHentai);
        using var json = await Json(response);
        Assert.Equal(3, json.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(1, json.RootElement.GetProperty("round").GetInt32());
        Assert.Equal(7, json.RootElement.GetProperty("left").GetProperty("animeId").GetInt32());
    }

    [Fact]
    public async Task StartTakesNullsAndMissingFieldsAsAnyYearAndEveryType()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs", new
        {
            yearFrom = (int?)null,
            yearTo = (int?)null,
            mediaTypes = (string[]?)null,
            list = "All",
            hideHentai = false,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var settings = fake.StartSettings!;
        Assert.Null(settings.YearFrom);
        Assert.Null(settings.YearTo);
        Assert.Null(settings.MediaTypes);
    }

    [Fact]
    public async Task StartRefusesAnUnknownListNameWithoutCallingTheService()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs", new { list = "Banana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartRefusesAnEmptyBody()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/higher-or-lower/runs", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("0 is not a release year.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs", new { yearFrom = 0, list = "All" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("0 is not a release year.", await ErrorAsync(response));
    }

    [Fact]
    public async Task StartAnswersConflictWhenTheSettingsLeaveTooFewAnime()
    {
        var fake = new FakeService { Throw = new HigherOrLowerNotPlayableException() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs", new { list = "All" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("There aren't enough anime for these settings.", await ErrorAsync(response));
    }

    // ---- POST api/games/higher-or-lower/runs/{id}/picks ----

    [Fact]
    public async Task APickPassesTheGameTheRoundAndTheAnimeThrough()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs/12/picks", new { round = 5, animeId = 9 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 5, 9), fake.PickCall);
    }

    [Fact]
    public async Task ACorrectPickAnswersTheVerdictTheScoresAndTheNextRound()
    {
        var fake = new FakeService
        {
            Picked = new HigherOrLowerPickDto(true, 8.31, 7.02, 10, 0, null, Game with { Round = 11, Streak = 10 }),
        };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/higher-or-lower/runs/3/picks", new { round = 10, animeId = 7 }));

        var root = json.RootElement;
        Assert.True(root.GetProperty("correct").GetBoolean());
        Assert.Equal(8.31, root.GetProperty("leftScore").GetDouble());
        Assert.Equal(7.02, root.GetProperty("rightScore").GetDouble());
        Assert.Equal(10, root.GetProperty("streak").GetInt32());
        Assert.Equal(0, root.GetProperty("milestoneLevel").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("end").ValueKind);
        Assert.Equal(11, root.GetProperty("next").GetProperty("round").GetInt32());
    }

    [Fact]
    public async Task AUsualCorrectPickHasNoMilestoneLevel()
    {
        var fake = new FakeService { Picked = new HigherOrLowerPickDto(true, 8.31, 7.02, 11, null, null, Game) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/higher-or-lower/runs/3/picks", new { round = 2, animeId = 7 }));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("milestoneLevel").ValueKind);
    }

    [Theory]
    [InlineData(HigherOrLowerEnd.Wrong, "Wrong")]
    [InlineData(HigherOrLowerEnd.Cleared, "Cleared")]
    public async Task HowAGameEndedIsAStringAndTheNextRoundIsNull(HigherOrLowerEnd end, string expected)
    {
        var fake = new FakeService { Picked = new HigherOrLowerPickDto(end == HigherOrLowerEnd.Cleared, 8.31, 7.02, 4, null, end, null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/higher-or-lower/runs/3/picks", new { round = 2, animeId = 7 }));

        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("end").ValueKind);
        Assert.Equal(expected, json.RootElement.GetProperty("end").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("next").ValueKind);
    }

    [Fact]
    public async Task APickOfAnAnimeNotOnTheTableIsABadRequest()
    {
        var fake = new FakeService { Throw = new HigherOrLowerCardNotOnTableException(55) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs/3/picks", new { round = 2, animeId = 55 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Anime 55 is not one of the two cards on the table.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickForAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new HigherOrLowerRunNotFoundException(3) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs/3/picks", new { round = 2, animeId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 3 is not in progress.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickForARoundTheGameHasPassedIsAConflict()
    {
        var fake = new FakeService { Throw = new HigherOrLowerStaleRoundException(2) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs/3/picks", new { round = 2, animeId = 7 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Round 2 is no longer the current round.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickWithNoBodyIsABadRequestAndNeverReachesTheService()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/higher-or-lower/runs/3/picks", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.PickCall);
    }

    [Fact]
    public async Task AGameIdThatIsNotANumberMatchesNoRoute()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/higher-or-lower/runs/abc/picks", new { round = 1, animeId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(fake.PickCall);
    }

    // ---- POST api/games/higher-or-lower/runs/{id}/end ----

    [Fact]
    public async Task EndAnswersTheStreakOfTheGameItEnded()
    {
        var fake = new FakeService { Ended = new HigherOrLowerEndDto(4) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/higher-or-lower/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(12, fake.EndedId);
        using var json = await Json(response);
        Assert.Equal(4, json.RootElement.GetProperty("streak").GetInt32());
    }

    [Fact]
    public async Task EndingAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new HigherOrLowerRunNotFoundException(12) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/higher-or-lower/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 12 is not in progress.", await ErrorAsync(response));
    }

    // ---- the routes themselves ----

    [Theory]
    [InlineData("POST", "api/games/higher-or-lower")]
    [InlineData("POST", "api/games/higher-or-lower/pool")]
    [InlineData("GET", "api/games/higher-or-lower/runs")]
    [InlineData("GET", "api/games/higher-or-lower/runs/3/picks")]
    [InlineData("GET", "api/games/higher-or-lower/runs/3/end")]
    [InlineData("PUT", "api/games/higher-or-lower/runs/3/end")]
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

    /// <summary>Stands in for the game: records what the controller passed it and
    /// answers what a test set, or throws the exception it set.</summary>
    private sealed class FakeService : IHigherOrLowerService
    {
        public Exception? Throw { get; init; }
        public HigherOrLowerStateDto State { get; init; } = new(null, null, []);
        public GamePoolDto Pool { get; init; } = new(0, false);
        public HigherOrLowerGameDto Started { get; init; } = Game;
        public HigherOrLowerPickDto Picked { get; init; } = new(true, 8, 7, 1, null, null, Game);
        public HigherOrLowerEndDto Ended { get; init; } = new(0);

        public bool? StateHideHentai { get; private set; }
        public GameSettings? PoolSettings { get; private set; }
        public GameSettings? StartSettings { get; private set; }
        public (int Id, int Round, int AnimeId)? PickCall { get; private set; }
        public int? EndedId { get; private set; }

        public Task<HigherOrLowerStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
        {
            StateHideHentai = hideHentai;
            return Task.FromResult(State);
        }

        public Task<GamePoolDto> GetPoolAsync(GameSettings settings, CancellationToken ct = default)
        {
            PoolSettings = settings;
            return Throw is null ? Task.FromResult(Pool) : Task.FromException<GamePoolDto>(Throw);
        }

        public Task<HigherOrLowerGameDto> StartAsync(GameSettings settings, CancellationToken ct = default)
        {
            StartSettings = settings;
            return Throw is null ? Task.FromResult(Started) : Task.FromException<HigherOrLowerGameDto>(Throw);
        }

        public Task<HigherOrLowerPickDto> PickAsync(int runId, int round, int animeId, CancellationToken ct = default)
        {
            PickCall = (runId, round, animeId);
            return Throw is null ? Task.FromResult(Picked) : Task.FromException<HigherOrLowerPickDto>(Throw);
        }

        public Task<HigherOrLowerEndDto> EndAsync(int runId, CancellationToken ct = default)
        {
            EndedId = runId;
            return Throw is null ? Task.FromResult(Ended) : Task.FromException<HigherOrLowerEndDto>(Throw);
        }
    }

    /// <summary>Only the one controller, so no other route or dependency is
    /// involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "HigherOrLowerController";
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

        public static async Task<TestHost> StartAsync(IHigherOrLowerService service)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(service);
            // The one JSON option Program.cs sets: enums travel as their names.
            builder.Services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(HigherOrLowerController)));
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
