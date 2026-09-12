namespace AnimeTracker.Api.Services.Jobs;

/// <summary>The one mapping from a job's internal snapshot to what a trigger
/// response or the combined status read serves the page (design.md D1/D15).</summary>
public record JobDto(
    string Phase, int Done, int? Total, string? Error,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, DateTimeOffset? RetryAt,
    bool OutcomeSeen)
{
    public static JobDto From(JobSnapshot snapshot) => new(
        snapshot.Phase.ToString(), snapshot.Done, snapshot.Total, snapshot.Error,
        snapshot.StartedAt, snapshot.FinishedAt, snapshot.RetryAt, snapshot.OutcomeSeen);
}

/// <summary>JobDto plus which of accept-all/decline-all is running or most
/// recently ran — the pair is one job (design.md D18), and the page needs to
/// know which of its two buttons to show as busy.</summary>
public record HeldDecisionJobDto(
    string Phase, int Done, int? Total, string? Error,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt, DateTimeOffset? RetryAt,
    bool OutcomeSeen,
    string? Action)
{
    public static HeldDecisionJobDto From(HeldDecisionProgress tracker)
    {
        var snapshot = tracker.Snapshot;
        return new HeldDecisionJobDto(
            snapshot.Phase.ToString(), snapshot.Done, snapshot.Total, snapshot.Error,
            snapshot.StartedAt, snapshot.FinishedAt, snapshot.RetryAt, snapshot.OutcomeSeen, tracker.Action?.ToString());
    }
}
