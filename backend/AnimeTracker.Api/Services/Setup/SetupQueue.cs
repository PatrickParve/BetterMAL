namespace AnimeTracker.Api.Services.Setup;

/// <summary>What every setup worker shares: how to open a scope for one item, where its
/// in-memory state and the wake-ups live, and what time it is. One item, or one batch, is
/// processed in a scope and so a DbContext of its own, so a failed save can't leave tracked
/// entities behind for the next.</summary>
internal sealed record SetupWorkerContext(
    IServiceScopeFactory ScopeFactory,
    SetupRunState State,
    SetupWake Wake,
    SetupWake Progress,
    Func<DateTimeOffset> Clock,
    ILogger Logger);

/// <summary>One setup queue's loop (design D1, D12, D13): ask the database what is left, take
/// the next item or batch that is due, process it, and go round again. Each step's rules live
/// here once, so the details, series and airing queues can't drift apart:
/// <list type="bullet">
/// <item><description><b>Order and keeping place.</b> The targets come back in the step's own
/// order. An item waiting on its retry is passed over and, when its wait ends, is taken next
/// in its original place (<see cref="RetryLadder"/>).</description></item>
/// <item><description><b>A service that is down pauses the queue.</b> It takes nothing until the
/// service's own ladder is due, then makes one attempt with one item. A success resets the
/// service and the queue resumes; a failure moves its ladder on
/// (<see cref="ServiceHealth.Advance"/>).</description></item>
/// <item><description><b>Sleeping.</b> With only waiting items left, it sleeps until the
/// soonest is due, and wakes early for Retry now (<see cref="SetupWake"/>).</description></item>
/// <item><description><b>The estimate.</b> What a round finished is the drop in what is left since
/// the last look, so an item done, an item skipped for good and the several list anime one franchise
/// build settled all count, and an anime the list read stored meanwhile only raises what is left. The
/// estimate starts over whenever the queue stops making progress: paused, sleeping on waits, or
/// starting a round (<see cref="EtaEstimator"/>).</description></item>
/// </list>
/// A throttle wait is not the queue's to handle: the request pacers hold the next request
/// back themselves, and the status read shows it from the service's health.</summary>
internal sealed class SetupQueue(
    SetupStep step,
    ServiceHealth health,
    RetryLadder ladder,
    SetupWorkerContext context)
{
    /// <summary>Runs until nothing is left. <paramref name="getTargets"/> returns the ids still
    /// to do, in the order to do them; it is asked again after every item, so it sees what any
    /// other path has written meanwhile. <paramref name="processBatch"/> handles the ids it is
    /// given and records each one's outcome itself: <see cref="RetryLadder.Clear"/> for a
    /// success, <see cref="RetryLadder.RecordFailure"/> for a temporary failure, whatever
    /// marks a permanent one. It must not let an item's failure escape, since that would end
    /// the queue; only a cancellation of <paramref name="ct"/> does. No targets ends the
    /// queue: what the list read has yet to store is the coordinator's to run another round
    /// for.</summary>
    public async Task RunAsync(
        Func<CancellationToken, Task<IReadOnlyList<int>>> getTargets,
        Func<IReadOnlyList<int>, CancellationToken, Task> processBatch,
        int batchSize,
        CancellationToken ct)
    {
        // A round starts from no pace at all: whatever the step did in an earlier round, or
        // waited through since, says nothing about this one.
        context.State.ResetEta(step);
        int? remainingBefore = null;

        while (true)
        {
            ct.ThrowIfCancellationRequested();

            // Taken before looking, so a Retry now that lands while the targets are being
            // read still wakes the sleep below.
            var woken = context.Wake.Next();
            var targets = await getTargets(ct);
            var now = context.Clock();
            context.State.SetWaitingRetry(step, ladder.Waiting(targets, now));

            context.State.RecordProgress(
                step, remainingBefore is { } before ? before - targets.Count : 0, targets.Count, now);
            remainingBefore = targets.Count;

            if (targets.Count == 0)
            {
                context.State.SetPhase(step, SetupStepPhase.Done);
                context.State.ResetEta(step);
                return;
            }

            if (health.IsDown && health.NextTryAt is { } tryAt && tryAt > now)
            {
                context.State.SetPhase(step, SetupStepPhase.Paused);
                context.State.ResetEta(step);
                await SleepAsync(woken, tryAt - now, ct);
                continue;
            }

            // A down service whose ladder is due gets one attempt, with the next item.
            var probing = health.IsDown;
            var due = ladder.Due(targets, now, probing ? 1 : batchSize);
            if (due.Count == 0)
            {
                // Every target is waiting on its own retry, so one of them has a wait to end.
                context.State.SetPhase(step, probing ? SetupStepPhase.Paused : SetupStepPhase.Running);
                context.State.ResetEta(step);
                await SleepAsync(woken, ladder.EarliestDue(targets, now) - now, ct);
                continue;
            }

            context.State.SetPhase(step, probing ? SetupStepPhase.Paused : SetupStepPhase.Running);
            if (probing)
                context.State.ResetEta(step);
            await processBatch(due, ct);
            context.Progress.Pulse();

            if (probing && health.IsDown)
                health.Advance();
        }
    }

    /// <summary>Waits for a wake-up or <paramref name="delay"/> passing (forever when null),
    /// whichever comes first. The timer is cancelled as soon as the wake-up wins, so a long wait
    /// doesn't leave one running for an hour.</summary>
    internal static async Task SleepAsync(Task woken, TimeSpan? delay, CancellationToken ct)
    {
        using var timerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timer = Task.Delay(delay ?? Timeout.InfiniteTimeSpan, timerCts.Token);

        await Task.WhenAny(woken, timer);
        await timerCts.CancelAsync();

        // A cancelled sleep ends normally (the timer is what got cancelled), so a caller looping
        // on it would spin forever at shutdown. Ending it with the cancellation makes that
        // impossible.
        ct.ThrowIfCancellationRequested();
    }
}
