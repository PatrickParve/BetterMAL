namespace AnimeTracker.Api.Services.Jobs;

/// <summary>Runs the four in-request actions (sync now, reconciliation, and
/// the held-decision pair) in the background instead of inside the request
/// that started them (design.md D4). Registered as both a singleton and a
/// hosted service so it has a lifetime of its own to run work on, and to
/// cancel and await on shutdown — never the request's own token, which ends
/// as soon as the response is sent.
///
/// A controller passes the concrete tracker (not just the sink interface) so
/// its own ending rules — a partial run that must fail with a specific
/// reason — stay in the controller's work lambda, which closes over the same
/// tracker instance. This runner only starts the job once, keeps the task
/// alive past the request, and guarantees an ending: an exception fails the
/// job, and work that returns without failing completes it.</summary>
public class BackgroundJobRunner(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private readonly Lock _lock = new();
    private readonly HashSet<Task> _inFlight = new();
    private CancellationToken _stoppingToken;

    public bool TryStart(JobProgressTracker tracker, Func<IServiceProvider, IJobProgressSink, CancellationToken, Task> work) =>
        TryStart(tracker, tracker.TryBegin, work);

    /// <summary>Starts with a custom begin check in place of the tracker's own
    /// parameterless <see cref="JobProgressTracker.TryBegin"/> — used by the
    /// held-decision pair, whose begin step also records which action is
    /// starting under the same lock (design.md D18:
    /// <c>HeldDecisionProgress.TryBegin(action)</c>).</summary>
    public bool TryStart(JobProgressTracker tracker, Func<bool> tryBegin, Func<IServiceProvider, IJobProgressSink, CancellationToken, Task> work)
    {
        if (!tryBegin())
            return false;

        var task = RunAsync(tracker, work);
        lock (_lock)
            _inFlight.Add(task);

        _ = task.ContinueWith(t =>
        {
            lock (_lock)
                _inFlight.Remove(t);
        }, TaskScheduler.Default);

        return true;
    }

    private async Task RunAsync(JobProgressTracker tracker, Func<IServiceProvider, IJobProgressSink, CancellationToken, Task> work)
    {
        var stoppingToken = _stoppingToken;
        using var scope = scopeFactory.CreateScope();

        try
        {
            await work(scope.ServiceProvider, tracker, stoppingToken);

            // A guard against an oversight in the work itself, not the normal
            // path for jobs whose work ends by calling Fail() on a partial
            // run — this only fires when the work returned having decided
            // nothing, and the job would otherwise be stuck Running forever.
            if (tracker.Snapshot.Phase == JobPhase.Running)
                tracker.Complete();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The application itself is stopping — job state is in memory
            // and is lost with the process either way (design.md D3).
        }
        catch (Exception ex)
        {
            tracker.Fail(JobFailure.Describe(ex, "MyAnimeList"));
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _stoppingToken = stoppingToken;

        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stoppingToken.Register(() => stopped.TrySetResult());
        await stopped.Task;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        List<Task> inFlight;
        lock (_lock)
            inFlight = _inFlight.ToList();

        if (inFlight.Count == 0)
            return;

        try
        {
            await Task.WhenAll(inFlight).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Shutdown's own grace period ran out first — the process is
            // ending regardless, and job state is in memory only.
        }
    }
}
