using AnimeTracker.Api.Controllers;
using AnimeTracker.Api.Data;
using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Import;
using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Setup;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Tests.Controllers;

// mal-api-integration, "One-time interactive PKCE authorization" (the callback
// renders no page: every outcome redirects back into the app, carrying only a
// short failure code) and first-run-setup, "Setup checks both MyAnimeList
// credentials first" / "The connect screen starts setup" (design D5, D6).
public class MalAuthControllerTests
{
    private sealed class Harness : IDisposable
    {
        private readonly ServiceProvider _provider;

        public FakeOAuthService OAuth { get; } = new();
        public FakeTokenStore Tokens { get; } = new();
        public CountingImportTrigger ImportTrigger { get; } = new();
        public CountingSetupTrigger SetupTrigger { get; } = new();
        public SetupGate Gate { get; }
        public MalAuthController Controller { get; }

        public Harness(MalOptions? options = null, bool setupFinished = false)
        {
            var services = new ServiceCollection();
            var dbName = Guid.NewGuid().ToString();
            services.AddDbContext<AnimeTrackerDbContext>(o => o.UseInMemoryDatabase(dbName));
            _provider = services.BuildServiceProvider();
            Gate = new SetupGate(_provider.GetRequiredService<IServiceScopeFactory>());
            if (setupFinished)
                Gate.MarkFinishedAsync().GetAwaiter().GetResult();

            Controller = new MalAuthController(
                OAuth, Tokens, ImportTrigger, SetupTrigger, Gate,
                Options.Create(options ?? new MalOptions { ClientId = "id", ClientSecret = "secret" }),
                NullLogger<MalAuthController>.Instance);
        }

        public void Dispose() => _provider.Dispose();
    }

    private static string LocationOf(IActionResult result) => Assert.IsType<RedirectResult>(result).Url;

    private static OAuthToken StoredToken(DateTimeOffset? lostAt = null) => new()
    {
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = DateTimeOffset.UtcNow.AddDays(30),
        ConnectionLostAt = lostAt,
    };

    // Start

    [Fact]
    public void StartRedirectsToMalWhenBothCredentialsAreSet()
    {
        using var h = new Harness();

        var result = h.Controller.Start();

        Assert.Equal("https://myanimelist.net/authorize-stub", LocationOf(result));
        Assert.Equal(1, h.OAuth.BuildCalls);
    }

    [Theory]
    [InlineData("id", null, new[] { "MAL_CLIENT_SECRET" })]
    [InlineData("id", "", new[] { "MAL_CLIENT_SECRET" })]
    [InlineData("", "secret", new[] { "MAL_CLIENT_ID" })]
    [InlineData("", "", new[] { "MAL_CLIENT_ID", "MAL_CLIENT_SECRET" })]
    [InlineData("  ", "  ", new[] { "MAL_CLIENT_ID", "MAL_CLIENT_SECRET" })]
    public void StartIsRefusedWithTheMissingNamesAndBuildsNoRedirect(string clientId, string? secret, string[] expectedMissing)
    {
        using var h = new Harness(new MalOptions { ClientId = clientId, ClientSecret = secret });

        var result = h.Controller.Start();

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        var missing = (IReadOnlyList<string>)conflict.Value!.GetType().GetProperty("missingCredentials")!.GetValue(conflict.Value)!;
        Assert.Equal(expectedMissing, missing);
        // No authorize URL, so no pending attempt is recorded either.
        Assert.Equal(0, h.OAuth.BuildCalls);
    }

    [Fact]
    public void AMissingCredentialRefusalNeverCarriesAValue()
    {
        using var h = new Harness(new MalOptions { ClientId = "the-client-id", ClientSecret = null });

        var conflict = Assert.IsType<ConflictObjectResult>(h.Controller.Start());

        Assert.DoesNotContain("the-client-id", System.Text.Json.JsonSerializer.Serialize(conflict.Value));
    }

    // Callback: where each outcome goes

    [Fact]
    public async Task ASuccessBeforeSetupFinishesReturnsToTheRootAndSignalsSetup()
    {
        using var h = new Harness();
        h.OAuth.Result = new MalCallbackResult.Completed(StoredToken());

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        Assert.Equal("http://localhost:5173/", LocationOf(result));
        Assert.Equal(1, h.SetupTrigger.Signals);
        Assert.Equal(0, h.ImportTrigger.Signals);
    }

    [Fact]
    public async Task ASuccessAfterSetupFinishesReturnsToSettingsAndSignalsTheImport()
    {
        using var h = new Harness(setupFinished: true);
        h.OAuth.Result = new MalCallbackResult.Completed(StoredToken());

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        Assert.Equal("http://localhost:5173/settings", LocationOf(result));
        Assert.Equal(1, h.ImportTrigger.Signals);
        Assert.Equal(0, h.SetupTrigger.Signals);
    }

    [Fact]
    public async Task TheRedirectFollowsTheConfiguredFrontendPort()
    {
        using var h = new Harness(new MalOptions { ClientId = "id", ClientSecret = "secret", FrontendPort = 8081 });
        h.OAuth.Result = new MalCallbackResult.Completed(StoredToken());

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        Assert.Equal("http://localhost:8081/", LocationOf(result));
    }

    [Theory]
    [InlineData(false, "http://localhost:5173/?connectError=denied")]
    [InlineData(true, "http://localhost:5173/settings?connectError=denied")]
    public async Task AnErrorFromMalReturnsWithTheDeniedCodeAndNoExchange(bool setupFinished, string expected)
    {
        using var h = new Harness(setupFinished: setupFinished);

        var result = await h.Controller.Callback(null, null, "access_denied", CancellationToken.None);

        Assert.Equal(expected, LocationOf(result));
        Assert.Equal(0, h.OAuth.CallbackCalls);
        Assert.Equal(0, h.SetupTrigger.Signals);
        Assert.Equal(0, h.ImportTrigger.Signals);
    }

    [Theory]
    [InlineData(false, "http://localhost:5173/?connectError=expired")]
    [InlineData(true, "http://localhost:5173/settings?connectError=expired")]
    public async Task AnUnknownOrExpiredStateReturnsWithTheExpiredCodeAndSignalsNothing(bool setupFinished, string expected)
    {
        using var h = new Harness(setupFinished: setupFinished);
        h.OAuth.Result = new MalCallbackResult.StateRejected();

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        Assert.Equal(expected, LocationOf(result));
        Assert.Equal(0, h.SetupTrigger.Signals);
        Assert.Equal(0, h.ImportTrigger.Signals);
    }

    [Theory]
    [InlineData(null, "state")]
    [InlineData("code", null)]
    [InlineData("", "state")]
    [InlineData("code", "")]
    [InlineData(null, null)]
    public async Task AMissingCodeOrStateReturnsWithTheFailedCodeAndNoExchange(string? code, string? state)
    {
        using var h = new Harness();

        var result = await h.Controller.Callback(code, state, null, CancellationToken.None);

        Assert.Equal("http://localhost:5173/?connectError=failed", LocationOf(result));
        Assert.Equal(0, h.OAuth.CallbackCalls);
        Assert.Equal(0, h.SetupTrigger.Signals);
    }

    [Fact]
    public async Task AFailedExchangeReturnsWithTheFailedCodeAndSignalsNothing()
    {
        using var h = new Harness();
        h.OAuth.Throw = new HttpRequestException("Response status code does not indicate success: 400");

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        Assert.Equal("http://localhost:5173/?connectError=failed", LocationOf(result));
        Assert.Equal(0, h.SetupTrigger.Signals);
        Assert.Equal(0, h.ImportTrigger.Signals);
    }

    // Callback: nothing MAL or the exception said reaches the URL

    [Fact]
    public async Task MalsErrorTextNeverReachesTheLocation()
    {
        using var h = new Harness();

        var result = await h.Controller.Callback(null, null, "<script>alert(1)</script>&x=y", CancellationToken.None);

        var location = LocationOf(result);
        Assert.Equal("http://localhost:5173/?connectError=denied", location);
        Assert.DoesNotContain("script", location);
    }

    [Fact]
    public async Task AnExceptionMessageNeverReachesTheLocation()
    {
        using var h = new Harness();
        h.OAuth.Throw = new InvalidOperationException("secret-token-abc leaked in a message");

        var result = await h.Controller.Callback("code", "state", null, CancellationToken.None);

        var location = LocationOf(result);
        Assert.DoesNotContain("secret-token-abc", location);
        Assert.DoesNotContain("leaked", location);
    }

    // Status (unchanged)

    [Theory]
    [InlineData(null, "NotConnected")]
    [InlineData("connected", "Connected")]
    [InlineData("lost", "Lost")]
    public async Task StatusReportsTheThreeConnectionStates(string? stored, string expected)
    {
        using var h = new Harness();
        h.Tokens.Token = stored switch
        {
            null => null,
            "connected" => StoredToken(),
            _ => StoredToken(lostAt: DateTimeOffset.UtcNow),
        };

        var ok = Assert.IsType<OkObjectResult>(await h.Controller.Status(CancellationToken.None));

        Assert.Equal(expected, ok.Value!.GetType().GetProperty("state")!.GetValue(ok.Value));
    }

    private sealed class FakeOAuthService : IMalOAuthService
    {
        public int BuildCalls { get; private set; }
        public int CallbackCalls { get; private set; }
        public MalCallbackResult Result { get; set; } = new MalCallbackResult.Completed(StoredToken());
        public Exception? Throw { get; set; }

        public string BuildAuthorizeUrl()
        {
            BuildCalls++;
            return "https://myanimelist.net/authorize-stub";
        }

        public Task<MalCallbackResult> HandleCallbackAsync(string code, string state, CancellationToken ct = default)
        {
            CallbackCalls++;
            return Throw is not null ? Task.FromException<MalCallbackResult>(Throw) : Task.FromResult(Result);
        }

        public Task<MalRefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeTokenStore : IMalTokenStore
    {
        public OAuthToken? Token { get; set; }

        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(Token);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            Task.CompletedTask;
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class CountingImportTrigger : IImportTrigger
    {
        public int Signals { get; private set; }
        public void Signal() => Signals++;
        public Task WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }

    private sealed class CountingSetupTrigger : ISetupTrigger
    {
        public int Signals { get; private set; }
        public void Signal() => Signals++;
        public Task WaitAsync(CancellationToken ct) => throw new NotImplementedException();
    }
}
