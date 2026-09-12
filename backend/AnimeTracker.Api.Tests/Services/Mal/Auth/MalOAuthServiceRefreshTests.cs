using System.Net;
using System.Text;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Mal.Auth;

// MalOAuthService.RefreshAsync tells MyAnimeList refusing this login (400 or
// 401 from the token endpoint) apart from an outage, and records only a
// refusal as a lost connection (design.md D11).
public class MalOAuthServiceRefreshTests
{
    private static MalOAuthService CreateService(HttpMessageHandler handler, FakeMalTokenStore store) =>
        new(new FakeHttpClientFactory(handler), store, new MalOAuthStateStore(),
            Options.Create(new MalOptions { ClientId = "client-id", ClientSecret = "secret" }),
            NullLogger<MalOAuthService>.Instance);

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.Unauthorized)]
    public async Task A400Or401IsRefusedAndMarksTheConnectionLost(HttpStatusCode statusCode)
    {
        var store = new FakeMalTokenStore();
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode)
        {
            Content = new StringContent("{\"error\":\"invalid_grant\"}", Encoding.UTF8, "application/json"),
        });

        var result = await CreateService(handler, store).RefreshAsync("refresh-token");

        var refused = Assert.IsType<MalRefreshResult.Refused>(result);
        Assert.Equal((int)statusCode, refused.StatusCode);
        Assert.Equal("invalid_grant", refused.Error);
        Assert.True(store.ConnectionMarkedLost);
        Assert.Null(store.Saved);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task OtherFailingStatusesAreUnavailableAndMarkNothing(HttpStatusCode statusCode)
    {
        var store = new FakeMalTokenStore();
        var handler = new StubHandler(_ => new HttpResponseMessage(statusCode));

        var result = await CreateService(handler, store).RefreshAsync("refresh-token");

        Assert.IsType<MalRefreshResult.Unavailable>(result);
        Assert.False(store.ConnectionMarkedLost);
        Assert.Null(store.Saved);
    }

    [Fact]
    public async Task AThrownHttpRequestExceptionIsUnavailable()
    {
        var store = new FakeMalTokenStore();
        var handler = new ThrowingHandler(() => new HttpRequestException("network down"));

        var result = await CreateService(handler, store).RefreshAsync("refresh-token");

        Assert.IsType<MalRefreshResult.Unavailable>(result);
        Assert.False(store.ConnectionMarkedLost);
    }

    [Fact]
    public async Task ATimeoutIsUnavailable()
    {
        var store = new FakeMalTokenStore();
        var handler = new ThrowingHandler(() => new TaskCanceledException("timed out"));

        // The caller's own token is never cancelled — a TaskCanceledException
        // reaching RefreshAsync despite that is HttpClient's own timeout.
        var result = await CreateService(handler, store).RefreshAsync("refresh-token", CancellationToken.None);

        Assert.IsType<MalRefreshResult.Unavailable>(result);
        Assert.False(store.ConnectionMarkedLost);
    }

    [Fact]
    public async Task A200IsRefreshedAndSaved()
    {
        var store = new FakeMalTokenStore();
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                "{\"token_type\":\"Bearer\",\"expires_in\":3600,\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\"}",
                Encoding.UTF8, "application/json"),
        });

        var result = await CreateService(handler, store).RefreshAsync("old-refresh");

        var refreshed = Assert.IsType<MalRefreshResult.Refreshed>(result);
        Assert.Equal("new-access", refreshed.Token.AccessToken);
        Assert.NotNull(store.Saved);
        Assert.Equal("new-access", store.Saved!.Value.AccessToken);
        Assert.Equal("new-refresh", store.Saved!.Value.RefreshToken);
        Assert.False(store.ConnectionMarkedLost);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class ThrowingHandler(Func<Exception> makeException) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw makeException();
    }

    private sealed class FakeMalTokenStore : IMalTokenStore
    {
        public bool ConnectionMarkedLost { get; private set; }
        public (string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)? Saved { get; private set; }

        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult<OAuthToken?>(null);

        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default)
        {
            Saved = (accessToken, refreshToken, expiresAt);
            return Task.CompletedTask;
        }

        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default)
        {
            ConnectionMarkedLost = true;
            return Task.CompletedTask;
        }
    }
}
