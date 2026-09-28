namespace AnimeTracker.Api.Services.Mal.Auth;

/// <summary>Periodically refreshes the MAL access token well ahead of its
/// ~31-day expiry. This is the normal refresh path; it shares the token
/// provider's lock with the request-path refresh, which is what stops it
/// racing a request that refreshes the same token, such as after the app
/// was off past the token's expiry.</summary>
public class MalTokenRefreshBackgroundService(
    IMalTokenProvider tokenProvider,
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
            // Filtered on the token, not the exception type: a cancellation
            // that isn't our own shutdown (an HttpClient timeout) is a failed
            // check, not something to let escape and fault the service.
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
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
        var result = await tokenProvider.RefreshIfExpiringWithinAsync(RefreshBuffer, ct);

        switch (result)
        {
            case null:
                // no login stored, the connection is lost, or still comfortably fresh
                return;

            case MalRefreshResult.Refreshed refreshed:
                logger.LogInformation(
                    "Refreshed the MAL access token ahead of expiry; it now expires at {ExpiresAt}.",
                    refreshed.Token.ExpiresAt);
                break;

            case MalRefreshResult.Refused refused:
                logger.LogWarning(
                    "MAL refused to refresh the login ({StatusCode}); re-authorization will be required.", refused.StatusCode);
                break;

            case MalRefreshResult.Unavailable unavailable:
                logger.LogWarning("MAL token refresh could not be completed: {Detail}", unavailable.Detail);
                break;
        }
    }
}
