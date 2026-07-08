namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Spreads tiered score refresh across small batches on a steady
/// interval instead of one nightly burst (design.md risk: "Refresh job
/// hammering the API on a large library"). Each pass is cheap when nothing is
/// due — RefreshStaleBatchAsync just returns 0. A fixed nightly cap limits the
/// total calls made per calendar day (UTC); once reached, no more refreshes
/// happen until the next day, and any remaining stale candidates simply carry
/// over automatically since they stay the most-stale next time.</summary>
public class MetadataRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MetadataRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    private const int BatchSize = 20;
    private const int NightlyCap = 500;

    private DateOnly _capWindowDate = DateOnly.MinValue;
    private int _callsThisWindow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                if (today != _capWindowDate)
                {
                    _capWindowDate = today;
                    _callsThisWindow = 0;
                }

                var remainingQuota = NightlyCap - _callsThisWindow;
                if (remainingQuota > 0)
                {
                    using var scope = scopeFactory.CreateScope();
                    var refresh = scope.ServiceProvider.GetRequiredService<IMetadataRefreshService>();
                    var refreshed = await refresh.RefreshStaleBatchAsync(Math.Min(BatchSize, remainingQuota), stoppingToken);
                    _callsThisWindow += refreshed;
                    if (refreshed > 0)
                        logger.LogInformation(
                            "Metadata refresh pass updated {Count} anime score(s) ({Used}/{Cap} today).",
                            refreshed, _callsThisWindow, NightlyCap);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Metadata refresh pass failed.");
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
