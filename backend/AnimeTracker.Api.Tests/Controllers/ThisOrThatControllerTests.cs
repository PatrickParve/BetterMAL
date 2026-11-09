using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text;
using System.Text.Json;
using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Services.Ranking.ThisOrThat;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AnimeTracker.Api.Tests.Controllers;

// add-this-or-that-ranking task 3.5: each route's success and each refusal, the fields
// the client reads, a null `winnerId`, an optional Start body, and `version` on the
// DELETE query string. Route binding and JSON only happen in a real pipeline, so these
// requests go over HTTP to the controller alone, hosted on Kestrel in the process,
// with a fake service behind it.
public class ThisOrThatControllerTests
{
    private static readonly ThisOrThatCard Left =
        new(7, "Left title", "Left english", "https://img/7.jpg", "tv", 2019);

    private static readonly ThisOrThatCard Right = new(9, "Right title", null, null, null, null);

    private static readonly ThisOrThatRunState Run = new(
        Version: 4, Placed: 38, Total: 149, PicksMade: 200, PicksLeftAtMost: 712, CanUndo: true,
        Pair: new ThisOrThatPairDto(Left, Right), Result: null, UpcomingPictureUrls: ["https://img/1.jpg", "https://img/2.jpg"]);

    private static readonly ThisOrThatScoreState State = new(8, 149, 937, Run);

    // ---- GET api/this-or-that ----

    [Fact]
    public async Task TheScoresRouteAnswersEachOfferedScoreWithItsCountMostPicksAndWhetherItIsInProgress()
    {
        var fake = new FakeService
        {
            Scores = new ThisOrThatScoresDto([new(10, 12, 33, false), new(8, 149, 937, true)]),
        };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/this-or-that");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = await Json(response);
        var scores = json.RootElement.GetProperty("scores");
        Assert.Equal(2, scores.GetArrayLength());
        Assert.Equal(10, scores[0].GetProperty("score").GetInt32());
        Assert.Equal(12, scores[0].GetProperty("count").GetInt32());
        Assert.Equal(33, scores[0].GetProperty("maxPicks").GetInt32());
        Assert.False(scores[0].GetProperty("inProgress").GetBoolean());
        Assert.True(scores[1].GetProperty("inProgress").GetBoolean());
    }

    // ---- GET api/this-or-that/{score} ----

    [Fact]
    public async Task AScoreWithARankingInProgressAnswersItsRunAndPairByTheFieldsTheClientReads()
    {
        var fake = new FakeService { Score = State };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/this-or-that/8");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(8, fake.ScoreRead);
        using var json = await Json(response);
        var root = json.RootElement;
        Assert.Equal(8, root.GetProperty("score").GetInt32());
        Assert.Equal(149, root.GetProperty("count").GetInt32());
        Assert.Equal(937, root.GetProperty("maxPicks").GetInt32());
        var run = root.GetProperty("run");
        Assert.Equal(4, run.GetProperty("version").GetInt32());
        Assert.Equal(38, run.GetProperty("placed").GetInt32());
        Assert.Equal(149, run.GetProperty("total").GetInt32());
        Assert.Equal(200, run.GetProperty("picksMade").GetInt32());
        Assert.Equal(712, run.GetProperty("picksLeftAtMost").GetInt32());
        Assert.True(run.GetProperty("canUndo").GetBoolean());
        Assert.Equal(JsonValueKind.Null, run.GetProperty("result").ValueKind);
        Assert.Equal(["https://img/1.jpg", "https://img/2.jpg"],
            run.GetProperty("upcomingPictureUrls").EnumerateArray().Select(e => e.GetString()));
        var left = run.GetProperty("pair").GetProperty("left");
        Assert.Equal(7, left.GetProperty("animeId").GetInt32());
        Assert.Equal("Left title", left.GetProperty("title").GetString());
        Assert.Equal("Left english", left.GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/7.jpg", left.GetProperty("pictureUrl").GetString());
        Assert.Equal("tv", left.GetProperty("mediaType").GetString());
        Assert.Equal(2019, left.GetProperty("year").GetInt32());
        var right = run.GetProperty("pair").GetProperty("right");
        Assert.Equal(JsonValueKind.Null, right.GetProperty("englishTitle").ValueKind);
        Assert.Equal(JsonValueKind.Null, right.GetProperty("pictureUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, right.GetProperty("year").ValueKind);
    }

    [Fact]
    public async Task AFinishedRankingAnswersTheResultRowsAndANullPair()
    {
        var finished = Run with
        {
            Pair = null,
            Result = [new(7, "Left title", "Left english", "https://img/7.jpg", 1, 3), new(9, "Right title", null, null, 2, 1)],
            UpcomingPictureUrls = [],
        };
        var fake = new FakeService { Score = State with { Run = finished } };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/this-or-that/8"));

        var run = json.RootElement.GetProperty("run");
        Assert.Equal(JsonValueKind.Null, run.GetProperty("pair").ValueKind);
        var rows = run.GetProperty("result");
        Assert.Equal(2, rows.GetArrayLength());
        Assert.Equal(7, rows[0].GetProperty("animeId").GetInt32());
        Assert.Equal("Left title", rows[0].GetProperty("title").GetString());
        Assert.Equal("Left english", rows[0].GetProperty("englishTitle").GetString());
        Assert.Equal("https://img/7.jpg", rows[0].GetProperty("pictureUrl").GetString());
        Assert.Equal(1, rows[0].GetProperty("position").GetInt32());
        Assert.Equal(3, rows[0].GetProperty("previousPosition").GetInt32());
        Assert.Equal(0, run.GetProperty("upcomingPictureUrls").GetArrayLength());
    }

    [Fact]
    public async Task AScoreWithNoRankingAnswersANullRun()
    {
        var fake = new FakeService { Score = new ThisOrThatScoreState(8, 149, 937, null) };
        await using var host = await TestHost.StartAsync(fake);

        using var json = await Json(await host.Client.GetAsync("api/this-or-that/8"));

        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("run").ValueKind);
        Assert.Equal(937, json.RootElement.GetProperty("maxPicks").GetInt32());
    }

    [Fact]
    public async Task AScoreThatIsNotOfferedIsNotFound()
    {
        var fake = new FakeService { Score = null };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/this-or-that/3");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Score 3 has nothing to rank.", await ErrorAsync(response));
    }

    [Fact]
    public async Task AScoreThatIsNotANumberMatchesNoRoute()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.GetAsync("api/this-or-that/abc");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Null(fake.ScoreRead);
    }

    // ---- POST api/this-or-that/{score} ----

    [Fact]
    public async Task StartPassesTheVersionBeingReplacedAndAnswersTheFirstState()
    {
        var fake = new FakeService { Started = State };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8", new { replacingVersion = 3 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((8, (int?)3), fake.StartCall);
        using var json = await Json(response);
        Assert.Equal(4, json.RootElement.GetProperty("run").GetProperty("version").GetInt32());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"replacingVersion\":null}")]
    [InlineData("")]
    public async Task StartWithNoVersionOrNoBodyAtAllIsAFirstStart(string body)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/this-or-that/8", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((8, (int?)null), fake.StartCall);
    }

    [Fact]
    public async Task StartAnswersBadRequestForAScoreWithFewerThanTwoAnime()
    {
        var fake = new FakeService { Throw = new ThisOrThatNotEnoughAnimeException(9) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/9", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Score 9 needs at least two anime to rank.", await ErrorAsync(response));
    }

    [Fact]
    public async Task StartAnswersConflictWhenARankingIsInProgressAndTheVersionIsNotItsOwn()
    {
        var fake = new FakeService { Throw = new ThisOrThatStaleException("The ranking of score 8 has moved on.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8", new { replacingVersion = 1 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The ranking of score 8 has moved on.", await ErrorAsync(response));
    }

    // ---- POST api/this-or-that/{score}/picks ----

    [Fact]
    public async Task APickPassesTheScoreThePairAndTheWinnerThroughAndAnswersTheNextState()
    {
        var fake = new FakeService { Picked = State };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/picks", new { leftId = 7, rightId = 9, winnerId = 9 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((8, 7, 9, (int?)9), fake.PickCall);
        using var json = await Json(response);
        Assert.Equal(7, json.RootElement.GetProperty("run").GetProperty("pair").GetProperty("left").GetProperty("animeId").GetInt32());
    }

    [Theory]
    [InlineData("{\"leftId\":7,\"rightId\":9,\"winnerId\":null}")]
    [InlineData("{\"leftId\":7,\"rightId\":9}")]
    public async Task ANullOrMissingWinnerIdIsCantDecide(string body)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/this-or-that/8/picks", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((8, 7, 9, (int?)null), fake.PickCall);
    }

    [Fact]
    public async Task APickWhosePairIsNotTheCurrentOneIsAConflict()
    {
        var fake = new FakeService { Throw = new ThisOrThatStaleException("That is no longer the current pair.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/picks", new { leftId = 7, rightId = 9, winnerId = 7 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("That is no longer the current pair.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickOfAWinnerThatIsNeitherCardIsABadRequest()
    {
        var fake = new FakeService { Throw = new ThisOrThatWinnerNotInPairException(55) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/picks", new { leftId = 7, rightId = 9, winnerId = 55 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Anime 55 is not one of the two cards.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickWithNoRankingInProgressIsNotFound()
    {
        var fake = new FakeService { Throw = new ThisOrThatRunNotFoundException(8) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/picks", new { leftId = 7, rightId = 9, winnerId = 7 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("No ranking of score 8 is in progress.", await ErrorAsync(response));
    }

    [Fact]
    public async Task APickWithNoBodyIsABadRequestAndNeverReachesTheService()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsync(
            "api/this-or-that/8/picks", new StringContent("", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.PickCall);
    }

    // ---- POST api/this-or-that/{score}/undo ----

    [Fact]
    public async Task UndoPassesTheVersionThroughAndAnswersTheState()
    {
        var fake = new FakeService { Undone = State };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/undo", new { version = 4 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal((8, 4), fake.UndoCall);
        using var json = await Json(response);
        Assert.Equal(200, json.RootElement.GetProperty("run").GetProperty("picksMade").GetInt32());
    }

    [Theory]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.Conflict)]
    public async Task UndoWithNoRankingIsNotFoundAndWithAStaleVersionOrNothingToUndoIsAConflict(bool noRun, HttpStatusCode expected)
    {
        Exception thrown = noRun ? new ThisOrThatRunNotFoundException(8) : new ThisOrThatStaleException("moved on");
        var fake = new FakeService { Throw = thrown };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/undo", new { version = 4 });

        Assert.Equal(expected, response.StatusCode);
    }

    // ---- POST api/this-or-that/{score}/save ----

    [Fact]
    public async Task SaveAnswersNoContentAndPassesTheVersionThrough()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/save", new { version = 9 });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal((8, 9), fake.SaveCall);
    }

    [Fact]
    public async Task SaveWithNoRankingIsNotFound()
    {
        var fake = new FakeService { Throw = new ThisOrThatRunNotFoundException(8) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/save", new { version = 9 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SaveWithAStaleVersionIsAConflict()
    {
        var fake = new FakeService { Throw = new ThisOrThatStaleException("The ranking of score 8 has moved on.") };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/save", new { version = 9 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The ranking of score 8 has moved on.", await ErrorAsync(response));
    }

    [Fact]
    public async Task SaveBeforeTheRankingIsFinishedIsAConflict()
    {
        var fake = new FakeService { Throw = new ThisOrThatNotFinishedException(8) };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.PostAsJsonAsync("api/this-or-that/8/save", new { version = 9 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("The ranking of score 8 isn't finished.", await ErrorAsync(response));
    }

    // ---- DELETE api/this-or-that/{score}?version=N ----

    [Fact]
    public async Task DiscardTakesTheVersionFromTheQueryStringAndAnswersNoContent()
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.DeleteAsync("api/this-or-that/8?version=12");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal((8, 12), fake.DiscardCall);
    }

    [Theory]
    [InlineData("api/this-or-that/8")]
    [InlineData("api/this-or-that/8?version=")]
    [InlineData("api/this-or-that/8?version=abc")]
    public async Task DiscardWithNoUsableVersionIsABadRequestAndNeverReachesTheService(string url)
    {
        var fake = new FakeService();
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.DeleteAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Null(fake.DiscardCall);
    }

    [Theory]
    [InlineData(true, HttpStatusCode.NotFound)]
    [InlineData(false, HttpStatusCode.Conflict)]
    public async Task DiscardWithNoRankingIsNotFoundAndWithAStaleVersionIsAConflict(bool noRun, HttpStatusCode expected)
    {
        Exception thrown = noRun ? new ThisOrThatRunNotFoundException(8) : new ThisOrThatStaleException("moved on");
        var fake = new FakeService { Throw = thrown };
        await using var host = await TestHost.StartAsync(fake);

        var response = await host.Client.DeleteAsync("api/this-or-that/8?version=1");

        Assert.Equal(expected, response.StatusCode);
    }

    // ---- the routes themselves ----

    [Theory]
    [InlineData("POST", "api/this-or-that")]
    [InlineData("PUT", "api/this-or-that/8")]
    [InlineData("GET", "api/this-or-that/8/picks")]
    [InlineData("GET", "api/this-or-that/8/undo")]
    [InlineData("GET", "api/this-or-that/8/save")]
    [InlineData("DELETE", "api/this-or-that/8/save")]
    [InlineData("PUT", "api/this-or-that/8/picks")]
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

    /// <summary>Stands in for the service: records what the controller passed it and
    /// answers what a test set, or throws the exception it set.</summary>
    private sealed class FakeService : IThisOrThatService
    {
        public Exception? Throw { get; init; }
        public ThisOrThatScoresDto Scores { get; init; } = new([]);
        public ThisOrThatScoreState? Score { get; init; } = State;
        public ThisOrThatScoreState Started { get; init; } = State;
        public ThisOrThatScoreState Picked { get; init; } = State;
        public ThisOrThatScoreState Undone { get; init; } = State;

        public int? ScoreRead { get; private set; }
        public (int Score, int? ReplacingVersion)? StartCall { get; private set; }
        public (int Score, int LeftId, int RightId, int? WinnerId)? PickCall { get; private set; }
        public (int Score, int Version)? UndoCall { get; private set; }
        public (int Score, int Version)? SaveCall { get; private set; }
        public (int Score, int Version)? DiscardCall { get; private set; }

        public Task<ThisOrThatScoresDto> GetScoresAsync(CancellationToken ct = default) => Task.FromResult(Scores);

        public Task<ThisOrThatScoreState?> GetScoreAsync(int score, CancellationToken ct = default)
        {
            ScoreRead = score;
            return Task.FromResult(Score);
        }

        public Task<ThisOrThatScoreState> StartAsync(int score, int? replacingVersion, CancellationToken ct = default)
        {
            StartCall = (score, replacingVersion);
            return Throw is null ? Task.FromResult(Started) : Task.FromException<ThisOrThatScoreState>(Throw);
        }

        public Task<ThisOrThatScoreState> PickAsync(int score, int leftId, int rightId, int? winnerId, CancellationToken ct = default)
        {
            PickCall = (score, leftId, rightId, winnerId);
            return Throw is null ? Task.FromResult(Picked) : Task.FromException<ThisOrThatScoreState>(Throw);
        }

        public Task<ThisOrThatScoreState> UndoAsync(int score, int version, CancellationToken ct = default)
        {
            UndoCall = (score, version);
            return Throw is null ? Task.FromResult(Undone) : Task.FromException<ThisOrThatScoreState>(Throw);
        }

        public Task SaveAsync(int score, int version, CancellationToken ct = default)
        {
            SaveCall = (score, version);
            return Throw is null ? Task.CompletedTask : Task.FromException(Throw);
        }

        public Task DiscardAsync(int score, int version, CancellationToken ct = default)
        {
            DiscardCall = (score, version);
            return Throw is null ? Task.CompletedTask : Task.FromException(Throw);
        }
    }

    /// <summary>Only the one controller, so no other route or dependency is
    /// involved.</summary>
    private sealed class ControllerOnly(params Type[] types) : ApplicationPart, IApplicationPartTypeProvider
    {
        public override string Name => "ThisOrThatController";
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

        public static async Task<TestHost> StartAsync(IThisOrThatService service)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { ContentRootPath = AppContext.BaseDirectory });
            builder.Logging.ClearProviders();
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Services.AddSingleton(service);
            builder.Services.AddControllers()
                .ConfigureApplicationPartManager(manager =>
                {
                    manager.ApplicationParts.Clear();
                    manager.ApplicationParts.Add(new ControllerOnly(typeof(ThisOrThatController)));
                });

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
