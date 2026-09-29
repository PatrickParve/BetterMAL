namespace AnimeTracker.Api.Services.Setup;

/// <summary>Turns one step's recent progress into an estimate of the time it has left
/// (design D17). It keeps the last <see cref="RingSize"/> completion events, each the
/// moment some work finished and how many items it finished, and reads the pace from them:
/// the items finished after the oldest event, over the time from the oldest event to the
/// newest. Items rather than events, because one event may finish many: a list page of 100
/// entries, an airing batch of 25 anime, a franchise build that settles a dozen list anime.
/// Counting the events instead would make the estimate as lumpy as the work.
/// <para>There is no estimate until there is enough to go on: fewer than
/// <see cref="MinCompletions"/> items finished, or less than <see cref="MinSpan"/> between the
/// oldest and newest event, gives null. A step that stops making progress (its service is down,
/// only waiting anime are left) must <see cref="Clear"/> the estimator, or the wait would be
/// averaged into the pace and the estimate would stay wrong for the next
/// <see cref="RingSize"/> events. A throttle or a refused login are not the estimator's to
/// know: the status read leaves the estimate out while either holds.</para>
/// <para>Not thread-safe: <see cref="SetupRunState"/> holds every estimator under its own
/// lock.</para></summary>
internal sealed class EtaEstimator
{
    internal const int RingSize = 30;
    internal const int MinCompletions = 5;
    internal static readonly TimeSpan MinSpan = TimeSpan.FromSeconds(10);

    private readonly Queue<(DateTimeOffset At, int Count)> _events = new(RingSize);

    /// <summary>Notes that <paramref name="completed"/> items finished at <paramref name="at"/>.
    /// Nothing finished is nothing to note.</summary>
    public void Record(int completed, DateTimeOffset at)
    {
        if (completed <= 0)
            return;

        if (_events.Count == RingSize)
            _events.Dequeue();
        _events.Enqueue((at, completed));
    }

    /// <summary>The seconds left for <paramref name="remaining"/> more items at the pace of the
    /// recorded events, or null while there is too little progress to say. The oldest event is
    /// the starting line, not part of the pace: its items finished before the clock started.</summary>
    public double? EtaSeconds(int remaining)
    {
        if (_events.Count < 2)
            return null;

        var completions = _events.Sum(e => e.Count);
        if (completions < MinCompletions)
            return null;

        var oldest = _events.Peek();
        var newest = _events.Last();
        var span = newest.At - oldest.At;
        if (span < MinSpan)
            return null;

        var itemsPerSecond = (completions - oldest.Count) / span.TotalSeconds;
        return Math.Max(remaining, 0) / itemsPerSecond;
    }

    public void Clear() => _events.Clear();
}
