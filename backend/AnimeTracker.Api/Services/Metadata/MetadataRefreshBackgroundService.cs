using AnimeTracker.Api.Services.Updates;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Spreads tiered full-detail refresh across small batches on a steady
/// interval instead of one nightly burst (design.md risk: "Refresh job
/// hammering the API on a large library"). Each pass is cheap when nothing is
/// due — RefreshStaleBatchAsync just returns 0. A fixed nightly cap limits the
/// total calls made per calendar day (UTC); once reached, no more refreshes
/// happen until the next day, and any remaining stale candidates simply carry
/// over automatically since they stay the most-stale next time. Announcement
/// resolution shares this same tick, pacer and cap (design.md D11): it runs
/// first each pass so a large staleness backlog can never starve news of its
/// calls, and both sets of calls draw from the one daily budget.</summary>
public class MetadataRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MetadataRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    private const int BatchSize = 20;
    private const int AnnouncementBatchSize = 10;
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

                    var resolution = scope.ServiceProvider.GetRequiredService<IAnnouncementResolutionService>();
                    var resolved = await resolution.ResolveAsync(Math.Min(AnnouncementBatchSize, remainingQuota), stoppingToken);
                    _callsThisWindow += resolved;
                    if (resolved > 0)
                        logger.LogInformation(
                            "Announcement resolution made {Count} MAL calls ({Used}/{Cap} today).",
                            resolved, _callsThisWindow, NightlyCap);

                    remainingQuota = NightlyCap - _callsThisWindow;
                    if (remainingQuota > 0)
                    {
                        var refresh = scope.ServiceProvider.GetRequiredService<IMetadataRefreshService>();
                        var refreshed = await refresh.RefreshStaleBatchAsync(Math.Min(BatchSize, remainingQuota), stoppingToken);
                        _callsThisWindow += refreshed;
                        if (refreshed > 0)
                            logger.LogInformation(
                                "Metadata refresh pass fully refreshed {Count} anime ({Used}/{Cap} today).",
                                refreshed, _callsThisWindow, NightlyCap);
                    }
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
