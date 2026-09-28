using AnimeTracker.Api.Services.Updates;

namespace AnimeTracker.Api.Services.Metadata;

/// <summary>Spreads tiered full-detail refresh across small batches on a steady
/// interval instead of one nightly burst (design.md risk: "Refresh job
/// hammering the API on a large library"). Each pass is cheap when nothing is
/// due — the two stages simply record no attempts on their <see cref="MalCallTally"/>.
/// A fixed nightly cap limits the total call attempts made per calendar day
/// (UTC), successes and failures both counting; once reached, no more calls
/// happen until the next day, and any remaining stale candidates simply carry
/// over automatically since they stay the most-stale next time. Announcement
/// resolution shares this same tick, pacer and cap (design.md D11): it runs
/// first each pass so a large staleness backlog can never starve news of its
/// calls, and both sets of calls draw from the one daily budget. A pass ends
/// at the first outage-type failure (design D2/D3) in either stage — a failed
/// resolution fetch also skips that pass's staleness batch — and the pass
/// that follows skips the anime whose call ended it (design D6), remembered
/// only in <c>_skipAnimeIdNextPass</c>, in memory, for that one pass.</summary>
public class MetadataRefreshBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<MetadataRefreshBackgroundService> logger) : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromMinutes(10);
    private const int BatchSize = 20;
    private const int AnnouncementBatchSize = 10;
    internal const int NightlyCap = 500;

    private DateOnly _capWindowDate = DateOnly.MinValue;
    private int _callsThisWindow;
    private int? _skipAnimeIdNextPass;

    /// <summary>Exposes the running per-day count for tests (`InternalsVisibleTo`).</summary>
    internal int CallsThisWindow => _callsThisWindow;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunPassAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
            }
            // Only our own shutdown ends the loop; an HttpClient timeout is a
            // TaskCanceledException too, and fails just this pass.
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
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

    /// <summary>Runs one pass: resolution, then (unless it ended the pass) the
    /// staleness batch. Exposed internally so tests can drive single passes
    /// without a clock abstraction — <paramref name="today"/> stands in for
    /// <c>DateTime.UtcNow</c>'s date, giving tests the day-rollover behaviour
    /// without a <c>TimeProvider</c>.</summary>
    internal async Task RunPassAsync(DateOnly today, CancellationToken ct)
    {
        if (today != _capWindowDate)
        {
            _capWindowDate = today;
            _callsThisWindow = 0;
        }

        var remainingQuota = NightlyCap - _callsThisWindow;
        if (remainingQuota <= 0)
            return;

        // The anime whose call ended the previous pass (design D6): taken
        // and cleared up front, so this pass tries it again unless a fresh
        // outage on some other anime re-sets the field below.
        var skip = _skipAnimeIdNextPass;
        _skipAnimeIdNextPass = null;

        using var scope = scopeFactory.CreateScope();

        var resolution = scope.ServiceProvider.GetRequiredService<IAnnouncementResolutionService>();
        var resolutionTally = new MalCallTally();
        try
        {
            await resolution.ResolveAsync(Math.Min(AnnouncementBatchSize, remainingQuota), skip, resolutionTally, ct);
        }
        finally
        {
            // Attempts count even if ResolveAsync throws afterwards, e.g. a
            // cancellation past its first call.
            _callsThisWindow += resolutionTally.Attempts;
        }

        if (resolutionTally.Attempts > 0)
            logger.LogInformation(
                "Announcement resolution made {Attempts} MAL call(s), {Succeeded} succeeded ({Used}/{Cap} today).",
                resolutionTally.Attempts, resolutionTally.Succeeded, _callsThisWindow, NightlyCap);

        if (resolutionTally.MalUnavailable)
        {
            // A failed resolution fetch also skips this pass's staleness
            // batch (spec: "A failed announcement fetch skips the refresh
            // batch") — the next pass is the retry for both stages.
            _skipAnimeIdNextPass = resolutionTally.UnavailableAnimeId;
            logger.LogInformation(
                "Metadata refresh pass ended because MyAnimeList appears unavailable; anime {AnimeId} will be skipped next pass.",
                resolutionTally.UnavailableAnimeId);
            return;
        }

        remainingQuota = NightlyCap - _callsThisWindow;
        if (remainingQuota <= 0)
            return;

        var refresh = scope.ServiceProvider.GetRequiredService<IMetadataRefreshService>();
        var refreshTally = new MalCallTally();
        try
        {
            await refresh.RefreshStaleBatchAsync(Math.Min(BatchSize, remainingQuota), skip, refreshTally, ct);
        }
        finally
        {
            _callsThisWindow += refreshTally.Attempts;
        }

        if (refreshTally.Attempts > 0)
            logger.LogInformation(
                "Metadata refresh made {Attempts} MAL call(s), {Succeeded} succeeded ({Used}/{Cap} today).",
                refreshTally.Attempts, refreshTally.Succeeded, _callsThisWindow, NightlyCap);

        if (refreshTally.MalUnavailable)
        {
            _skipAnimeIdNextPass = refreshTally.UnavailableAnimeId;
            logger.LogInformation(
                "Metadata refresh pass ended because MyAnimeList appears unavailable; anime {AnimeId} will be skipped next pass.",
                refreshTally.UnavailableAnimeId);
        }
    }
}
