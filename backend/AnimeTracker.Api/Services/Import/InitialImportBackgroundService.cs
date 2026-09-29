using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using AnimeTracker.Api.Services.Setup;

namespace AnimeTracker.Api.Services.Import;

/// <summary>Runs the initial import when signaled — by the OAuth callback on
/// re-authorization, or by itself on startup if a token already exists (so an
/// import interrupted by an app/PC restart resumes automatically) — and retries
/// a run that couldn't finish on its own, after 1, 5, 15, 60 and 60 minutes in
/// turn, then not again until the next start or re-authorization (design.md
/// D9). No run is attempted, and no retry is planned, while the MyAnimeList
/// connection is lost.
/// <para>It does nothing until first-run setup has finished
/// (add-first-run-setup design D2): reading the list for the first time is
/// setup's own step. When it had to wait for that, setup ran in this process
/// and has just read the list, so the at-start signal is skipped, and setup's
/// finish opens <see cref="ListImportProgress.Gate"/> itself
/// (<see cref="ListImportProgress.MarkListReadBySetup"/>) for the file import; the
/// next start, or a re-authorization, signals it as usual.</para></summary>
public class InitialImportBackgroundService(
    IServiceScopeFactory scopeFactory,
    IImportTrigger trigger,
    ListImportProgress progress,
    SetupGate setupGate,
    ILogger<InitialImportBackgroundService> logger,
    IReadOnlyList<TimeSpan>? retrySchedule = null) : BackgroundService
{
    private static readonly IReadOnlyList<TimeSpan> DefaultRetrySchedule =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(60),
        TimeSpan.FromMinutes(60),
    ];

    private readonly IReadOnlyList<TimeSpan> _retrySchedule = retrySchedule ?? DefaultRetrySchedule;
    private int _retryAttempt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read before waiting: a gate that is still open here means setup runs in
        // this process, and reads the list itself.
        var waitedForSetup = !setupGate.IsFinished;
        await setupGate.WhenFinished.WaitAsync(stoppingToken);

        if (!waitedForSetup)
            await SignalIfConnectedAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            // Read before waiting, so no delay is armed — and no RetryAt
            // shown — while the connection is lost (design.md D9).
            //
            // _retryAttempt counts failures already seen, 1-based (0 means
            // none yet this sequence), so the *next* retry to plan is
            // _retryAttempt's 1-based position in the schedule — index
            // _retryAttempt - 1.
            var lostBeforeWait = await IsConnectionLostAsync(stoppingToken);
            TimeSpan? delay = !lostBeforeWait && progress.Gate.LastReadFailure is not null
                && _retryAttempt > 0 && _retryAttempt <= _retrySchedule.Count
                ? _retrySchedule[_retryAttempt - 1]
                : null;
            progress.SetRetryAt(delay is { } d ? DateTimeOffset.UtcNow + d : null);

            var timedOut = await WaitForTriggerOrRetryAsync(delay, stoppingToken);
            if (stoppingToken.IsCancellationRequested)
                break;

            // A genuine signal (a start, or a re-authorization) begins the
            // retry sequence afresh; a timeout is the retry itself and
            // leaves the sequence where it stood.
            if (!timedOut)
                _retryAttempt = 0;

            if (await IsConnectionLostAsync(stoppingToken))
            {
                progress.SetRetryAt(null);
                continue;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var importService = scope.ServiceProvider.GetRequiredService<IInitialImportService>();
                await importService.RunAsync(stoppingToken);

                // A run that went through the list with nothing left missing
                // ends the sequence; one that couldn't finish advances it
                // (design.md D9).
                _retryAttempt = progress.Gate.LastReadFailure is null ? 0 : _retryAttempt + 1;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (MalAuthorizationRequiredException)
            {
                // The connection was lost partway through this run — no
                // further retry is planned; re-authorizing signals a fresh
                // start instead (design.md D9). One past the schedule's
                // length keeps the "<= Count" delay check above from firing.
                _retryAttempt = _retrySchedule.Count + 1;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Initial import run failed.");
                _retryAttempt++;
            }
        }
    }

    /// <summary>Waits for a trigger signal, or for <paramref name="delay"/> to
    /// elapse when a retry is planned — a linked token cancelled after the
    /// delay. Returns true when the delay elapsed first rather than a signal
    /// or the host stopping (design.md D9: "a cancellation that isn't the
    /// host stopping means retry now").</summary>
    private async Task<bool> WaitForTriggerOrRetryAsync(TimeSpan? delay, CancellationToken stoppingToken)
    {
        if (delay is not { } d)
        {
            try
            {
                await trigger.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // stoppingToken fired — the caller's own check ends the loop.
            }

            return false;
        }

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        linked.CancelAfter(d);

        try
        {
            await trigger.WaitAsync(linked.Token);
            return false;
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return false;
        }
        catch (OperationCanceledException)
        {
            return true; // the delay elapsed, not the host stopping
        }
    }

    private async Task<bool> IsConnectionLostAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        var token = await tokenStore.GetAsync(ct);
        return token is null || token.ConnectionLostAt is not null;
    }

    private async Task SignalIfConnectedAsync(CancellationToken ct)
    {
        if (!await IsConnectionLostAsync(ct))
            trigger.Signal();
    }
}
