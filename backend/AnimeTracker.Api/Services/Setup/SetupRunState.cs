namespace AnimeTracker.Api.Services.Setup;

/// <summary>What setup's workers know in memory and the database can't say (design
/// D1): each step's phase, retry wait and ETA, how much of the list has been read,
/// the anime found to belong to no series, and the entries left out for their status.
/// The workers write it as they go; the coordinator and the status read take
/// <see cref="Snapshot"/>s. Every member is guarded by one lock, so a worker, the
/// status poll and Retry now can share it.
/// <para>Held in memory only, by decision (proposal §4): a restart forgets it, and
/// the database says where each step stands.</para></summary>
public sealed class SetupRunState
{
    private readonly object _lock = new();
    private readonly Dictionary<SetupStep, SetupStepState> _steps = Enum.GetValues<SetupStep>()
        .ToDictionary(step => step, _ => SetupStepState.Waiting);
    private readonly Dictionary<SetupStep, EtaEstimator> _etas = Enum.GetValues<SetupStep>()
        .ToDictionary(step => step, _ => new EtaEstimator());
    private readonly HashSet<int> _noSeries = [];
    private List<UnrecognizedStatusSkip> _unrecognized = [];
    private IReadOnlyDictionary<int, int> _listOrder = new Dictionary<int, int>();
    private bool _listReadThisRun;
    private int _listRead;
    private int? _listTotal;
    private bool _waitingForReconnect;
    private bool _draining;

    /// <summary>True from the moment airing work is left over after Home opened until
    /// nothing is left or waiting. The hourly airing catch-up stays out of the way
    /// meanwhile, so no anime is fetched twice at once.</summary>
    public bool IsDraining
    {
        get { lock (_lock) return _draining; }
    }

    public void SetPhase(SetupStep step, SetupStepPhase phase) => Update(step, s => s with { Phase = phase });

    public void SetWaitingRetry(SetupStep step, SetupRetryWait? wait) => Update(step, s => s with { WaitingRetry = wait });

    /// <summary>Notes that <paramref name="completed"/> more items of a step finished at
    /// <paramref name="at"/> and re-estimates the time it has left, given the
    /// <paramref name="remaining"/> items still to do (design D17). Null remaining, the list read
    /// before its total is known, keeps the pace but leaves the estimate out. The workers call this
    /// with whatever finished since their last look, so a build that settles several anime counts
    /// them all.</summary>
    public void RecordProgress(SetupStep step, int completed, int? remaining, DateTimeOffset at)
    {
        lock (_lock)
        {
            var estimator = _etas[step];
            estimator.Record(completed, at);
            _steps[step] = _steps[step] with { EtaSeconds = remaining is { } left ? estimator.EtaSeconds(left) : null };
        }
    }

    /// <summary>A step stopped making progress, or is starting over: its pace so far is forgotten
    /// and its estimate left out until it has fresh progress to estimate from. Without this, the
    /// wait would be averaged into the pace.</summary>
    public void ResetEta(SetupStep step)
    {
        lock (_lock)
        {
            _etas[step].Clear();
            _steps[step] = _steps[step] with { EtaSeconds = null };
        }
    }

    /// <summary>The list read's running count and the total, once it is known. MyAnimeList
    /// gives no total with a page (design D7), so it may stay null until the last page.</summary>
    public void SetListProgress(int read, int? total)
    {
        lock (_lock)
        {
            _listRead = read;
            _listTotal = total;
        }
    }

    /// <summary>The list has been read in this run of setup. It is a fact about the
    /// process, not the database: the list is read again at the start of every run.</summary>
    public void MarkListRead()
    {
        lock (_lock)
            _listReadThisRun = true;
    }

    public void SetWaitingForReconnect(bool waiting)
    {
        lock (_lock)
            _waitingForReconnect = waiting;
    }

    public void SetDraining(bool draining)
    {
        lock (_lock)
            _draining = draining;
    }

    /// <summary>A build found that the anime belongs to no series, so it counts as settled
    /// in the series step. After a restart the set is empty and those anime are built
    /// again. A build re-fetches an anime that has no relation rows, since it reads that as
    /// a row never fully fetched, so each such anime costs one MyAnimeList request per
    /// restart.</summary>
    public void AddNoSeries(int animeId)
    {
        lock (_lock)
            _noSeries.Add(animeId);
    }

    /// <summary>The order the list read returned the anime in (design D8), which the details
    /// step keeps within a tier and the airing step keeps as its tie-break. A restart forgets
    /// it and the next read rebuilds it; until then callers fall back on the anime id.</summary>
    public void SetListOrder(IReadOnlyList<int> animeIds)
    {
        var order = new Dictionary<int, int>(animeIds.Count);
        for (var index = 0; index < animeIds.Count; index++)
            order.TryAdd(animeIds[index], index);

        lock (_lock)
            _listOrder = order;
    }

    /// <summary>Each list anime's position in the latest read; empty before a read. The
    /// returned dictionary is never modified, so it can be read without the lock.</summary>
    public IReadOnlyDictionary<int, int> ListOrder
    {
        get { lock (_lock) return _listOrder; }
    }

    /// <summary>Replaces the entries left out for their status with the latest read's.</summary>
    public void SetUnrecognized(IEnumerable<UnrecognizedStatusSkip> skips)
    {
        lock (_lock)
            _unrecognized = skips.ToList();
    }

    public SetupSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new SetupSnapshot(
                _listReadThisRun, _listRead, _listTotal, _waitingForReconnect, _draining,
                _steps[SetupStep.List], _steps[SetupStep.Details], _steps[SetupStep.Series], _steps[SetupStep.Airing],
                _noSeries.ToHashSet(), _unrecognized.ToList());
        }
    }

    private void Update(SetupStep step, Func<SetupStepState, SetupStepState> change)
    {
        lock (_lock)
            _steps[step] = change(_steps[step]);
    }
}
