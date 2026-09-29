using AnimeTracker.Api.Services.Mal;
using AnimeTracker.Api.Services.Mal.Auth;
using Microsoft.Extensions.Options;

namespace AnimeTracker.Api.Services.Setup;

/// <summary>Owns the whole first run (add-first-run-setup design D1). It decides, once
/// at start, whether setup has anything to do and when it may begin:
/// <list type="number">
/// <item><description>a finished install (<see cref="SetupGate.IsFinished"/>) has no setup: it returns;</description></item>
/// <item><description>with a MyAnimeList credential missing it idles and sends no request, since fixing that needs an
/// <c>.env</c> edit and a restart;</description></item>
/// <item><description>with no login stored it waits for <see cref="ISetupTrigger"/>, which the OAuth callback signals
/// once a login is stored, so setup starts by itself with no further press;</description></item>
/// <item><description>then it runs the list step, and once the list has been read the MyAnimeList steps (details, then
/// series) and the AniList step (airing dates) side by side, with the Home check that finishes setup.</description></item>
/// </list>
/// Neither worker keeps a work list across restarts: each asks the database what is
/// left, so setup resumes from stored data. What only memory holds, the retry waits,
/// the ETA samples, the anime found to have no series and the entries left out for
/// their status, lives in <see cref="SetupRunState"/>, which this exposes as a
/// snapshot for the status read.</summary>
public class SetupCoordinator(
    SetupGate gate,
    ISetupTrigger trigger,
    SetupRunState state,
    IOptions<MalOptions> malOptions,
    IServiceScopeFactory scopeFactory,
    MalServiceHealth malHealth,
    AniListServiceHealth aniListHealth,
    ILogger<SetupCoordinator> logger,
    Func<DateTimeOffset>? now = null) : BackgroundService, ISetupCoordinator
{
    /// <summary>How long a step that stopped on an unexpected exception waits before it is
    /// started again. Every step resumes from stored data, so a restart of one is harmless.</summary>
    private static readonly TimeSpan RestartDelay = TimeSpan.FromSeconds(30);

    /// <summary>How often the Home check looks again when nothing has just been saved.</summary>
    private static readonly TimeSpan HomeCheckInterval = TimeSpan.FromSeconds(5);

    private readonly Func<DateTimeOffset> _clock = now ?? (() => DateTimeOffset.UtcNow);

    // What the workers wait on and what Retry now reaches (design D13): a wake-up for the
    // sleeping queues, a nudge for the Home check after every saved item, and one ladder of
    // waiting anime per service.
    private readonly SetupWake _wake = new();
    private readonly SetupWake _progress = new();
    private readonly RetryLadder _malLadder = new();
    private readonly RetryLadder _aniListLadder = new();

    // Set once the airing step has nothing left, so the finish that comes after can't leave
    // IsDraining true with nothing draining.
    private volatile bool _airingEnded;

    public bool IsDraining => state.IsDraining;

    public SetupSnapshot GetSnapshot() => state.Snapshot();

    public void RetryNow()
    {
        // Every waiting anime and each service's paused queue is due at once, and the
        // sleeping workers wake to find out. A throttle a service has asked the app to wait
        // out stays: ServiceHealth.MakeDueNow leaves it, and so does nothing here.
        var at = _clock();
        malHealth.MakeDueNow();
        aniListHealth.MakeDueNow();
        _malLadder.MakeAllDueNow(at);
        _aniListLadder.MakeAllDueNow(at);
        _wake.Pulse();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (gate.IsFinished)
                return;

            var missing = MalCredentials.Missing(malOptions.Value);
            if (missing.Count > 0)
            {
                // No request goes to MyAnimeList or AniList. The status read names
                // what is missing; setting it needs a restart, so there is nothing
                // to wait for here.
                logger.LogWarning(
                    "Setup cannot start: {Missing} must be set in .env and the app restarted.", string.Join(" and ", missing));
                await Task.Delay(Timeout.Infinite, stoppingToken);
                return;
            }

            // A signal can be spurious (a Reconnect with a login already stored) or
            // stale, so the login is checked again after every wake-up.
            while (!await IsLoginStoredAsync(stoppingToken))
                await trigger.WaitAsync(stoppingToken);

            await RunAsync(stoppingToken);
        }
        // Only our own shutdown ends it; a failure inside setup's work is that
        // work's to handle, never the host's.
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task<bool> IsLoginStoredAsync(CancellationToken ct)
    {
        // A login MyAnimeList has since refused is still stored: Reconnect is a
        // step of setup's screen, not a reason to wait for a first sign-in.
        using var scope = scopeFactory.CreateScope();
        var tokenStore = scope.ServiceProvider.GetRequiredService<IMalTokenStore>();
        return await tokenStore.GetAsync(ct) is not null;
    }

    /// <summary>The whole run (design D1): the list read, then the MyAnimeList steps (details, then
    /// series) and the AniList step side by side, with the Home check watching all of it. Every piece
    /// asks the database what is left, so a restart resumes from stored data.
    /// <para>The steps that need no login start once the list has been read, or once the login was
    /// refused (they carry on with what is stored). Setup finishes when the Home check says so. The
    /// MyAnimeList steps have nothing left by then and end; the airing step keeps draining what is
    /// left until nothing is, with <see cref="IsDraining"/> set.</para></summary>
    private async Task RunAsync(CancellationToken ct)
    {
        logger.LogInformation("Setup: a login is stored and both credentials are set.");
        _airingEnded = false;

        var context = new SetupWorkerContext(scopeFactory, state, _wake, _progress, _clock, logger);
        var listStored = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var listRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var listStep = new SetupListStep(context, trigger, malHealth, _malLadder);
        var detailsStep = new SetupDetailsStep(context, malHealth, _malLadder);
        var seriesStep = new SetupSeriesStep(context, malHealth, _malLadder);
        var airingStep = new SetupAiringStep(context, aniListHealth, _aniListLadder);
        var homeCheck = new SetupHomeCheck(context, _aniListLadder, aniListHealth);

        var list = SuperviseAsync("list step", token => listStep.RunAsync(listStored, listRead, token), ct);
        await Task.WhenAny(listStored.Task, list);
        ct.ThrowIfCancellationRequested();

        var malSteps = SuperviseAsync("MyAnimeList steps", token => RunUntilListReadAsync(
            [SetupStep.Details, SetupStep.Series],
            async round =>
            {
                // Series starts after the details have finished, as the spec says.
                await detailsStep.RunAsync(round);
                await seriesStep.RunAsync(round);
            },
            listRead.Task, token), ct);

        var airing = SuperviseAsync("airing step", async token =>
        {
            try
            {
                await RunUntilListReadAsync([SetupStep.Airing], airingStep.RunAsync, listRead.Task, token);
            }
            finally
            {
                _airingEnded = true;
                state.SetDraining(false);
            }
        }, ct);

        var home = SuperviseAsync("Home check", token => RunHomeCheckAsync(homeCheck, token), ct);

        await Task.WhenAll(list, malSteps, airing, home);
    }

    /// <summary>Runs a step (or steps) until nothing is left <i>and</i> the list has been read. A round that
    /// began before the read finished ran on the entries stored at the time: with a refused login that is
    /// all there is, and after a Reconnect the read stores more. So the steps report waiting until the read
    /// has run, and go round once more. A round that began after it saw everything the read stored.</summary>
    private async Task RunUntilListReadAsync(
        SetupStep[] steps, Func<CancellationToken, Task> round, Task listRead, CancellationToken ct)
    {
        while (true)
        {
            var listReadBefore = listRead.IsCompleted;
            await round(ct);
            if (listReadBefore)
                return;

            foreach (var step in steps)
                state.SetPhase(step, SetupStepPhase.Waiting);
            await listRead.WaitAsync(ct);
        }
    }

    /// <summary>Evaluates the Home conditions after every saved item and every few seconds, and finishes
    /// setup the first time they hold.</summary>
    private async Task RunHomeCheckAsync(SetupHomeCheck homeCheck, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Taken before looking, so an item saved meanwhile brings the next look at once.
            var itemSaved = _progress.Next();
            if (await homeCheck.HoldsAsync(ct))
            {
                // Before the gate opens: the hourly airing catch-up starts the moment it does,
                // and must already see that this process is still fetching those anime.
                if (!_airingEnded)
                {
                    state.SetDraining(true);
                    if (_airingEnded)
                        state.SetDraining(false); // it ended while we were setting the flag
                }

                await gate.MarkFinishedAsync(ct);
                logger.LogInformation("Setup finished: the library is complete. {Draining}",
                    state.IsDraining ? "Airing dates carry on in the background." : "");
                return;
            }

            await SetupQueue.SleepAsync(itemSaved, HomeCheckInterval, ct);
        }
    }

    /// <summary>Runs one piece of setup until it completes. A failure nothing inside handled (a database
    /// that went away, say) ends the piece; it is logged and started again after a pause, since every
    /// piece resumes from stored data. Only our own shutdown ends it for good.</summary>
    private async Task SuperviseAsync(string name, Func<CancellationToken, Task> work, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await work(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Setup's {Step} stopped unexpectedly; starting it again in {Seconds} s.", name, RestartDelay.TotalSeconds);
                await Task.Delay(RestartDelay, ct);
            }
        }
    }
}
