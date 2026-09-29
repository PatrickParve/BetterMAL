using System.Net;
using System.Text;
using System.Web;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Services.Mal.Auth;

// MalOAuthService.HandleCallbackAsync tells an unknown, expired or used state
// (an expected outcome: a restart during sign-in, the ten-minute limit) apart
// from a completed exchange, and still throws for a failed exchange (mal-api-
// integration, "One-time interactive PKCE authorization"; design D6).
public class MalOAuthServiceCallbackTests
{
    private sealed record Setup(MalOAuthService Service, MalOAuthStateStore StateStore, FakeStore Store, StubHandler Handler);

    private static Setup CreateService(Func<HttpRequestMessage, HttpResponseMessage>? respond = null)
    {
        var handler = new StubHandler(respond ?? (_ => TokenResponse()));
        var store = new FakeStore();
        var stateStore = new MalOAuthStateStore();
        var service = new MalOAuthService(
            new FakeHttpClientFactory(handler), store, stateStore,
            Options.Create(new MalOptions { ClientId = "client-id", ClientSecret = "secret", CallbackPort = 5050 }),
            NullLogger<MalOAuthService>.Instance);
        return new Setup(service, stateStore, store, handler);
    }

    private static HttpResponseMessage TokenResponse() => new(HttpStatusCode.OK)
    {
        Content = new StringContent(
            "{\"token_type\":\"Bearer\",\"expires_in\":2678400,\"access_token\":\"new-access\",\"refresh_token\":\"new-refresh\"}",
            Encoding.UTF8, "application/json"),
    };

    private static string PendingStateOf(MalOAuthService service) =>
        HttpUtility.ParseQueryString(new Uri(service.BuildAuthorizeUrl()).Query)["state"]!;

    [Fact]
    public async Task AnUnknownStateIsRejectedWithNoExchangeAndNothingSaved()
    {
        var setup = CreateService();
        _ = setup.Service.BuildAuthorizeUrl(); // a different attempt is pending

        var result = await setup.Service.HandleCallbackAsync("code", "not-the-pending-state");

        Assert.IsType<MalCallbackResult.StateRejected>(result);
        Assert.Equal(0, setup.Handler.Requests);
        Assert.Null(setup.Store.Saved);
    }

    [Fact]
    public async Task NoPendingAttemptAtAllIsRejected()
    {
        // The application restarted between start and the callback: the
        // pending verifier lived in memory and is gone.
        var setup = CreateService();

        var result = await setup.Service.HandleCallbackAsync("code", "state-from-before-the-restart");

        Assert.IsType<MalCallbackResult.StateRejected>(result);
        Assert.Equal(0, setup.Handler.Requests);
    }

    [Fact]
    public async Task AMatchingStateCompletesTheExchangeAndSavesTheTokens()
    {
        var setup = CreateService();
        var state = PendingStateOf(setup.Service);

        var result = await setup.Service.HandleCallbackAsync("the-code", state);

        var completed = Assert.IsType<MalCallbackResult.Completed>(result);
        Assert.Equal("new-access", completed.Token.AccessToken);
        Assert.Equal("new-refresh", completed.Token.RefreshToken);
        Assert.Equal(("new-access", "new-refresh"), (setup.Store.Saved!.Value.AccessToken, setup.Store.Saved.Value.RefreshToken));
        Assert.Equal(1, setup.Handler.Requests);
    }

    [Fact]
    public async Task TheExchangeSendsTheClientIdAndSecretAndTheCode()
    {
        var setup = CreateService();
        var state = PendingStateOf(setup.Service);

        await setup.Service.HandleCallbackAsync("the-code", state);

        var form = HttpUtility.ParseQueryString(setup.Handler.LastBody!);
        Assert.Equal("client-id", form["client_id"]);
        Assert.Equal("secret", form["client_secret"]);
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("the-code", form["code"]);
        Assert.Equal("http://localhost:5050/callback", form["redirect_uri"]);
    }

    [Fact]
    public async Task AStateCanOnlyBeUsedOnce()
    {
        var setup = CreateService();
        var state = PendingStateOf(setup.Service);
        await setup.Service.HandleCallbackAsync("the-code", state);

        var replay = await setup.Service.HandleCallbackAsync("the-code", state);

        Assert.IsType<MalCallbackResult.StateRejected>(replay);
        Assert.Equal(1, setup.Handler.Requests);
    }

    [Fact]
    public async Task AFailedExchangeStillThrowsAndSavesNothing()
    {
        var setup = CreateService(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        var state = PendingStateOf(setup.Service);

        await Assert.ThrowsAsync<HttpRequestException>(() => setup.Service.HandleCallbackAsync("the-code", state));

        Assert.Null(setup.Store.Saved);
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Requests { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return respond(request);
        }
    }

    private sealed class FakeStore : IMalTokenStore
    {
        public (string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)? Saved { get; private set; }

        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult<OAuthToken?>(null);

        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default)
        {
            Saved = (accessToken, refreshToken, expiresAt);
            return Task.CompletedTask;
        }

        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) => Task.CompletedTask;
    }
}
