using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Import;

/// <summary>The MAL list import's tracker (design.md D8). Unlike every other
/// job it is shown only while it has something to report: a run stays quiet
/// while it reads the list, because until then it can't know whether it has
/// work, except on a device with no entries at all, where a first import is
/// certain to have work and so is shown from the moment it starts.
///
/// The base <see cref="JobProgressTracker"/> still drives the real,
/// current-run lifecycle — <see cref="Gate"/>'s <c>Running</c> comes straight
/// from it, and it's what lets a stale run end and a fresh one begin. On top
/// of that, this tracks whether the current run is shown, and freezes what a
/// shown run last looked like so a later quiet run has something to fall back
/// on. <see cref="Snapshot"/> overrides the base member to answer with
/// whichever of those the page should currently see, and
/// <see cref="MarkOutcomeSeen"/> overrides it too so it acknowledges
/// whichever snapshot <see cref="Snapshot"/> is currently showing (design.md
/// D3). Lock order stays subclass-then-base throughout this class, as
/// <see cref="Snapshot"/> and <see cref="Gate"/> already nest, so no new
/// deadlock is possible.</summary>
public sealed class ListImportProgress : JobProgressTracker
{
    private readonly Lock _lock = new();
    private JobSnapshot _shown = new(JobPhase.NotStarted, 0, null, null, null, null);
    private bool _visible;
    private bool _wentThroughSinceStart;
    private string? _lastReadFailure;
    private DateTimeOffset? _retryAt;

    /// <summary>What the Settings page currently sees: the live run while
    /// it's shown, or whatever the last shown run left behind while this run
    /// is quiet (design.md D8).</summary>
    public override JobSnapshot Snapshot
    {
        get
        {
            lock (_lock)
                return (_visible ? base.Snapshot : _shown) with { RetryAt = _retryAt };
        }
    }

    /// <summary>Acknowledges whichever snapshot <see cref="Snapshot"/> is
    /// currently showing: the live run while it's shown, or the frozen
    /// <see cref="_shown"/> snapshot while quiet — mirroring
    /// <see cref="Snapshot"/>'s own choice, so a shown failure followed by a
    /// quiet retry can still be acknowledged (design.md D3).</summary>
    public override void MarkOutcomeSeen(DateTimeOffset finishedAt)
    {
        lock (_lock)
        {
            if (_visible)
                base.MarkOutcomeSeen(finishedAt);
            else if (_shown.FinishedAt == finishedAt)
                _shown = _shown with { OutcomeSeen = true };
        }
    }

    /// <summary>The file import's gate reads this instead of
    /// <see cref="Snapshot"/>, because its refusal needs to tell a quiet run
    /// apart from no run at all (design.md D10).</summary>
    public (bool Running, bool WentThroughSinceStart, string? LastReadFailure, DateTimeOffset? RetryAt) Gate
    {
        get
        {
            lock (_lock)
                return (base.Snapshot.Phase == JobPhase.Running, _wentThroughSinceStart, _lastReadFailure, _retryAt);
        }
    }

    /// <summary>Starts a run. Only a device with an empty list is shown from
    /// the start (design.md D8 point 1) — every other run stays quiet until
    /// <see cref="Reveal"/> finds it has work.</summary>
    public void BeginRun(bool visibleFromStart)
    {
        TryBegin();
        lock (_lock)
        {
            _visible = visibleFromStart;
            _retryAt = null;
        }
    }

    /// <summary>The run found work: from here it's shown, with the total it
    /// found (design.md D8 point 5).</summary>
    public void Reveal(int total)
    {
        SetTotal(total);
        lock (_lock)
            _visible = true;
    }

    /// <summary>The run found nothing missing: whatever an earlier run left
    /// shown is cleared, and the gate counts the list as gone through
    /// (design.md D8 point 4).</summary>
    public void EndWithoutWork()
    {
        base.Complete();
        lock (_lock)
        {
            _visible = false;
            _shown = new JobSnapshot(JobPhase.NotStarted, 0, null, null, null, null);
            _wentThroughSinceStart = true;
            _lastReadFailure = null;
            _retryAt = null;
        }
    }

    /// <summary>The run threw before it could read the list. Shown only when
    /// this run was visible from the start; otherwise whatever was shown
    /// before stays shown, and the gate stays closed since the list was
    /// never read (design.md D8 point 7).</summary>
    public void FailBeforeRead(string reason)
    {
        base.Fail(reason);
        lock (_lock)
        {
            if (_visible)
                _shown = base.Snapshot;
            _lastReadFailure = reason;
        }
    }

    /// <summary>Ends a run that read the whole list with every anime
    /// fetched. Hides <see cref="JobProgressTracker.Complete"/> so ending a
    /// shown run also freezes it into <see cref="Snapshot"/> for a later,
    /// possibly quiet, run to fall back on, and so the gate counts the list
    /// as gone through (design.md D8 point 6).</summary>
    public new void Complete()
    {
        base.Complete();
        lock (_lock)
        {
            if (_visible)
                _shown = base.Snapshot;
            _wentThroughSinceStart = true;
            _lastReadFailure = null;
        }
    }

    /// <summary>Ends a run that read the whole list but left some anime
    /// unfetched (design.md D8 point 6) — see <see cref="Complete"/>.</summary>
    public new void Fail(string reason)
    {
        base.Fail(reason);
        lock (_lock)
        {
            if (_visible)
                _shown = base.Snapshot;
            _wentThroughSinceStart = true;
            _lastReadFailure = reason;
        }
    }

    /// <summary>Publishes when the next automatic retry will run, so a shown
    /// failure can say when (design.md D9). Pass null when none is
    /// planned.</summary>
    public void SetRetryAt(DateTimeOffset? at)
    {
        lock (_lock)
            _retryAt = at;
    }
}
