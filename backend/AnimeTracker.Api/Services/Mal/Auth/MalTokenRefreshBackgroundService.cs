namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Periodically refreshes the MAL access token well ahead of its
/// ~31-day expiry, so token refresh is a background concern rather than
/// something a live request ever has to wait on.</summary>
public class MalTokenRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MalTokenRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private static readonly TimeSpan RefreshBuffer = TimeSpan.FromDays(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RefreshIfNeededAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "MAL background token refresh check failed.");
            }

            try
            {
                await Task.Delay(CheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task RefreshIfNeededAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        var oauth = scope.ServiceProvider.GetRequiredService<IMalOAuthService>();

        var token = await tokenStore.GetAsync(ct);
        if (token is null)
            return; // not authorized yet — nothing to refresh

        if (token.ExpiresAt - RefreshBuffer > DateTimeOffset.UtcNow)
            return; // still comfortably fresh

        logger.LogInformation("MAL access token expires at {ExpiresAt}; refreshing ahead of expiry.", token.ExpiresAt);
        var refreshed = await oauth.RefreshAsync(token.RefreshToken, ct);

        if (refreshed is null)
            logger.LogWarning("MAL token refresh failed; re-authorization will be required.");
    }
}
