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

// add-guess-the-anime task 6.4: each route's success and each refusal, the enums as
// strings, repeated `types` query values, and the 502's reason. Query binding and
// JSON enums only happen in a real pipeline, so these requests go over HTTP to the
// controller alone, hosted on Kestrel in the process, with a fake service behind
// it. The host mirrors Program.cs's one JSON option (enums as strings).
public class GuessTheAnimeControllerTests
{
    private static readonly GuessTheAnimeRowDto Row = new(
        7, "Row title", "Row english", "https://img/7.jpg",
        new GuessNumberDto(2019, GuessCompared.AnswerHigher),
        new GuessTextDto("spring", true),
        [new GuessTagDto("Action", true), new GuessTagDto("Drama", false)],
        new GuessNumberDto(null, null),
        [],
        new GuessTextDto("tv", false),
        new GuessNumberDto(8.31, GuessCompared.Same));

    private static readonly GuessTheAnimeGameDto Game = new(3, 10, [Row]);

    // ---- GET api/games/guess-the-anime ----

    [Fact]
    public async Task TheStateRouteAnswersTheGameTheYearsAndThePosters()
    {
        var fake = new FakeService
        {
            State = new GuessTheAnimeStateDto(Game, new GameYearsDto(1963, 2026), ["https://img/1.jpg", "https://img/2.jpg"]),
        };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/guess-the-anime");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await Json(response);
        var root = json.RootElement;
        Assert.Equal(3, root.GetProperty("game").GetProperty("id").GetInt32());
        Assert.Equal(10, root.GetProperty("game").GetProperty("maxGuesses").GetInt32());
        Assert.Equal(1, root.GetProperty("game").GetProperty("rows").GetArrayLength());
        Assert.Equal(1963, root.GetProperty("years").GetProperty("min").GetInt32());
        Assert.Equal(2026, root.GetProperty("years").GetProperty("max").GetInt32());
        Assert.Equal(2, root.GetProperty("pictures").GetArrayLength());
    }

    [Fact]
    public async Task ARowIsNamedByTheFieldsTheClientReadsWithItsEnumsAsStrings()
    {
        var fake = new FakeService { State = new GuessTheAnimeStateDto(Game, null, []) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/guess-the-anime"));

        var row = json.RootElement.GetProperty("game").GetProperty("rows")[0];
        Assert.Equal(7, row.GetProperty("animeId").GetInt32());
        Assert.Equal("Row title", row.GetProperty("title").GetString());
        Assert.Equal("Row english", row.GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/7.jpg", row.GetProperty("pictureUrl").GetString());
        Assert.Equal(2019, row.GetProperty("year").GetProperty("value").GetInt32());
        Assert.Equal("AnswerHigher", row.GetProperty("year").GetProperty("compared").GetString());
        Assert.Equal("spring", row.GetProperty("season").GetProperty("value").GetString());
        Assert.True(row.GetProperty("season").GetProperty("same").GetBoolean());
        Assert.Equal("Action", row.GetProperty("genres")[0].GetProperty("name").GetString());
        Assert.True(row.GetProperty("genres")[0].GetProperty("same").GetBoolean());
        Assert.False(row.GetProperty("genres")[1].GetProperty("same").GetBoolean());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("episodes").GetProperty("value").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("episodes").GetProperty("compared").ValueKind);
        Assert.Equal(0, row.GetProperty("studios").GetArrayLength());
        Assert.Equal("tv", row.GetProperty("mediaType").GetProperty("value").GetString());
        Assert.Equal(8.31, row.GetProperty("score").GetProperty("value").GetDouble());
        Assert.Equal("Same", row.GetProperty("score").GetProperty("compared").GetString());
    }

    [Fact]
    public async Task WithNoGameTheStateHasANullGameAndNullYears()
    {
        var fake = new FakeService { State = new GuessTheAnimeStateDto(null, null, []) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/games/guess-the-anime"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("game").ValueKind);
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("years").ValueKind);
        Assert.Equal(0, json.RootElement.GetProperty("pictures").GetArrayLength());
    }

    [Theory]
    [InlineData("api/games/guess-the-anime?hideHentai=true", true)]
    [InlineData("api/games/guess-the-anime?hideHentai=false", false)]
    [InlineData("api/games/guess-the-anime", false)]
    public async Task TheStateRoutePassesHideHentaiThrough(string url, bool expected)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync(url);

        Assert.Equal(expected, fake.StateHideHentai);
    }

    // ---- GET api/games/guess-the-anime/pool ----

    [Fact]
    public async Task ThePoolRouteBindsEverySettingFromTheQuery()
    {
        var fake = new FakeService { Pool = new GamePoolDto(1234, true) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync(
            "api/games/guess-the-anime/pool?yearFrom=2010&yearTo=2015&types=tv&types=movie&list=OnlyMyList&hideHentai=true");

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

        await host.Client.GetAsync("api/games/guess-the-anime/pool?types=ova&types=special&types=ona&types=tv");

        Assert.Equal(["ova", "special", "ona", "tv"], fake.PoolSettings!.MediaTypes);
    }

    [Fact]
    public async Task WithNoSettingsInTheQueryThePoolIsEveryAnime()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        await host.Client.GetAsync("api/games/guess-the-anime/pool");

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

        await host.Client.GetAsync($"api/games/guess-the-anime/pool?list={value}");

        Assert.Equal(expected, fake.PoolSettings!.List);
    }

    [Fact]
    public async Task AnUnknownListSettingIsABadRequest()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/guess-the-anime/pool?list=Banana");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.PoolSettings);
    }

    [Fact]
    public async Task ThePoolRouteAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("The release years run backwards: 2020 is after 2010.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/games/guess-the-anime/pool?yearFrom=2020&yearTo=2010");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("The release years run backwards: 2020 is after 2010.", await ErrorAsync(response));
    }

    // ---- POST api/games/guess-the-anime/runs ----

    [Fact]
    public async Task StartBindsTheSettingsFromTheBodyAndAnswersTheEmptyGame()
    {
        var fake = new FakeService { Started = new GuessTheAnimeGameDto(3, 10, []) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new
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
        Assert.Equal(10, json.RootElement.GetProperty("maxGuesses").GetInt32());
        Assert.Equal(0, json.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public async Task StartTakesNullsAsAnyYearAndEveryType()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new
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

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new { list = "Banana" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartRefusesAnEmptyBody()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/guess-the-anime/runs", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.StartSettings);
    }

    [Fact]
    public async Task StartAnswersBadRequestForSettingsThatCannotDescribeAGame()
    {
        var fake = new FakeService { Throw = new GameSettingsException("0 is not a release year.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new { yearFrom = 0, list = "All" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("0 is not a release year.", await ErrorAsync(response));
    }

    [Fact]
    public async Task StartAnswersConflictWhenTheSettingsLeaveNoAnime()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeNotPlayableException() };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new { list = "All" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("There aren't enough anime for these settings.", await ErrorAsync(response));
    }

    [Fact]
    public async Task StartAnswersBadGatewayWithTheReasonWhenNoAnswerCouldBeSettled()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeNoAnswerException("MyAnimeList couldn't be reached.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs", new { list = "All" });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MyAnimeList couldn't be reached.", await ErrorAsync(response));
    }

    // ---- POST api/games/guess-the-anime/runs/{id}/guesses ----

    [Fact]
    public async Task AGuessPassesTheGameTheNumberAndTheAnimeThrough()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/12/guesses", new { number = 5, animeId = 9 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((12, 5, 9), fake.GuessCall);
    }

    [Fact]
    public async Task AGuessThatDidNotEndTheGameAnswersItsRowAndNothingElse()
    {
        var fake = new FakeService { Guessed = new GuessTheAnimeGuessDto(Row, null, null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/guess-the-anime/runs/3/guesses", new { number = 1, animeId = 7 }));

        var root = json.RootElement;
        Assert.Equal(7, root.GetProperty("row").GetProperty("animeId").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("end").ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("answer").ValueKind);
    }

    [Theory]
    [InlineData(GuessTheAnimeEnd.Guessed, "Guessed")]
    [InlineData(GuessTheAnimeEnd.OutOfGuesses, "OutOfGuesses")]
    public async Task HowAGameEndedIsAStringAndTheAnswerComesWithIt(GuessTheAnimeEnd end, string expected)
    {
        var fake = new FakeService { Guessed = new GuessTheAnimeGuessDto(Row, end, new GuessTheAnimeAnswerDto(Row, true)) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.PostAsJsonAsync(
            "api/games/guess-the-anime/runs/3/guesses", new { number = 4, animeId = 7 }));

        Assert.Equal(JsonValueKind.String, json.RootElement.GetProperty("end").ValueKind);
        Assert.Equal(expected, json.RootElement.GetProperty("end").GetString());
        var answer = json.RootElement.GetProperty("answer");
        Assert.Equal(7, answer.GetProperty("row").GetProperty("animeId").GetInt32());
        Assert.True(answer.GetProperty("inMyList").GetBoolean());
    }

    [Fact]
    public async Task AGuessOfAnAnimeAlreadyGuessedIsABadRequest()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeAlreadyGuessedException(55) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/3/guesses", new { number = 2, animeId = 55 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Anime 55 has already been guessed in this game.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AGuessForAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeRunNotFoundException(3) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/3/guesses", new { number = 2, animeId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 3 is not in progress.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AGuessTheGameHasPassedIsAConflict()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeStaleGuessException(2) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/3/guesses", new { number = 2, animeId = 7 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("Guess 2 is no longer the current guess.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AGuessWhoseDetailsCouldNotBeFetchedIsABadGatewayWithTheReason()
    {
        var fake = new FakeService
        {
            Throw = new GuessTheAnimeDetailsUnavailableException("MyAnimeList couldn't be reached.", new HttpRequestException()),
        };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/3/guesses", new { number = 2, animeId = 7 });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("MyAnimeList couldn't be reached.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AGuessWithNoBodyIsABadRequestAndNeverReachesTheService()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/games/guess-the-anime/runs/3/guesses", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.GuessCall);
    }

    [Fact]
    public async Task AGameIdThatIsNotANumberMatchesNoRoute()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/games/guess-the-anime/runs/abc/guesses", new { number = 1, animeId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(fake.GuessCall);
    }

    // ---- POST api/games/guess-the-anime/runs/{id}/end ----

    [Fact]
    public async Task EndAnswersTheAnswerOfTheGameItEnded()
    {
        var fake = new FakeService { Ended = new GuessTheAnimeEndDto(new GuessTheAnimeAnswerDto(Row, false)) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/guess-the-anime/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(12, fake.EndedId);
        using var json = await Json(response);
        Assert.Equal(7, json.RootElement.GetProperty("answer").GetProperty("row").GetProperty("animeId").GetInt32());
        Assert.False(json.RootElement.GetProperty("answer").GetProperty("inMyList").GetBoolean());
    }

    [Fact]
    public async Task EndingAGameNoLongerInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new GuessTheAnimeRunNotFoundException(12) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync("api/games/guess-the-anime/runs/12/end", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Game 12 is not in progress.", await ErrorAsync(response));
    }

    // ---- the routes themselves ----

    [Theory]
    [InlineData("POST", "api/games/guess-the-anime")]
    [InlineData("POST", "api/games/guess-the-anime/pool")]
    [InlineData("GET", "api/games/guess-the-anime/runs")]
    [InlineData("GET", "api/games/guess-the-anime/runs/3/guesses")]
    [InlineData("GET", "api/games/guess-the-anime/runs/3/end")]
    [InlineData("PUT", "api/games/guess-the-anime/runs/3/end")]
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
    private sealed class FakeService : IGuessTheAnimeService
    {
        public Exception? Throw { get; init; }
        public GuessTheAnimeStateDto State { get; init; } = new(null, null, []);
        public GamePoolDto Pool { get; init; } = new(0, false);
        public GuessTheAnimeGameDto Started { get; init; } = Game;
        public GuessTheAnimeGuessDto Guessed { get; init; } = new(Row, null, null);
        public GuessTheAnimeEndDto Ended { get; init; } = new(new GuessTheAnimeAnswerDto(Row, false));

        public bool? StateHideHentai { get; private set; }
        public GameSettings? PoolSettings { get; private set; }
        public GameSettings? StartSettings { get; private set; }
        public (int Id, int Number, int AnimeId)? GuessCall { get; private set; }
        public int? EndedId { get; private set; }

        public Task<GuessTheAnimeStateDto> GetStateAsync(bool hideHentai, CancellationToken ct = default)
        {
            StateHideHentai = hideHentai;
            return Task.FromResult(State);
        }

        public Task<GamePoolDto> GetPoolAsync(GameSettings settings, CancellationToken ct = default)
        {
            PoolSettings = settings;
            return Throw is null ? Task.FromResult(Pool) : Task.FromException<GamePoolDto>(Throw);
        }

        public Task<GuessTheAnimeGameDto> StartAsync(GameSettings settings, CancellationToken ct = default)
        {
            StartSettings = settings;
            return Throw is null ? Task.FromResult(Started) : Task.FromException<GuessTheAnimeGameDto>(Throw);
        }

        public Task<GuessTheAnimeGuessDto> GuessAsync(int runId, int number, int animeId, CancellationToken ct = default)
        {
            GuessCall = (runId, number, animeId);
            return Throw is null ? Task.FromResult(Guessed) : Task.FromException<GuessTheAnimeGuessDto>(Throw);
        }

        public Task<GuessTheAnimeEndDto> EndAsync(int runId, CancellationToken ct = default)
        {
            EndedId = runId;
            return Throw is null ? Task.FromResult(Ended) : Task.FromException<GuessTheAnimeEndDto>(Throw);
        }
    }

    /// <summary>Only the one controller, so no other route or dependency is
    /// involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "GuessTheAnimeController";
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

        public static async Task<TestHost> StartAsync(IGuessTheAnimeService service)
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
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(GuessTheAnimeController)));
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
