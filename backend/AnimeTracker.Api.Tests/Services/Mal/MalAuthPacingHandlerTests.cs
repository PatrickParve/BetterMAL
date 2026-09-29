using System.Net;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Setup;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Mal;

// MalAuthPacingHandler's one-shot 401 handling on a signed-in call
// (design.md D12), kept separate from the existing 403 backoff loop: a
// revoked login is noticed and retried exactly once, and an unauthenticated
// client-id request is never touched by any of this.
public class MalAuthPacingHandlerTests
{
    private static MalAuthPacingHandler CreateHandler(
        HttpMessageHandler inner, IMalTokenProvider tokenProvider, MalServiceHealth? health = null, TimeSpan? initialBackoff = null) =>
        new(Options.Create(new MalOptions { ClientId = "client-id" }), tokenProvider, new MalRequestPacer(TimeSpan.Zero),
            health ?? new MalServiceHealth(), NullLogger<MalAuthPacingHandler>.Instance, initialBackoff)
        { InnerHandler = inner };

    private static HttpRequestMessage BearerRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.myanimelist.net/v2/users/@me/animelist");
        request.Options.Set(MalRequestOptions.AuthModeKey, MalAuthMode.Bearer);
        return request;
    }

    private static HttpRequestMessage ClientIdRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.myanimelist.net/v2/anime/1");
        request.Options.Set(MalRequestOptions.AuthModeKey, MalAuthMode.ClientId);
        return request;
    }

    private static OAuthToken TokenRecord(string accessToken) => new()
    {
        AccessToken = accessToken,
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        UpdatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task A401RefreshesAndSendsTheRequestAgainOnceReturningItsResponse()
    {
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);
        inner.Enqueue(_ => new HttpResponseMessage(HttpStatusCode.OK));

        var tokenProvider = new FakeMalTokenProvider("token-1")
        {
            RefreshAfterRejectionResult = new MalRefreshResult.Refreshed(TokenRecord("token-2")),
        };

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider));
        var response = await invoker.SendAsync(BearerRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal("token-1", inner.Requests[0].Headers.Authorization!.Parameter);
        Assert.Equal("token-2", inner.Requests[1].Headers.Authorization!.Parameter);
        Assert.Equal(1, tokenProvider.RefreshAfterRejectionCallCount);
        Assert.Equal("token-1", tokenProvider.RejectedTokenReceived);
    }

    [Fact]
    public async Task RefusedThrowsMalAuthorizationRequiredException()
    {
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var tokenProvider = new FakeMalTokenProvider("token-1")
        {
            RefreshAfterRejectionResult = new MalRefreshResult.Refused(401, "invalid_grant"),
        };

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider));

        await Assert.ThrowsAsync<MalAuthorizationRequiredException>(() =>
            invoker.SendAsync(BearerRequest(), CancellationToken.None));
        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task UnavailableReturnsTheOriginal401Response()
    {
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var tokenProvider = new FakeMalTokenProvider("token-1")
        {
            RefreshAfterRejectionResult = new MalRefreshResult.Unavailable("MyAnimeList couldn't be reached."),
        };

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider));
        var response = await invoker.SendAsync(BearerRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Requests);
    }

    [Fact]
    public async Task ASecond401OnTheRetriedRequestIsReturnedWithoutAnotherRefresh()
    {
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var tokenProvider = new FakeMalTokenProvider("token-1")
        {
            RefreshAfterRejectionResult = new MalRefreshResult.Refreshed(TokenRecord("token-2")),
        };

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider));
        var response = await invoker.SendAsync(BearerRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(2, inner.Requests.Count);
        Assert.Equal(1, tokenProvider.RefreshAfterRejectionCallCount);
    }

    [Fact]
    public async Task AClientIdRequests401IsLeftAlone()
    {
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);

        var tokenProvider = new FakeMalTokenProvider("token-1");

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider));
        var response = await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Single(inner.Requests);
        Assert.Equal(0, tokenProvider.RefreshAfterRejectionCallCount);
        Assert.Equal(0, tokenProvider.GetValidAccessTokenCallCount);
    }

    // add-first-run-setup design D12: the handler is where MyAnimeList's health
    // is measured. A millisecond backoff keeps the 403 retries quick.
    private static readonly TimeSpan QuickBackoff = TimeSpan.FromMilliseconds(1);

    [Fact]
    public async Task A403BackoffIsReportedAsAThrottleWhileItRunsAndClearedByTheNextAnswer()
    {
        // The clock is frozen at the start, so the throttle reads as still in
        // force when the retry is made, rather than already run out.
        var start = DateTimeOffset.UtcNow;
        var health = new MalServiceHealth(() => start);
        DateTimeOffset? seenOnRetry = null;

        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Forbidden);
        inner.Enqueue(_ =>
        {
            seenOnRetry = health.ThrottledUntil;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health, QuickBackoff));
        var response = await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(seenOnRetry);
        Assert.True(seenOnRetry >= start + QuickBackoff);
        Assert.Null(health.ThrottledUntil);
        Assert.Equal(0, health.ConsecutiveFailures);
    }

    [Fact]
    public async Task EachBackoffDoublesTheReportedWait()
    {
        var start = DateTimeOffset.UtcNow;
        var health = new MalServiceHealth(() => start);
        var backoff = TimeSpan.FromMilliseconds(4);
        var seen = new List<DateTimeOffset?>();

        var inner = new FakeInnerHandler();
        for (var i = 0; i < 3; i++)
        {
            inner.Enqueue(_ =>
            {
                seen.Add(health.ThrottledUntil);
                return new HttpResponseMessage(HttpStatusCode.Forbidden);
            });
        }
        inner.Enqueue(_ =>
        {
            seen.Add(health.ThrottledUntil);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health, backoff));
        await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        // Nothing is reported on the first try; each retry sees the backoff that preceded it.
        Assert.Null(seen[0]);
        Assert.True(seen[1] >= start + backoff);
        Assert.True(seen[2] >= start + backoff * 2);
        Assert.True(seen[3] >= start + backoff * 4);
    }

    [Fact]
    public async Task A403ThatOutlastsEveryRetryIsOneTemporaryFailure()
    {
        var health = new MalServiceHealth();
        var inner = new FakeInnerHandler();
        for (var i = 0; i < 5; i++)
            inner.Enqueue(HttpStatusCode.Forbidden);

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health, QuickBackoff));
        var response = await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(5, inner.Requests.Count);
        Assert.Equal(1, health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ARequestThatIsAnsweredAfterA403IsNoFailureAtAll()
    {
        var health = new MalServiceHealth();
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Forbidden);
        inner.Enqueue(HttpStatusCode.Forbidden);
        inner.Enqueue(HttpStatusCode.OK);

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health, QuickBackoff));
        await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.Equal(0, health.ConsecutiveFailures);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task AnAnswerThatIsNeitherAServerErrorNorAThrottleEndsTheFailureStreak(HttpStatusCode status)
    {
        var health = new MalServiceHealth();
        health.RecordTemporaryFailure();
        health.RecordTemporaryFailure();
        var inner = new FakeInnerHandler();
        inner.Enqueue(status);

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health));
        var response = await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(0, health.ConsecutiveFailures);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task AServerErrorIsATemporaryFailureAndThreeInARowTakeMalDown(HttpStatusCode status)
    {
        var health = new MalServiceHealth();
        var inner = new FakeInnerHandler();
        for (var i = 0; i < 3; i++)
            inner.Enqueue(status);

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health));
        for (var i = 0; i < 2; i++)
        {
            var response = await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);
            Assert.Equal(status, response.StatusCode); // the caller still sees the response
        }
        Assert.False(health.IsDown);

        await invoker.SendAsync(ClientIdRequest(), CancellationToken.None);

        Assert.True(health.IsDown);
    }

    [Fact]
    public async Task ANetworkErrorIsATemporaryFailureAndStillReachesTheCaller()
    {
        var health = new MalServiceHealth();
        var inner = new FakeInnerHandler();
        inner.Enqueue(_ => throw new HttpRequestException("connection refused"));

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider("t"), health));

        await Assert.ThrowsAsync<HttpRequestException>(() => invoker.SendAsync(ClientIdRequest(), CancellationToken.None));
        Assert.Equal(1, health.ConsecutiveFailures);
    }

    [Fact]
    public async Task AnHttpClientTimeoutIsATemporaryFailure()
    {
        var health = new MalServiceHealth();
        using var client = new HttpClient(CreateHandler(new HangingHandler(), new FakeMalTokenProvider("t"), health))
        {
            Timeout = TimeSpan.FromMilliseconds(50),
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.SendAsync(ClientIdRequest()));

        Assert.Equal(1, health.ConsecutiveFailures);
    }

    [Fact]
    public async Task ALoginTheServiceRefusesIsNotAFailureOfTheService()
    {
        var health = new MalServiceHealth();
        health.RecordTemporaryFailure();
        health.RecordTemporaryFailure();
        var inner = new FakeInnerHandler();
        inner.Enqueue(HttpStatusCode.Unauthorized);
        var tokenProvider = new FakeMalTokenProvider("token-1")
        {
            RefreshAfterRejectionResult = new MalRefreshResult.Refused(401, "invalid_grant"),
        };

        using var invoker = new HttpMessageInvoker(CreateHandler(inner, tokenProvider, health));

        await Assert.ThrowsAsync<MalAuthorizationRequiredException>(() => invoker.SendAsync(BearerRequest(), CancellationToken.None));
        // Neither a strike nor an answer: the streak stands as it was.
        Assert.Equal(2, health.ConsecutiveFailures);
    }

    [Fact]
    public async Task NoStoredLoginIsNotAFailureOfTheService()
    {
        var health = new MalServiceHealth();
        health.RecordTemporaryFailure();
        var inner = new FakeInnerHandler();
        using var invoker = new HttpMessageInvoker(CreateHandler(inner, new FakeMalTokenProvider(null), health));

        await Assert.ThrowsAsync<MalAuthorizationRequiredException>(() => invoker.SendAsync(BearerRequest(), CancellationToken.None));

        Assert.Empty(inner.Requests);
        Assert.Equal(1, health.ConsecutiveFailures);
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable.");
        }
    }

    private sealed class FakeInnerHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();
        public List<HttpRequestMessage> Requests { get; } = [];

        public void Enqueue(HttpStatusCode statusCode) => Enqueue(_ => new HttpResponseMessage(statusCode));
        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> respond) => _responses.Enqueue(respond);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (_responses.Count == 0)
                throw new InvalidOperationException("No more stubbed responses.");
            return Task.FromResult(_responses.Dequeue()(request));
        }
    }

    private sealed class FakeMalTokenProvider(string? accessToken) : IMalTokenProvider
    {
        public string? AccessToken { get; set; } = accessToken;
        public MalRefreshResult RefreshAfterRejectionResult { get; set; } = new MalRefreshResult.Unavailable("not configured");
        public int GetValidAccessTokenCallCount { get; private set; }
        public int RefreshAfterRejectionCallCount { get; private set; }
        public string? RejectedTokenReceived { get; private set; }

        public Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
        {
            GetValidAccessTokenCallCount++;
            return Task.FromResult(AccessToken);
        }

        public Task<MalRefreshResult> RefreshAfterRejectionAsync(string rejectedAccessToken, CancellationToken ct = default)
        {
            RefreshAfterRejectionCallCount++;
            RejectedTokenReceived = rejectedAccessToken;
            if (RefreshAfterRejectionResult is MalRefreshResult.Refreshed refreshed)
                AccessToken = refreshed.Token.AccessToken;
            return Task.FromResult(RefreshAfterRejectionResult);
        }

        public Task<MalRefreshResult?> RefreshIfExpiringWithinAsync(TimeSpan window, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }
}
