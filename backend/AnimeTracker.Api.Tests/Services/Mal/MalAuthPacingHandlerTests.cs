using System.Net;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Mal;

// MalAuthPacingHandler's one-shot 401 handling on a signed-in call
// (design.md D12), kept separate from the existing 403 backoff loop: a
// revoked login is noticed and retried exactly once, and an unauthenticated
// client-id request is never touched by any of this.
public class MalAuthPacingHandlerTests
{
    private static MalAuthPacingHandler CreateHandler(HttpMessageHandler inner, IMalTokenProvider tokenProvider) =>
        new(Options.Create(new MalOptions { ClientId = "client-id" }), tokenProvider, new MalRequestPacer(TimeSpan.Zero),
            NullLogger<MalAuthPacingHandler>.Instance)
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
