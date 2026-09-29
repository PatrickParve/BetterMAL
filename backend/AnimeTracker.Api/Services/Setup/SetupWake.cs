namespace AnimeTracker.Api.Services.Setup;

/// <summary>A broadcast wake-up for setup's sleeping workers. A worker takes
/// <see cref="Next"/> <i>before</i> it looks at what there is to do, then waits on that task
/// if there is nothing: a <see cref="Pulse"/> that lands in between has already completed the
/// task it holds, so no wake-up is lost. Unlike <see cref="SetupTrigger"/>, which wakes one
/// waiter, every waiter wakes: Retry now must reach the MAL queue, the airing queue and the
/// Home check alike.</summary>
public sealed class SetupWake
{
    private readonly object _lock = new();
    private TaskCompletionSource _next = New();

    /// <summary>Completes at the next <see cref="Pulse"/>.</summary>
    public Task Next()
    {
        lock (_lock)
            return _next.Task;
    }

    /// <summary>Wakes everything waiting on a <see cref="Next"/> taken before now.</summary>
    public void Pulse()
    {
        TaskCompletionSource current;
        lock (_lock)
        {
            current = _next;
            _next = New();
        }

        current.TrySetResult();
    }

    // Asynchronous continuations, so a waiter resumes on the pool rather than inside the
    // caller of Pulse (a Retry now request, or a worker mid-save).
    private static TaskCompletionSource New() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
