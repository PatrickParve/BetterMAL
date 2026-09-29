namespace AnimeTracker.Api.Services.Airing.AniList;

/// <summary>Spaces outbound AniList calls by the rate limit AniList itself
/// states, instead of a fixed guess (add-first-run-setup design D11).
/// <list type="bullet">
/// <item><description>The spacing is <c>60 s / limit</c>, where <c>limit</c> is the last
/// <c>X-RateLimit-Limit</c> <see cref="Observe"/> was given. Until one has been
/// read it is <see cref="DefaultLimitPerMinute"/>, AniList's lowest published
/// limit: about one request every two seconds.</description></item>
/// <item><description>A response that reports nothing remaining holds the next dispatch
/// until the reset it names, or a full minute when it names none (AniList sends
/// no <c>X-RateLimit-Reset</c> on a 200).</description></item>
/// <item><description>A caller told to wait (a 429's <c>Retry-After</c>) blocks the pacer
/// itself (<see cref="BlockFor"/>), so every other caller waits it out too.</description></item>
/// </list>
/// One request is dispatched at a time, however many anime a caller's refresh
/// needs, and a multi-page schedule fetch stays inside the limit.</summary>
public class AniListRequestPacer(
    Func<DateTimeOffset>? now = null,
    Func<TimeSpan, CancellationToken, Task>? delay = null)
{
    /// <summary>What AniList allows while it is degraded, and so what the pacer
    /// assumes until a response has said otherwise.</summary>
    public const int DefaultLimitPerMinute = 30;

    /// <summary>AniList's rate-limit window, waited out when a response reports
    /// nothing remaining and gives no reset time.</summary>
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly Func<DateTimeOffset> _now = now ?? (() => DateTimeOffset.UtcNow);
    private readonly Func<TimeSpan, CancellationToken, Task> _delay = delay ?? Task.Delay;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly object _stateLock = new();
    private int _limit = DefaultLimitPerMinute;
    private DateTimeOffset _lastDispatch = DateTimeOffset.MinValue;
    private DateTimeOffset _blockedUntil = DateTimeOffset.MinValue;

    /// <summary>The gap kept between two dispatches, from the last limit seen.</summary>
    public TimeSpan Interval
    {
        get { lock (_stateLock) return TimeSpan.FromSeconds(60.0 / _limit); }
    }

    /// <summary>Takes in the rate-limit headers of a response, each null when the
    /// response had none. Called after every response, whatever its status.
    /// <paramref name="resetAt"/> is null when AniList sent no
    /// <c>X-RateLimit-Reset</c>.</summary>
    public void Observe(int? limit, int? remaining, DateTimeOffset? resetAt)
    {
        lock (_stateLock)
        {
            if (limit is > 0)
                _limit = limit.Value;

            if (remaining is not { } left || left > 0)
                return;

            // A reset further off than one window can only be a skewed clock: the
            // limit is per minute, so it is never waited out for longer.
            var cap = _now() + Window;
            var until = resetAt is { } reset && reset < cap ? reset : cap;
            _blockedUntil = Max(_blockedUntil, until);
        }
    }

    /// <summary>Holds every dispatch until <paramref name="duration"/> from now has
    /// passed, and returns that instant, so the caller can publish when
    /// requests resume.</summary>
    public DateTimeOffset BlockFor(TimeSpan duration)
    {
        lock (_stateLock)
        {
            var until = _now() + duration;
            _blockedUntil = Max(_blockedUntil, until);
            return until;
        }
    }

    public async Task WaitAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            TimeSpan wait;
            lock (_stateLock)
            {
                var current = _now();
                var spacing = TimeSpan.FromSeconds(60.0 / _limit) - (current - _lastDispatch);
                wait = TimeSpan.FromTicks(Math.Max(spacing.Ticks, (_blockedUntil - current).Ticks));
            }

            if (wait > TimeSpan.Zero)
                await _delay(wait, ct);

            lock (_stateLock)
                _lastDispatch = _now();
        }
        finally
        {
            _gate.Release();
        }
    }

    private static DateTimeOffset Max(DateTimeOffset a, DateTimeOffset b) => a >= b ? a : b;
}
