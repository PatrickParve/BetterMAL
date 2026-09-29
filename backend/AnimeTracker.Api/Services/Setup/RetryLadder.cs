namespace AnimeTracker.Api.Services.Setup;

/// <summary>Per-item retry waits for one setup queue (design D13; spec "A failing anime
/// is retried and keeps its place"). An item that fails for a temporary reason is
/// recorded here and waits 1, 5, 15 and 60 minutes, then 60 more for as long as it
/// keeps failing (<see cref="RetrySchedule"/>). A success clears it.
/// <para>The queue's order is never touched. A worker asks for the first item, in its
/// own priority order, that is not waiting (<see cref="NextDue"/>), so a waiting item is
/// passed over and, the moment its wait ends, is again ahead of everything after it in
/// the order: it keeps its place rather than moving to the end.</para>
/// <para>Held in memory only, by decision (proposal §4): a restart forgets every wait, and
/// everything that was waiting is tried at once. Every member is guarded by one lock, so a
/// worker and Retry now can share it. Times are passed in, so a test decides what "now"
/// is.</para></summary>
public sealed class RetryLadder
{
    private sealed record RetryState(int Failures, DateTimeOffset DueAt);

    private readonly object _lock = new();
    private readonly Dictionary<int, RetryState> _states = [];

    /// <summary>The item failed for a temporary reason at <paramref name="now"/>: it waits
    /// for the next step of the ladder, one further than its last failure's.</summary>
    public void RecordFailure(int id, DateTimeOffset now)
    {
        lock (_lock)
        {
            var failures = _states.TryGetValue(id, out var state) ? state.Failures : 0;
            _states[id] = new RetryState(failures + 1, now + RetrySchedule.DelayAfter(failures));
        }
    }

    /// <summary>The item succeeded: its failures are forgotten, and a later failure starts
    /// the ladder over.</summary>
    public void Clear(int id)
    {
        lock (_lock)
            _states.Remove(id);
    }

    /// <summary>True when the item has failed and not yet succeeded, whether or not its wait
    /// has ended. Home's airing condition counts such an anime as waiting on a retry.</summary>
    public bool HasFailed(int id)
    {
        lock (_lock)
            return _states.ContainsKey(id);
    }

    /// <summary>True while the item's wait has not ended.</summary>
    public bool IsWaiting(int id, DateTimeOffset now)
    {
        lock (_lock)
            return _states.TryGetValue(id, out var state) && state.DueAt > now;
    }

    /// <summary>The first of <paramref name="orderedIds"/> that is not waiting, or null when
    /// every one of them is. An item that has never failed, or whose wait has ended, is
    /// due.</summary>
    public int? NextDue(IEnumerable<int> orderedIds, DateTimeOffset now)
    {
        lock (_lock)
        {
            foreach (var id in orderedIds)
            {
                if (!IsWaitingLocked(id, now))
                    return id;
            }

            return null;
        }
    }

    /// <summary>The first <paramref name="max"/> of <paramref name="orderedIds"/> that are
    /// not waiting, in that order: a batch, when one request can cover several items.</summary>
    public IReadOnlyList<int> Due(IEnumerable<int> orderedIds, DateTimeOffset now, int max)
    {
        lock (_lock)
        {
            var due = new List<int>();
            foreach (var id in orderedIds)
            {
                if (due.Count >= max)
                    break;
                if (!IsWaitingLocked(id, now))
                    due.Add(id);
            }

            return due;
        }
    }

    /// <summary>When the soonest wait among <paramref name="among"/> ends, or null when none
    /// of them is waiting. What a worker with only waiting items left sleeps until.</summary>
    public DateTimeOffset? EarliestDue(IEnumerable<int> among, DateTimeOffset now)
    {
        lock (_lock)
        {
            DateTimeOffset? earliest = null;
            foreach (var id in among)
            {
                if (_states.TryGetValue(id, out var state) && state.DueAt > now && (earliest is null || state.DueAt < earliest))
                    earliest = state.DueAt;
            }

            return earliest;
        }
    }

    /// <summary>How many of <paramref name="among"/> are waiting and when the soonest is due,
    /// for the status read; null when none is.</summary>
    public SetupRetryWait? Waiting(IEnumerable<int> among, DateTimeOffset now)
    {
        lock (_lock)
        {
            var count = 0;
            DateTimeOffset? earliest = null;
            foreach (var id in among)
            {
                if (!_states.TryGetValue(id, out var state) || state.DueAt <= now)
                    continue;

                count++;
                if (earliest is null || state.DueAt < earliest)
                    earliest = state.DueAt;
            }

            return count == 0 ? null : new SetupRetryWait(count, earliest!.Value);
        }
    }

    /// <summary>Retry now: every waiting item is due at <paramref name="now"/>. Each keeps its
    /// failures, so one that fails again goes on up the ladder.</summary>
    public void MakeAllDueNow(DateTimeOffset now)
    {
        lock (_lock)
        {
            foreach (var (id, state) in _states.ToList())
            {
                if (state.DueAt > now)
                    _states[id] = state with { DueAt = now };
            }
        }
    }

    private bool IsWaitingLocked(int id, DateTimeOffset now) =>
        _states.TryGetValue(id, out var state) && state.DueAt > now;
}
