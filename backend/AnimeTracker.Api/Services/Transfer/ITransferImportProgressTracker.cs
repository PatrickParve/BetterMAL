using AnimeTracker.Api.Services.Jobs;

namespace AnimeTracker.Api.Services.Transfer;

public enum TransferImportPhase
{
    NotStarted,
    Running,
    Complete,
    Failed,
}

/// <summary>design.md D1's status shape. <see cref="DeviceName"/> and
/// <see cref="ExportedAt"/> name the file the current or most recent run is
/// for; <see cref="Report"/> and <see cref="Error"/> are that run's outcome,
/// whichever phase it ended in. <see cref="StartedAt"/>/<see cref="FinishedAt"/>
/// and <see cref="OutcomeSeen"/> join the shared job shape (design.md
/// D4). <see cref="Dismissed"/> is set when the person closes the ended
/// run's outcome; it is kept here, beside the run, so every browser agrees
/// (simplify-settings-and-first-fetch-states D3).</summary>
public record TransferImportStatusSnapshot(
    TransferImportPhase Phase,
    int Done,
    int Total,
    string? DeviceName,
    DateTimeOffset? ExportedAt,
    TransferImportReport? Report,
    string? Error,
    DateTimeOffset? StartedAt = null,
    DateTimeOffset? FinishedAt = null,
    bool OutcomeSeen = false,
    bool Dismissed = false);

/// <summary>In-memory progress for the file import (device-transfer),
/// modelled on <c>ISeriesBulkBuildProgressTracker</c>. Not persisted — a
/// failed or abandoned import can simply be re-imported (design.md D1
/// Alternatives).</summary>
public interface ITransferImportProgressTracker
{
    TransferImportStatusSnapshot Snapshot { get; }

    /// <summary>Reports a run as in flight before the background service has
    /// woken up — called from the accepting request itself, so the response
    /// already reads as running (design.md D1). Also records the file this
    /// run is for, and clears the previous run's report and error.</summary>
    void MarkPending(string? deviceName, DateTimeOffset exportedAt);

    void Start(int total);

    /// <summary>Widens the total as more work is discovered mid-run (design.md
    /// task 6.5: picture refreshes are added to the total as they are
    /// found).</summary>
    void AddToTotal(int n);

    void ReportProgress(int done);

    /// <summary>Reports a finished run and its report. <see cref="DeviceName"/>
    /// and <see cref="ExportedAt"/> are left as <see cref="MarkPending"/> set
    /// them.</summary>
    void Complete(TransferImportReport report);

    /// <summary>Reports a run that failed outright — nothing from the file
    /// was applied (design.md D11).</summary>
    void Fail(string reason);

    /// <summary>Records that this run's ended outcome has been shown to the
    /// user, guarded by the exact time it ended — the same guard
    /// <see cref="JobProgressTracker.MarkOutcomeSeen"/> applies (design.md
    /// D4).</summary>
    void MarkOutcomeSeen(DateTimeOffset finishedAt);

    /// <summary>Closes this run's ended outcome for everyone, and counts it as
    /// seen. Guarded like <see cref="MarkOutcomeSeen"/>: a run still in flight,
    /// or a <paramref name="finishedAt"/> that is no longer the latest run's,
    /// changes nothing. <see cref="MarkPending"/> starts the next run not
    /// dismissed.</summary>
    void Dismiss(DateTimeOffset finishedAt);

    /// <summary>Maps into the shared job shape for the combined status read
    /// (design.md D15).</summary>
    JobSnapshot ToJobSnapshot();
}
