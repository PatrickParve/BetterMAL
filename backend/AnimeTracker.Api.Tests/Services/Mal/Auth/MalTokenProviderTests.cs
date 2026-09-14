using AnimeTracker.Api.Models;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.DependencyInjection;

namespace AnimeTracker.Api.Tests.Services.Mal.Auth;

// MalTokenProvider (design.md D12/D13): a lost connection is a dead end with
// no refresh attempt, and RefreshAfterRejectionAsync serializes concurrent
// callers behind one refresh, letting a caller that only lost the race reuse
// whichever token is now stored instead of refreshing again itself.
// RefreshIfExpiringWithinAsync (design.md D4/D5) shares that same lock, so
// the background refresh it backs can't race a request-path refresh either.
public class MalTokenProviderTests
{
    private static OAuthToken Token(string accessToken, DateTimeOffset? connectionLostAt = null, DateTimeOffset? expiresAt = null) => new()
    {
        AccessToken = accessToken,
        RefreshToken = "refresh",
        ExpiresAt = expiresAt ?? DateTimeOffset.UtcNow.AddDays(1),
        UpdatedAt = DateTimeOffset.UtcNow,
        ConnectionLostAt = connectionLostAt,
    };

    [Fact]
    public async Task ALostRowReturnsNullAndNeverCallsRefreshAsync()
    {
        var store = new FakeMalTokenStore(Token("access", connectionLostAt: DateTimeOffset.UtcNow));
        var oauth = new FakeMalOAuthService();
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var accessToken = await provider.GetValidAccessTokenAsync();

        Assert.Null(accessToken);
        Assert.Equal(0, oauth.RefreshCallCount);
    }

    [Fact]
    public async Task RefreshAfterRejectionReturnsTheStoredTokenWithoutARefreshWhenItDiffersFromTheRejectedOne()
    {
        var store = new FakeMalTokenStore(Token("current"));
        var oauth = new FakeMalOAuthService();
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshAfterRejectionAsync("stale-token");

        var refreshed = Assert.IsType<MalRefreshResult.Refreshed>(result);
        Assert.Equal("current", refreshed.Token.AccessToken);
        Assert.Equal(0, oauth.RefreshCallCount);
    }

    [Fact]
    public async Task RefreshAfterRejectionRefreshesWhenTheAccessTokensMatch()
    {
        var store = new FakeMalTokenStore(Token("current"));
        var oauth = new FakeMalOAuthService
        {
            Result = new MalRefreshResult.Refreshed(Token("new")),
        };
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshAfterRejectionAsync("current");

        Assert.Equal(1, oauth.RefreshCallCount);
        var refreshed = Assert.IsType<MalRefreshResult.Refreshed>(result);
        Assert.Equal("new", refreshed.Token.AccessToken);
    }

    [Fact]
    public async Task TwoConcurrentCallsRefreshOnce()
    {
        var store = new FakeMalTokenStore(Token("current"));
        var gate = new TaskCompletionSource();
        var oauth = new FakeMalOAuthService
        {
            Gate = gate,
            Result = new MalRefreshResult.Refreshed(Token("refreshed")),
        };
        // A real refresh updates the stored row; simulate that so the second
        // (queued) caller sees the new token once it gets the lock.
        oauth.OnRefreshed = () => store.Current = Token("refreshed");
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var call1 = provider.RefreshAfterRejectionAsync("current");
        await Task.Delay(50); // let call1 acquire the lock and block on the gate
        var call2 = provider.RefreshAfterRejectionAsync("current");
        await Task.Delay(50); // let call2 queue up behind the lock
        gate.SetResult();

        var results = await Task.WhenAll(call1, call2);

        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.All(results, r => Assert.IsType<MalRefreshResult.Refreshed>(r));
    }

    [Fact]
    public async Task NoLoginStoredReturnsNullWithoutRefreshing()
    {
        var store = new FakeMalTokenStore(null);
        var oauth = new FakeMalOAuthService();
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));

        Assert.Null(result);
        Assert.Equal(0, oauth.RefreshCallCount);
    }

    [Fact]
    public async Task ALostConnectionReturnsNullWithoutRefreshingEvenWhenExpired()
    {
        var store = new FakeMalTokenStore(Token(
            "access", connectionLostAt: DateTimeOffset.UtcNow, expiresAt: DateTimeOffset.UtcNow.AddDays(-1)));
        var oauth = new FakeMalOAuthService();
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));

        Assert.Null(result);
        Assert.Equal(0, oauth.RefreshCallCount);
    }

    [Fact]
    public async Task AFreshTokenOutsideTheWindowReturnsNullWithoutRefreshing()
    {
        var store = new FakeMalTokenStore(Token("access", expiresAt: DateTimeOffset.UtcNow.AddDays(30)));
        var oauth = new FakeMalOAuthService();
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));

        Assert.Null(result);
        Assert.Equal(0, oauth.RefreshCallCount);
    }

    public static IEnumerable<object[]> RefreshOutcomes()
    {
        yield return new object[] { new MalRefreshResult.Refreshed(Token("new")) };
        yield return new object[] { new MalRefreshResult.Refused(400, "invalid_grant") };
        yield return new object[] { new MalRefreshResult.Unavailable("MyAnimeList couldn't be reached.") };
    }

    [Theory]
    [MemberData(nameof(RefreshOutcomes))]
    public async Task InsideTheWindowRefreshesOnceAndReturnsWhateverRefreshAsyncReturned(MalRefreshResult outcome)
    {
        var store = new FakeMalTokenStore(Token("access", expiresAt: DateTimeOffset.UtcNow.AddHours(12)));
        var oauth = new FakeMalOAuthService { Result = outcome };
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var result = await provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));

        Assert.Same(outcome, result);
        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.Equal("refresh", oauth.LastRefreshToken);
    }

    [Fact]
    public async Task BackgroundRefreshFirstThenARequestOnAnExpiredTokenSharesOneRefresh()
    {
        var store = new FakeMalTokenStore(Token("current", expiresAt: DateTimeOffset.UtcNow.AddDays(-1)));
        var gate = new TaskCompletionSource();
        var oauth = new FakeMalOAuthService
        {
            Gate = gate,
            Result = new MalRefreshResult.Refreshed(Token("refreshed", expiresAt: DateTimeOffset.UtcNow.AddDays(31))),
        };
        // Outside both the 10-minute (GetValidAccessTokenAsync) and the 1-day
        // (RefreshIfExpiringWithinAsync) window, so the waiting caller reuses
        // this token instead of refreshing again itself.
        oauth.OnRefreshed = () => store.Current = Token("refreshed", expiresAt: DateTimeOffset.UtcNow.AddDays(31));
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var backgroundCall = provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));
        await Task.Delay(50); // let the background call acquire the lock and block on the gate
        var requestCall = provider.GetValidAccessTokenAsync();
        await Task.Delay(50); // let the request call queue up behind the lock
        gate.SetResult();

        var backgroundResult = await backgroundCall;
        var accessToken = await requestCall;

        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.IsType<MalRefreshResult.Refreshed>(backgroundResult);
        Assert.Equal("refreshed", accessToken);
        // FakeMalTokenStore.MarkConnectionLostAsync throws NotImplementedException,
        // so if the waiting request path ever marked the connection lost instead
        // of reusing the refreshed token, this test would fail with that instead.
    }

    [Fact]
    public async Task ARequestFirstThenTheBackgroundCheckOnAnExpiredTokenSharesOneRefresh()
    {
        var store = new FakeMalTokenStore(Token("current", expiresAt: DateTimeOffset.UtcNow.AddDays(-1)));
        var gate = new TaskCompletionSource();
        var oauth = new FakeMalOAuthService
        {
            Gate = gate,
            Result = new MalRefreshResult.Refreshed(Token("refreshed", expiresAt: DateTimeOffset.UtcNow.AddDays(31))),
        };
        oauth.OnRefreshed = () => store.Current = Token("refreshed", expiresAt: DateTimeOffset.UtcNow.AddDays(31));
        var provider = new MalTokenProvider(new FakeScopeFactory(store, oauth));

        var requestCall = provider.GetValidAccessTokenAsync();
        await Task.Delay(50); // let the request call acquire the lock and block on the gate
        var backgroundCall = provider.RefreshIfExpiringWithinAsync(TimeSpan.FromDays(1));
        await Task.Delay(50); // let the background call queue up behind the lock
        gate.SetResult();

        var accessToken = await requestCall;
        var backgroundResult = await backgroundCall;

        Assert.Equal(1, oauth.RefreshCallCount);
        Assert.Equal("refreshed", accessToken);
        Assert.Null(backgroundResult);
    }

    private sealed class FakeMalTokenStore(OAuthToken? initial) : IMalTokenStore
    {
        public OAuthToken? Current { get; set; } = initial;

        public Task<OAuthToken?> GetAsync(CancellationToken ct = default) => Task.FromResult(Current);
        public Task SaveAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken ct = default) =>
            throw new NotImplementedException();
        public Task MarkConnectionLostAsync(DateTimeOffset at, CancellationToken ct = default) =>
            throw new NotImplementedException();
    }

    private sealed class FakeMalOAuthService : IMalOAuthService
    {
        public MalRefreshResult Result { get; set; } = new MalRefreshResult.Unavailable("not configured");
        public TaskCompletionSource? Gate { get; set; }
        public Action? OnRefreshed { get; set; }
        public int RefreshCallCount { get; private set; }
        public string? LastRefreshToken { get; private set; }

        public string BuildAuthorizeUrl() => throw new NotImplementedException();
        public Task<OAuthToken> HandleCallbackAsync(string code, string state, CancellationToken ct = default) =>
            throw new NotImplementedException();

        public async Task<MalRefreshResult> RefreshAsync(string refreshToken, CancellationToken ct = default)
        {
            RefreshCallCount++;
            LastRefreshToken = refreshToken;
            if (Gate is not null)
                await Gate.Task;
            OnRefreshed?.Invoke();
            return Result;
        }
    }

    private sealed class FakeScopeFactory(IMalTokenStore store, IMalOAuthService oauth) : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeScope(store, oauth);
    }

    private sealed class FakeScope(IMalTokenStore store, IMalOAuthService oauth) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider(store, oauth);
        public void Dispose() { }
    }

    private sealed class FakeServiceProvider(IMalTokenStore store, IMalOAuthService oauth) : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            if (serviceType == typeof(IMalTokenStore)) return store;
            if (serviceType == typeof(IMalOAuthService)) return oauth;
            return null;
        }
    }
}
