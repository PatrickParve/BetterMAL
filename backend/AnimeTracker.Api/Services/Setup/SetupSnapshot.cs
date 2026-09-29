namespace AnimeTracker.Api.Services.Setup;

/// <summary>Setup's four steps, in the order the screen shows them.</summary>
public enum SetupStep
{
    List,
    Details,
    Series,
    Airing,
}

/// <summary>Where a step is: not started, running, held (its service is down, or the
/// login it needs was refused) or finished. What a step is waiting on is read from
/// which step it is: details and airing wait for the list, series for the details.</summary>
public enum SetupStepPhase
{
    Waiting,
    Running,
    Paused,
    Done,
}

/// <summary>Anime of a step waiting for their retry (design D13): how many, and when
/// the soonest one is due.</summary>
public sealed record SetupRetryWait(int Count, DateTimeOffset NextAt);

/// <summary>What only the coordinator knows about one step. Its counts are read from
/// the database, except the list step's (<see cref="SetupSnapshot.ListRead"/>).
/// <paramref name="EtaSeconds"/> is null until there is enough progress to estimate
/// from, and while the step is paused, throttled or waiting for Reconnect.</summary>
public sealed record SetupStepState(SetupStepPhase Phase, SetupRetryWait? WaitingRetry, double? EtaSeconds)
{
    public static readonly SetupStepState Waiting = new(SetupStepPhase.Waiting, null, null);
}

/// <summary>A list entry the list read left out because MyAnimeList gave it a status
/// the app doesn't recognize. No entry and no row are stored for it, so the title
/// and the raw status are kept here, for Settings to show.</summary>
public sealed record UnrecognizedStatusSkip(int AnimeId, string Title, string MalStatus);

/// <summary>A copy of everything setup holds in memory (design D1): what the status
/// read serves beside the counts it takes from the database. It never changes once
/// taken, so a reader sees one moment.</summary>
public sealed record SetupSnapshot(
    bool ListReadThisRun,
    int ListRead,
    int? ListTotal,
    bool WaitingForReconnect,
    bool AiringDraining,
    SetupStepState List,
    SetupStepState Details,
    SetupStepState Series,
    SetupStepState Airing,
    IReadOnlySet<int> NoSeriesAnimeIds,
    IReadOnlyList<UnrecognizedStatusSkip> UnrecognizedStatuses)
{
    public SetupStepState Step(SetupStep step) => step switch
    {
        SetupStep.List => List,
        SetupStep.Details => Details,
        SetupStep.Series => Series,
        SetupStep.Airing => Airing,
        _ => throw new ArgumentOutOfRangeException(nameof(step)),
    };

    /// <summary>Nothing has started: what a process that hasn't begun setup reports.</summary>
    public static readonly SetupSnapshot Idle = new(
        ListReadThisRun: false, ListRead: 0, ListTotal: null, WaitingForReconnect: false, AiringDraining: false,
        SetupStepState.Waiting, SetupStepState.Waiting, SetupStepState.Waiting, SetupStepState.Waiting,
        new HashSet<int>(), []);
}
