using System.Net;
using System.Text;
using AnimeTracker.Api.Services.Airing.AniList;
using AnimeTracker.Api.Services.Setup;
using Microsoft.Extensions.Logging.Abstractions;

namespace AnimeTracker.Api.Tests.Services.Airing.AniList;

// episode-airing-data spec, "AniList request pacing", and first-run-setup,
// "Throttling is shown with its resume time"; design D11 and D12. The client's
// pacer and health share one manual clock, and a wait moves it forward by
// exactly that much, so nothing here sleeps.
public class AniListClientTests
{
    private const string MediaBody =
        """{"data":{"Media":{"id":5,"status":"FINISHED","episodes":12,"nextAiringEpisode":null,"relations":{"edges":[]}}}}""";

    private sealed class FakeAniListHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
        public int Requests { get; private set; }

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> respond) => _responses.Enqueue(respond);

        public void Enqueue(HttpStatusCode status, string body = "{}", params (string Name, string Value)[] headers) =>
            Enqueue(_ => Response(status, body, headers));

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more stubbed responses.");
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private static HttpResponseMessage Response(HttpStatusCode status, string body, params (string Name, string Value)[] headers)
    {
        var response = new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        foreach (var (name, value) in headers)
            response.Headers.TryAddWithoutValidation(name, value);
        return response;
    }

    private sealed class Harness
    {
        public DateTimeOffset Now { get; set; } = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        public List<TimeSpan> Waits { get; } = [];
        // What the health reported as each wait began, before the clock moved.
        public List<DateTimeOffset?> ThrottleSeenAtWait { get; } = [];
        public FakeAniListHandler Handler { get; } = new();
        public AniListRequestPacer Pacer { get; }
        public AniListServiceHealth Health { get; }
        public AniListClient Client { get; }

        public Harness()
        {
            Health = new AniListServiceHealth(() => Now);
            Pacer = new AniListRequestPacer(() => Now, (wait, _) =>
            {
                Waits.Add(wait);
                ThrottleSeenAtWait.Add(Health.ThrottledUntil);
                Now += wait;
                return Task.CompletedTask;
            });
            Client = new AniListClient(
                new HttpClient(Handler) { BaseAddress = new Uri("https://graphql.anilist.co/") },
                Pacer, Health, NullLogger<AniListClient>.Instance);
        }

        public Task<AniListMediaLookup?> LookupAsync(CancellationToken ct = default) => Client.LookupByMalIdAsync(1, ct);

        public string ResetHeaderIn(TimeSpan span) => (Now + span).ToUnixTimeSeconds().ToString();
    }

    [Fact]
    public async Task TheRateLimitHeadersOfEveryResponseFeedThePacer()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody, ("X-RateLimit-Limit", "90"), ("X-RateLimit-Remaining", "89"));

        var lookup = await h.LookupAsync();

        Assert.Equal(5, lookup!.AniListId);
        Assert.Equal(TimeSpan.FromSeconds(60.0 / 90), h.Pacer.Interval);
    }

    [Fact]
    public async Task ARemainingCountOfZeroWithAResetHoldsTheNextRequestUntilThen()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody,
            ("X-RateLimit-Limit", "30"), ("X-RateLimit-Remaining", "0"), ("X-RateLimit-Reset", h.ResetHeaderIn(TimeSpan.FromSeconds(20))));
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        await h.LookupAsync();
        await h.LookupAsync();

        Assert.Equal([TimeSpan.FromSeconds(20)], h.Waits);
    }

    [Fact]
    public async Task ARemainingCountOfZeroWithNoResetHoldsTheNextRequestAMinute()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody, ("X-RateLimit-Limit", "30"), ("X-RateLimit-Remaining", "0"));
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        await h.LookupAsync();
        await h.LookupAsync();

        Assert.Equal([TimeSpan.FromMinutes(1)], h.Waits);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("-3")]
    [InlineData("99999999999999999999")]
    public async Task HeadersThatAreNotNumbersAreIgnored(string junk)
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody,
            ("X-RateLimit-Limit", junk), ("X-RateLimit-Remaining", junk), ("X-RateLimit-Reset", junk));

        await h.LookupAsync();

        Assert.Equal(TimeSpan.FromSeconds(2), h.Pacer.Interval);
        Assert.Empty(h.Waits);
    }

    [Fact]
    public async Task A429WaitsAsLongAsRetryAfterAsksThenRetries()
    {
        var h = new Harness();
        var expectedResume = h.Now + TimeSpan.FromSeconds(30);
        h.Handler.Enqueue(HttpStatusCode.TooManyRequests, "{}", ("Retry-After", "30"));
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        var lookup = await h.LookupAsync();

        Assert.Equal(5, lookup!.AniListId);
        Assert.Equal(2, h.Handler.Requests);
        Assert.Equal([TimeSpan.FromSeconds(30)], h.Waits);
        // Published while the wait began, not only afterwards.
        Assert.Equal([expectedResume], h.ThrottleSeenAtWait);
    }

    [Fact]
    public async Task TheThrottleIsClearedByTheNextAnswerThatIsNotOne()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.TooManyRequests, "{}", ("Retry-After", "30"));
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        await h.LookupAsync();

        Assert.Null(h.Health.ThrottledUntil);
        Assert.False(h.Health.IsDown);
        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task A429WithNoRetryAfterWaitsAFullMinute()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.TooManyRequests);
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        await h.LookupAsync();

        Assert.Equal([TimeSpan.FromMinutes(1)], h.Waits);
    }

    [Fact]
    public async Task A429IsRetriedTwiceThenGivenUpOnAsOneFailure()
    {
        var h = new Harness();
        for (var i = 0; i < 3; i++)
            h.Handler.Enqueue(HttpStatusCode.TooManyRequests, "{}", ("Retry-After", "10"));

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.Equal(HttpStatusCode.TooManyRequests, ex.StatusCode);
        Assert.Equal(3, h.Handler.Requests);
        Assert.Equal(1, h.Health.ConsecutiveFailures);
        Assert.Equal([TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10)], h.Waits);
        // AniList is still limiting: the wait stays reported, and the next request honours it.
        Assert.NotNull(h.Health.ThrottledUntil);
    }

    [Fact]
    public async Task AnAnswerWithErrorsAndNoDataIsAFailureNotAnEmptyAnswer()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, """{"errors":[{"message":"Internal server error"}],"data":null}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.Contains("Internal server error", ex.Message);
        Assert.Equal(1, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ErrorsWithNoDataKeyAtAllAreAlsoAFailure()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, """{"errors":[{"status":500}]}""");

        await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.Equal(1, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task AnAnswerWithDataIsNotAFailureEvenIfItAlsoReportsErrors()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK,
            """{"errors":[{"message":"one field failed"}],"data":{"Media":{"id":5,"status":"FINISHED","episodes":12,"relations":{"edges":[]}}}}""");

        var lookup = await h.LookupAsync();

        Assert.Equal(5, lookup!.AniListId);
        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ABodyWithNoDataAndNoErrorsIsStillNoData()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, "{}");

        Assert.Null(await h.LookupAsync());
        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task A404IsNoDataAndCountsAsAnAnswer()
    {
        var h = new Harness();
        h.Health.RecordTemporaryFailure();
        h.Health.RecordTemporaryFailure();
        h.Handler.Enqueue(HttpStatusCode.NotFound, """{"errors":[{"message":"Not Found."}],"data":{"Media":null}}""");

        Assert.Null(await h.LookupAsync());

        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task AServerErrorFailsTheCallAndCountsAgainstTheService()
    {
        var h = new Harness();
        for (var i = 0; i < 3; i++)
            h.Handler.Enqueue(HttpStatusCode.BadGateway);

        for (var i = 0; i < 3; i++)
            await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.True(h.Health.IsDown);
    }

    [Fact]
    public async Task ARefusedQueryFailsTheCallButIsNoStrikeAgainstTheService()
    {
        // A 400 (a bad query, or "page depth exceeds the maximum") is AniList
        // answering: it would fail the same way on every retry, so it must not
        // pause the queue as an outage does.
        var h = new Harness();
        h.Health.RecordTemporaryFailure();
        h.Handler.Enqueue(HttpStatusCode.BadRequest, """{"errors":[{"message":"Page depth exceeds maximum"}],"data":{"Page":null}}""");

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ANetworkErrorCountsAgainstTheService()
    {
        var h = new Harness();
        h.Handler.Enqueue(_ => throw new HttpRequestException("connection refused"));

        await Assert.ThrowsAsync<HttpRequestException>(() => h.LookupAsync());

        Assert.Equal(1, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ATimeoutCountsAgainstTheService()
    {
        // HttpClient reports its own timeout as a cancellation while the
        // caller's token is still live.
        var h = new Harness();
        h.Handler.Enqueue(_ => throw new TaskCanceledException("timed out"));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.LookupAsync());

        Assert.Equal(1, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task TheCallersOwnCancellationIsNoFailureOfTheService()
    {
        var h = new Harness();
        using var cts = new CancellationTokenSource();
        h.Handler.Enqueue(_ =>
        {
            cts.Cancel();
            throw new TaskCanceledException("cancelled");
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => h.LookupAsync(cts.Token));

        Assert.Equal(0, h.Health.ConsecutiveFailures);
    }

    [Fact]
    public async Task AGoodAnswerAfterFailuresTakesTheServiceBackUp()
    {
        var h = new Harness();
        for (var i = 0; i < 3; i++)
            h.Health.RecordTemporaryFailure();
        Assert.True(h.Health.IsDown);
        h.Handler.Enqueue(HttpStatusCode.OK, MediaBody);

        await h.LookupAsync();

        Assert.False(h.Health.IsDown);
    }

    // --- The batched reads (add-first-run-setup design D10) ---

    private static string Body(HttpRequestMessage request) => request.Content!.ReadAsStringAsync().Result;

    private static string MediaPage(params string[] media) =>
        "{\"data\":{\"Page\":{\"media\":[" + string.Join(",", media) + "]}}}";

    private static string MediaJson(int id, int? idMal, string status = "FINISHED", int episodes = 12, string next = "null", string relations = "[]") =>
        "{\"id\":" + id + ",\"idMal\":" + (idMal is null ? "null" : idMal.ToString()) + ",\"status\":\"" + status + "\",\"episodes\":" + episodes
        + ",\"nextAiringEpisode\":" + next + ",\"relations\":{\"edges\":" + relations + "}}";

    [Fact]
    public async Task ALookupBatchSendsItsIdsAtPerPage50AndKeysTheAnswerByMalId()
    {
        var h = new Harness();
        string? sent = null;
        h.Handler.Enqueue(request =>
        {
            sent = Body(request);
            return Response(HttpStatusCode.OK, MediaPage(
                MediaJson(500, 10, "RELEASING", 0, """{"episode":3,"airingAt":1800000000}""", """[{"relationType":"SEQUEL","node":{"idMal":11}}]"""),
                MediaJson(501, 12)));
        });

        var found = await h.Client.LookupBatchByMalIdsAsync([10, 11, 12]);

        Assert.Contains("\"perPage\":50", sent);
        Assert.Contains("\"idMalIn\":[10,11,12]", sent);
        Assert.Equal([10, 12], found.Keys.Order()); // MAL id 11 has no media: simply absent
        var airing = found[10];
        Assert.Equal(500, airing.AniListId);
        Assert.Equal("RELEASING", airing.Status);
        Assert.Null(airing.Episodes); // 0 is read as unknown, as the single lookup does
        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1800000000), airing.NextAiringEpisodeAtUtc);
        Assert.Equal(new AniListRelationEdge("SEQUEL", 11), Assert.Single(airing.Relations));
        Assert.Equal(12, found[12].Episodes);
        Assert.Equal(1, h.Handler.Requests); // one request, and no hasNextPage loop
    }

    [Fact]
    public async Task WhereAniListHoldsTwoMediaForOneMalIdTheLowestAniListIdIsKept()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, MediaPage(MediaJson(700, 10), MediaJson(600, 10)));

        var found = await h.Client.LookupBatchByMalIdsAsync([10]);

        Assert.Equal(600, found[10].AniListId);
    }

    [Fact]
    public async Task ALookupBatchOfMoreThan25IdsIsRefusedBeforeAnyRequest()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ArgumentException>(() => h.Client.LookupBatchByMalIdsAsync(Enumerable.Range(1, 26).ToList()));

        Assert.Equal(0, h.Handler.Requests);
    }

    [Fact]
    public async Task AnEmptyBatchMakesNoRequest()
    {
        var h = new Harness();

        Assert.Empty(await h.Client.LookupBatchByMalIdsAsync([]));
        Assert.Empty(await h.Client.GetMediaBatchAsync([]));
        Assert.Empty(await h.Client.GetAiringSchedulesAsync([], (_, _) => Task.CompletedTask));

        Assert.Equal(0, h.Handler.Requests);
    }

    [Fact]
    public async Task AMediaBatchKeysTheAnswerByAniListIdAndOmitsAnIdAniListNoLongerHas()
    {
        var h = new Harness();
        string? sent = null;
        h.Handler.Enqueue(request =>
        {
            sent = Body(request);
            return Response(HttpStatusCode.OK, MediaPage(MediaJson(500, 10, "RELEASING", 24, """{"episode":9,"airingAt":1800000000}""")));
        });

        var found = await h.Client.GetMediaBatchAsync([500, 501]);

        Assert.Contains("\"idIn\":[500,501]", sent);
        var state = Assert.Single(found).Value;
        Assert.Equal(new AniListMediaState(500, "RELEASING", DateTimeOffset.FromUnixTimeSeconds(1800000000), 24), state);
    }

    [Fact]
    public async Task AMediaBatchOfMoreThan50IdsIsRefused()
    {
        var h = new Harness();

        await Assert.ThrowsAsync<ArgumentException>(() => h.Client.GetMediaBatchAsync(Enumerable.Range(1, 51).ToList()));
    }

    private static string ScheduleRows(params (int MediaId, int Episode)[] rows) =>
        string.Join(",", rows.Select(r =>
            "{\"mediaId\":" + r.MediaId + ",\"episode\":" + r.Episode + ",\"airingAt\":" + (1700000000 + r.Episode * 604800L) + "}"));

    private static string SchedulePage(bool hasNextPage, string rows) =>
        "{\"data\":{\"Page\":{\"pageInfo\":{\"hasNextPage\":" + (hasNextPage ? "true" : "false") + "},\"airingSchedules\":[" + rows + "]}}}";

    [Fact]
    public async Task AScheduleReadHandsOverEachAnimeAsSoonAsItsBlockEnds()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(true, ScheduleRows((1, 1), (1, 2), (2, 1))));
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(false, ScheduleRows((2, 2), (3, 1))));
        var handedOver = new List<(int AniListId, int Rows, int RequestsSoFar)>();

        var incomplete = await h.Client.GetAiringSchedulesAsync([1, 2, 3], (id, rows) =>
        {
            handedOver.Add((id, rows.Count, h.Handler.Requests));
            return Task.CompletedTask;
        });

        Assert.Empty(incomplete);
        // Anime 1 is complete on page 1, the moment anime 2's first row shows it is over; 2 needs page 2.
        Assert.Equal([(1, 2, 1), (2, 2, 2), (3, 1, 2)], handedOver);
    }

    [Fact]
    public async Task AnAnimeWithNoRowsIsHandedOverEmptyWhenTheReadEnds()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(false, ScheduleRows((2, 1))));
        var handedOver = new List<(int AniListId, int Rows)>();

        await h.Client.GetAiringSchedulesAsync([1, 2], (id, rows) =>
        {
            handedOver.Add((id, rows.Count));
            return Task.CompletedTask;
        });

        Assert.Equal([(2, 1), (1, 0)], handedOver);
    }

    [Fact]
    public async Task ARowsAreOrderedByAirInstantWhateverOrderTheyArrivedIn()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(false, ScheduleRows((1, 3), (1, 1), (1, 2))));
        IReadOnlyList<AniListEpisode>? rows = null;

        await h.Client.GetAiringSchedulesAsync([1], (_, r) =>
        {
            rows = r;
            return Task.CompletedTask;
        });

        Assert.Equal([1, 2, 3], rows!.Select(r => r.Episode));
    }

    [Fact]
    public async Task AScheduleReadStopsAfterPage100AndReturnsWhatWasNotHandedOver()
    {
        var h = new Harness();
        // Anime 1 fills all 100 pages and is still going, and anime 2 is never reached.
        for (var page = 1; page <= 100; page++)
            h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(true, ScheduleRows((1, page))));
        var handedOver = new List<int>();

        var incomplete = await h.Client.GetAiringSchedulesAsync([1, 2], (id, _) =>
        {
            handedOver.Add(id);
            return Task.CompletedTask;
        });

        Assert.Equal(100, h.Handler.Requests); // page 101 would be an HTTP 400, so it is never asked for
        Assert.Empty(handedOver);
        Assert.Equal([1, 2], incomplete.Order());
    }

    [Fact]
    public async Task AnAnimeCompleteBeforeTheCapIsHandedOverAndTheCutOffOneIsReturned()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(true, ScheduleRows((1, 1), (2, 1))));
        for (var page = 2; page <= 100; page++)
            h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(true, ScheduleRows((2, page))));
        var handedOver = new List<int>();

        var incomplete = await h.Client.GetAiringSchedulesAsync([1, 2, 3], (id, _) =>
        {
            handedOver.Add(id);
            return Task.CompletedTask;
        });

        Assert.Equal([1], handedOver);
        Assert.Equal([2, 3], incomplete.Order());
    }

    [Fact]
    public async Task ARefusedPageIsNeverReadAsNoData()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(true, ScheduleRows((1, 1))));
        h.Handler.Enqueue(HttpStatusCode.BadRequest, """{"errors":[{"message":"Page depth exceeds maximum"}],"data":{"Page":null}}""");
        var handedOver = new List<int>();

        await Assert.ThrowsAsync<HttpRequestException>(() => h.Client.GetAiringSchedulesAsync([1], (id, _) =>
        {
            handedOver.Add(id);
            return Task.CompletedTask;
        }));

        Assert.Empty(handedOver); // anime 1's rows were cut off; saving them would replace a whole set with a part
    }

    [Fact]
    public async Task ABlockThatComesBackAfterItsAnimeWasHandedOverIsIgnored()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(false, ScheduleRows((1, 1), (2, 1), (1, 2))));
        var handedOver = new List<(int Id, int Rows)>();

        await h.Client.GetAiringSchedulesAsync([1, 2], (id, rows) =>
        {
            handedOver.Add((id, rows.Count));
            return Task.CompletedTask;
        });

        Assert.Equal([(1, 1), (2, 1)], handedOver);
    }

    [Fact]
    public async Task ARowForAnAnimeThatWasNotAskedForIsIgnored()
    {
        var h = new Harness();
        h.Handler.Enqueue(HttpStatusCode.OK, SchedulePage(false, ScheduleRows((1, 1), (9, 1))));
        var handedOver = new List<int>();

        await h.Client.GetAiringSchedulesAsync([1], (id, _) =>
        {
            handedOver.Add(id);
            return Task.CompletedTask;
        });

        Assert.Equal([1], handedOver);
    }
}
