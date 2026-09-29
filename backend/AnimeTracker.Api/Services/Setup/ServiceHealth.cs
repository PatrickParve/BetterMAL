namespace AnimeTracker.Api.Services.Setup;

/// <summary>What the app knows about one outside service right now: whether it
/// is throttling requests, whether it appears to be down, and when its paused
/// queue may try again (design D12; spec "A service that is down pauses its own
/// queue" and "Throttling is shown with its resume time").
/// <para>Fed where requests are actually made — <c>MalAuthPacingHandler</c> for
/// MyAnimeList and <c>AniListClient.PostAsync</c> for AniList — so it counts
/// requests, not anime: one failed 25-anime AniList batch is one strike. Only
/// a temporary failure counts (network error, timeout, 5xx, or a throttle that
/// outlasted its retries). Any request the service answers otherwise resets the
/// streak.</para>
/// <para>Held in memory only, by decision (proposal §4): a restart forgets it,
/// and the first request after it probes the service again. Every member is
/// guarded by one lock, so the setup workers, the HTTP layer and the status
/// read can share it.</para></summary>
public class ServiceHealth(string name, Func<DateTimeOffset>? now = null)
{
    /// <summary>Consecutive temporary failures, with no success in between, after
    /// which the service appears down.</summary>
    public const int DownAfterFailures = 3;

    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    private readonly object _lock = new();
    private int _consecutiveFailures;
    private int _failedTries;
    private DateTimeOffset? _nextTryAt;
    private DateTimeOffset? _throttledUntil;

    /// <summary>The name the setup screen shows: "MyAnimeList" or "AniList".</summary>
    public string Name => name;

    public int ConsecutiveFailures
    {
        get { lock (_lock) return _consecutiveFailures; }
    }

    /// <summary>True from the third consecutive temporary failure until a request
    /// succeeds (or <see cref="Reset"/>).</summary>
    public bool IsDown
    {
        get { lock (_lock) return _consecutiveFailures >= DownAfterFailures; }
    }

    /// <summary>When the service's paused queue may make its next attempt: 1, 5, 15
    /// and 60 minutes after it went down, then every 60 (<see cref="RetrySchedule"/>).
    /// Null while the service isn't down.</summary>
    public DateTimeOffset? NextTryAt
    {
        get { lock (_lock) return _nextTryAt; }
    }

    /// <summary>The instant the service's throttle wait ends, or null when nothing
    /// is being waited out. A wait that has run out reads as null, so a reader
    /// never shows "resuming in 0 s" for a throttle that is already over.</summary>
    public DateTimeOffset? ThrottledUntil
    {
        get
        {
            lock (_lock)
                return _throttledUntil is { } until && until > _now() ? until : null;
        }
    }

    /// <summary>The service answered a request, for any reason but a temporary
    /// failure: the streak ends and the ladder starts over.</summary>
    public void RecordSuccess() => Reset();

    /// <summary>A request to the service failed for a temporary reason. The third
    /// in a row takes the service down and starts its ladder.</summary>
    public void RecordTemporaryFailure()
    {
        lock (_lock)
        {
            if (_consecutiveFailures < int.MaxValue)
                _consecutiveFailures++;

            if (_consecutiveFailures >= DownAfterFailures && _nextTryAt is null)
            {
                _failedTries = 0;
                _nextTryAt = _now() + RetrySchedule.DelayAfter(_failedTries);
            }
        }
    }

    /// <summary>The queue's attempt at a down service failed: the next try is due
    /// one step further up the ladder. Nothing happens while the service isn't down.</summary>
    public void Advance()
    {
        lock (_lock)
        {
            if (_consecutiveFailures < DownAfterFailures)
                return;

            _failedTries++;
            _nextTryAt = _now() + RetrySchedule.DelayAfter(_failedTries);
        }
    }

    /// <summary>Forgets the streak and the ladder: the service is treated as up.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _consecutiveFailures = 0;
            _failedTries = 0;
            _nextTryAt = null;
        }
    }

    /// <summary>Makes the paused queue's next attempt due now (Retry now). Leaves the
    /// ladder's place, and the throttle, alone: a service that has asked to be left
    /// be still has to be waited out.</summary>
    public void MakeDueNow()
    {
        lock (_lock)
        {
            if (_consecutiveFailures >= DownAfterFailures)
                _nextTryAt = _now();
        }
    }

    /// <summary>The service asked the app to wait until <paramref name="at"/>. Set
    /// before the wait begins, so a reader sees it while it runs.</summary>
    public void SetThrottledUntil(DateTimeOffset at)
    {
        lock (_lock)
            _throttledUntil = at;
    }

    /// <summary>The service answered with something other than a throttle.</summary>
    public void ClearThrottle()
    {
        lock (_lock)
            _throttledUntil = null;
    }
}

/// <summary>MyAnimeList's health (design D12), fed by <c>MalAuthPacingHandler</c>.</summary>
public sealed class MalServiceHealth(Func<DateTimeOffset>? now = null) : ServiceHealth("MyAnimeList", now);

/// <summary>AniList's health (design D12), fed by <c>AniListClient</c>.</summary>
public sealed class AniListServiceHealth(Func<DateTimeOffset>? now = null) : ServiceHealth("AniList", now);
