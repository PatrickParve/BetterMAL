using System.Net;
using System.Net.Http.Headers;
using System.Text;
using AnimeTracker.Api.Services.Tmdb;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Tmdb;

// tmdb-artwork spec: the three images endpoints, the language filter, the
// status handling and the key hygiene (design.md D4), over a real HttpClient
// and a scripted handler that records every request.
public class TmdbClientTests
{
    private const string ApiKey = "test-key-0123456789abcdef";

    private static WaitRecordingClient CreateClient(
        ScriptedHandler handler, string apiKey = ApiKey, ILogger<TmdbClient>? logger = null) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.themoviedb.org/3/") },
            Options.Create(new TmdbOptions { ApiKey = apiKey }),
            logger ?? NullLogger<TmdbClient>.Instance);

    // A response in TMDB's real shape: extra fields the client must ignore
    // (aspect_ratio, votes, an "id"), a logos array, and a null language.
    private const string TvImagesJson = """
        {
          "id": 1429,
          "backdrops": [
            {"aspect_ratio":1.778,"height":1080,"iso_639_1":null,"file_path":"/backdrop-none.jpg","vote_average":5.3,"vote_count":3,"width":1920},
            {"aspect_ratio":1.778,"height":2160,"iso_639_1":"ja","file_path":"/backdrop-ja.jpg","vote_average":5.1,"vote_count":1,"width":3840}
          ],
          "logos": [
            {"aspect_ratio":3.0,"height":100,"iso_639_1":"en","file_path":"/logo-en.png","vote_average":5.0,"vote_count":1,"width":300}
          ],
          "posters": [
            {"aspect_ratio":0.667,"height":3000,"iso_639_1":"ja","file_path":"/poster-ja.jpg","vote_average":5.1,"vote_count":2,"width":2000},
            {"aspect_ratio":0.667,"height":1500,"iso_639_1":"en","file_path":"/poster-en.jpg","vote_average":5.0,"vote_count":1,"width":1000}
          ]
        }
        """;

    private static TmdbImagesResult.Images AssertImages(TmdbImagesResult result) =>
        Assert.IsType<TmdbImagesResult.Images>(result);

    // --- Endpoints ---

    [Fact]
    public async Task GetTvImagesAsync_RequestsTheExactPathAndQuery()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, TvImagesJson);

        await CreateClient(handler).GetTvImagesAsync(1429);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("api.themoviedb.org", request.Host);
        Assert.Equal("/3/tv/1429/images", request.AbsolutePath);
        Assert.Equal($"?include_image_language=ja,en,null&api_key={ApiKey}", request.Query);
    }

    [Fact]
    public async Task GetSeasonImagesAsync_RequestsTheExactPathAndQuery()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, "{}");

        await CreateClient(handler).GetSeasonImagesAsync(85937, 2);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/3/tv/85937/season/2/images", request.AbsolutePath);
        Assert.Equal($"?include_image_language=ja,en,null&api_key={ApiKey}", request.Query);
    }

    [Fact]
    public async Task GetMovieImagesAsync_RequestsTheExactPathAndQuery()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, "{}");

        await CreateClient(handler).GetMovieImagesAsync(635302);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/3/movie/635302/images", request.AbsolutePath);
        Assert.Equal($"?include_image_language=ja,en,null&api_key={ApiKey}", request.Query);
    }

    // --- Parsing ---

    [Fact]
    public async Task ImagesKeepPathLanguageAndSizeInTmdbsOrder()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, TvImagesJson);

        var images = AssertImages(await CreateClient(handler).GetTvImagesAsync(1429));

        Assert.Equal(
            [new TmdbImage("/poster-ja.jpg", "ja", 2000, 3000), new TmdbImage("/poster-en.jpg", "en", 1000, 1500)],
            images.Posters);
        Assert.Equal(
            [new TmdbImage("/backdrop-none.jpg", null, 1920, 1080), new TmdbImage("/backdrop-ja.jpg", "ja", 3840, 2160)],
            images.Backdrops);
    }

    [Fact]
    public async Task ALogosArrayIsIgnored()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, TvImagesJson);

        var images = AssertImages(await CreateClient(handler).GetTvImagesAsync(1429));

        Assert.DoesNotContain(images.Posters.Concat(images.Backdrops), i => i.FilePath == "/logo-en.png");
        Assert.Equal(2, images.Posters.Count);
        Assert.Equal(2, images.Backdrops.Count);
    }

    [Fact]
    public async Task AnImageInAnotherLanguageIsDropped()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """
            {"posters":[
              {"iso_639_1":"ja","file_path":"/ja.jpg","width":2,"height":3},
              {"iso_639_1":"fr","file_path":"/fr.jpg","width":2,"height":3},
              {"iso_639_1":null,"file_path":"/none.jpg","width":2,"height":3},
              {"iso_639_1":"en","file_path":"/en.jpg","width":2,"height":3}
            ],"backdrops":[]}
            """);

        var images = AssertImages(await CreateClient(handler).GetTvImagesAsync(1));

        Assert.Equal(["/ja.jpg", "/none.jpg", "/en.jpg"], images.Posters.Select(i => i.FilePath));
    }

    [Fact]
    public async Task AnEntryWithNoFilePathIsSkipped()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """
            {"posters":[{"iso_639_1":"ja","width":2,"height":3},{"iso_639_1":"ja","file_path":"/ok.jpg","width":2,"height":3}]}
            """);

        var images = AssertImages(await CreateClient(handler).GetTvImagesAsync(1));

        Assert.Equal(["/ok.jpg"], images.Posters.Select(i => i.FilePath));
    }

    [Fact]
    public async Task ASeasonResponseWithNoBackdropsKeyParsesToEmptyBackdrops()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, """
            {"id":"5a1","posters":[{"iso_639_1":"ja","file_path":"/s2.jpg","width":2000,"height":3000}]}
            """);

        var images = AssertImages(await CreateClient(handler).GetSeasonImagesAsync(85937, 2));

        Assert.Equal(["/s2.jpg"], images.Posters.Select(i => i.FilePath));
        Assert.Empty(images.Backdrops);
    }

    [Fact]
    public async Task ASeasonIsPostersOnlyEvenIfTheResponseCarriesBackdrops()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, TvImagesJson);

        var images = AssertImages(await CreateClient(handler).GetSeasonImagesAsync(1429, 1));

        Assert.Equal(2, images.Posters.Count);
        Assert.Empty(images.Backdrops);
    }

    [Fact]
    public async Task AMalformedBodyIsAFailureNotAnException()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.OK, "{ this is not json");

        var result = await CreateClient(handler).GetTvImagesAsync(1);

        Assert.IsType<TmdbImagesResult.Failed>(result);
    }

    // --- Status handling ---

    [Fact]
    public async Task ANotFoundIsNotFound()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.NotFound, """{"success":false,"status_code":34}""");

        var result = await CreateClient(handler).GetMovieImagesAsync(1);

        Assert.IsType<TmdbImagesResult.NotFound>(result);
        Assert.Single(handler.Requests); // an answer, not retried
    }

    [Fact]
    public async Task OneRateLimitThenASuccessGivesImagesAfterTwoRequests()
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.FromSeconds(2))
            .Respond(HttpStatusCode.OK, TvImagesJson);
        var client = CreateClient(handler);

        var result = await client.GetTvImagesAsync(1429);

        AssertImages(result);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal([TimeSpan.FromSeconds(2)], client.Waits); // waited as long as TMDB asked
    }

    [Theory]
    [InlineData(null, 2)] // no Retry-After: the default
    [InlineData(3, 3)]
    [InlineData(60, 10)] // capped
    public async Task TheRateLimitWaitDefaultsToTwoSecondsAndIsCappedAtTen(int? retryAfterSeconds, int expectedSeconds)
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.TooManyRequests, "{}",
                retryAfter: retryAfterSeconds is { } seconds ? TimeSpan.FromSeconds(seconds) : null)
            .Respond(HttpStatusCode.OK, "{}");
        var client = CreateClient(handler);

        await client.GetTvImagesAsync(1);

        Assert.Equal([TimeSpan.FromSeconds(expectedSeconds)], client.Waits);
    }

    [Fact]
    public async Task TwoRateLimitsInARowFail()
    {
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.FromSeconds(1))
            .Respond(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.FromSeconds(1));
        var client = CreateClient(handler);

        var result = await client.GetTvImagesAsync(1);

        Assert.IsType<TmdbImagesResult.Failed>(result);
        Assert.Equal(2, handler.Requests.Count); // retried once, no more
        Assert.Single(client.Waits);
    }

    [Fact]
    public async Task ARejectedKeyFailsAndLogsAnErrorThatNamesNeitherTheKeyNorTheUri()
    {
        var logger = new CapturingLogger<TmdbClient>();
        var handler = new ScriptedHandler().Respond(HttpStatusCode.Unauthorized, """{"status_code":7}""");

        var result = await CreateClient(handler, logger: logger).GetTvImagesAsync(1429);

        Assert.IsType<TmdbImagesResult.Failed>(result);
        var error = Assert.Single(logger.Lines, line => line.StartsWith("Error:", StringComparison.Ordinal));
        Assert.DoesNotContain(ApiKey, error);
        Assert.DoesNotContain("api_key", error);
        Assert.DoesNotContain("api.themoviedb.org", error);
        Assert.Single(handler.Requests); // not retried
    }

    [Fact]
    public async Task AServerErrorFails()
    {
        var handler = new ScriptedHandler().Respond(HttpStatusCode.InternalServerError, "oops");

        var result = await CreateClient(handler).GetTvImagesAsync(1);

        Assert.IsType<TmdbImagesResult.Failed>(result);
    }

    [Fact]
    public async Task ATimeoutFails()
    {
        // What HttpClient throws when its Timeout elapses: a cancellation the
        // caller did not ask for.
        var handler = new ScriptedHandler().Throw(
            new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing.", new TimeoutException()));

        var result = await CreateClient(handler).GetTvImagesAsync(1);

        Assert.IsType<TmdbImagesResult.Failed>(result);
    }

    [Fact]
    public async Task ANetworkErrorFails()
    {
        var handler = new ScriptedHandler().Throw(new HttpRequestException("Name or service not known (api.themoviedb.org:443)"));

        var result = await CreateClient(handler).GetTvImagesAsync(1);

        Assert.IsType<TmdbImagesResult.Failed>(result);
    }

    [Fact]
    public async Task TheCallersOwnCancellationIsNotSwallowed()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var handler = new ScriptedHandler().Throw(new OperationCanceledException(cts.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => CreateClient(handler).GetTvImagesAsync(1, cts.Token));
    }

    // --- Key handling ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task NoKeySendsNoRequest(string blankKey)
    {
        var handler = new ScriptedHandler();
        var client = CreateClient(handler, apiKey: blankKey);

        var results = new[]
        {
            await client.GetTvImagesAsync(1),
            await client.GetSeasonImagesAsync(1, 1),
            await client.GetMovieImagesAsync(1),
        };

        Assert.All(results, r => Assert.IsType<TmdbImagesResult.Failed>(r));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TheKeyNeverAppearsInAnyLogLine()
    {
        // Every way a request can go wrong, each of which logs something. The
        // key travels in the query string, so nothing here may log a URI
        // (design.md D4) — checked over the formatted messages and the
        // exceptions' own text, stack traces included.
        var logger = new CapturingLogger<TmdbClient>();
        var handler = new ScriptedHandler()
            .Respond(HttpStatusCode.Unauthorized, "{}")
            .Respond(HttpStatusCode.InternalServerError, "{}")
            .Respond(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.FromSeconds(1))
            .Respond(HttpStatusCode.TooManyRequests, "{}", retryAfter: TimeSpan.FromSeconds(1))
            .Respond(HttpStatusCode.OK, "{ not json")
            .Respond(HttpStatusCode.OK, "null")
            .Throw(new HttpRequestException("Connection refused (api.themoviedb.org:443)"))
            .Throw(new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout of 15 seconds elapsing.", new TimeoutException()));
        var client = CreateClient(handler, logger: logger);

        for (var i = 0; i < 7; i++)
            Assert.IsType<TmdbImagesResult.Failed>(await client.GetTvImagesAsync(1429));

        Assert.NotEmpty(logger.Lines);
        Assert.All(logger.Lines, line => Assert.DoesNotContain(ApiKey, line));
    }

    // --- Doubles ---

    private sealed class WaitRecordingClient(HttpClient http, IOptions<TmdbOptions> options, ILogger<TmdbClient> logger)
        : TmdbClient(http, options, logger)
    {
        public List<TimeSpan> Waits { get; } = [];

        protected override Task DelayAsync(TimeSpan wait, CancellationToken ct)
        {
            Waits.Add(wait);
            return Task.CompletedTask;
        }
    }

    // Answers requests from a script, in order, and records each request's
    // URI. A request beyond the script fails loudly rather than quietly
    // getting some default answer.
    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _script = new();

        public List<Uri> Requests { get; } = [];

        public ScriptedHandler Respond(HttpStatusCode status, string body, TimeSpan? retryAfter = null)
        {
            _script.Enqueue(() =>
            {
                var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
                if (retryAfter is { } wait)
                    response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
                return response;
            });
            return this;
        }

        public ScriptedHandler Throw(Exception exception)
        {
            _script.Enqueue(() => throw exception);
            return this;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            if (!_script.TryDequeue(out var next))
                throw new InvalidOperationException($"No scripted response for request {Requests.Count}: {request.RequestUri!.AbsolutePath}");

            return Task.FromResult(next());
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add($"{logLevel}: {formatter(state, exception)} {exception}");
    }
}
