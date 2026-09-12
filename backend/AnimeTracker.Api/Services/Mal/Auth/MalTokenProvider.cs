namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Registered as a singleton (it's injected into the pooled/pacing
/// HTTP handler), so it creates a fresh DI scope per call rather than holding
/// scoped dependencies (DbContext) directly.</summary>
public class MalTokenProvider(IServiceScopeFactory scopeFactory) : IMalTokenProvider
{
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromMinutes(10);

    // MAL rotates refresh tokens on use, so two concurrent refreshes would have
    // the second exchange consume an already-spent refresh token and fail. This
    // serializes the refresh path; the re-check after acquiring the lock lets
    // every caller that queued behind an in-flight refresh reuse its result
    // instead of refreshing again themselves.
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public async Task<string?> GetValidAccessTokenAsync(CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        var oauth = scope.ServiceProvider.GetRequiredService<IMalOAuthService>();

        var token = await tokenStore.GetAsync(ct);
        if (token is null || token.ConnectionLostAt is not null)
            return null;

        if (token.ExpiresAt - RefreshBuffer > DateTimeOffset.UtcNow)
            return token.AccessToken;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // Re-read: another caller may have already refreshed, or the
            // connection may have just been marked lost, while we waited.
            token = await tokenStore.GetAsync(ct);
            if (token is null || token.ConnectionLostAt is not null)
                return null;

            if (token.ExpiresAt - RefreshBuffer > DateTimeOffset.UtcNow)
                return token.AccessToken;

            var result = await oauth.RefreshAsync(token.RefreshToken, ct);
            return result is MalRefreshResult.Refreshed refreshed ? refreshed.Token.AccessToken : null;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<MalRefreshResult> RefreshAfterRejectionAsync(string rejectedAccessToken, CancellationToken ct = default)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        var oauth = scope.ServiceProvider.GetRequiredService<IMalOAuthService>();

        await _refreshLock.WaitAsync(ct);
        try
        {
            var token = await tokenStore.GetAsync(ct);
            if (token is null || token.ConnectionLostAt is not null)
                return new MalRefreshResult.Refused(0, null);

            // A concurrent caller already refreshed while we waited for the lock.
            if (token.AccessToken != rejectedAccessToken)
                return new MalRefreshResult.Refreshed(token);

            return await oauth.RefreshAsync(token.RefreshToken, ct);
        }
        finally
        {
            _refreshLock.Release();
        }
    }
}
