## Context

**Jobs today.** Five in-memory trackers back the Settings page's jobs, all registered as singletons in `Program.cs`:

| Tracker | Phases | Signalled by | Pending state before start? |
|---|---|---|---|
| `ResyncProgressTracker` | NotStarted/Running/Complete | `SyncController.TriggerResyncFromMal` | no |
| `AiringFullRefreshProgressTracker` | NotStarted/Running/Complete | `AiringController.TriggerFullRefresh` | no |
| `SeriesBulkBuildProgressTracker` | + Failed | `SeriesController.TriggerBulkBuild` | `MarkPending()`, but unconditional |
| `ImportProgressTracker` | NotStarted/Running/Complete | callback, startup | no |
| `TransferImportProgressTracker` | + Failed | `TransferImportService.AcceptAsync` | `MarkPending()` behind `TryOffer` |

- **Triggers.** Each trigger is a `SemaphoreSlim(0, 1)`. It merges signals only until the background service takes one, so the count drops back to 0 while the run is still in progress.
- **In-request actions.** Four actions do their work inside the request, with no progress:
  - `POST api/sync/now` → `EntryPushService.DrainPendingAsync`
  - `POST api/sync/reconcile` → `ReconciliationService.RunAsync`
  - `POST api/sync/held/accept` and `POST api/sync/held/decline` → `HeldChangeService.AcceptAllAsync` / `DeclineAllAsync`
- **Automatic callers of the same code.** `DrainPendingAsync` is also the 2-minute retry pass (`PendingSyncRetryBackgroundService`). `RunAsync` is also the weekly check (`ReconciliationBackgroundService`), which stamps `ReconciliationRunLog.LastRunAt` when it attempts a run.

**Auth today.**
- `MalOAuthService.RefreshAsync` catches every `HttpRequestException` and returns null. `EnsureSuccessStatusCode` throws one for any non-2xx answer and for no answer at all, so the two look the same.
- `MalTokenProvider` refreshes only within 10 minutes of expiry. `MalTokenRefreshBackgroundService` refreshes within 1 day of expiry, every 6 hours.
- `MalAuthPacingHandler` throws `MalAuthorizationRequiredException` when the provider returns null, and passes a `401` straight back to the caller.
- The token endpoint is called through the plain `IHttpClientFactory` client, not the paced MAL client.

**Constraints:**
- Backend tests use the EF in-memory provider, which doesn't support `ExecuteUpdateAsync`.
- The backend builds and tests only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`.
- The frontend has no test runner, builds with nvm's Node 22, and is checked by hand in the running app.
- `Program.cs` runs migrations at startup, before the hosted services start.

## Goals / Non-Goals

**Goals:**
- Every job starts on the first press, never runs twice at once, and always ends as Complete or Failed.
- Sync now, reconciliation, Accept all and Decline all run in the background and report progress, including while their total is unknown.
- The weekly check and the push retry never show up as a job. The weekly check's outcome is recorded.
- The MAL list import is shown only when it has work, retries on its own, and keeps the file import's gate working.
- MAL refusing the login is recorded as a lost connection, distinct from an outage, and cleared by re-authorizing.
- One cheap read serves every job's state and the connection state, ready for change 10.

**Non-Goals:**
- Anything in the navbar (change 10).
- Persisting job progress across restarts.
- Moving the trigger-based jobs (re-sync, airing refresh, series build, list import) onto the new runner (D4).
- Cancelling a running job, or a Retry button for the list import.
- Changing the weekly cadence, the 2-minute retry cadence, or the weekly check's own retry after a failure (still a week).
- Changing what any job does, except that the list import now leaves pending removals alone (D8).

## Decisions

### D1. One tracker type, one singleton per job

A new `Services/Jobs/` holds:

```csharp
public enum JobPhase { NotStarted, Running, Complete, Failed }

public record JobSnapshot(
    JobPhase Phase, int Done, int? Total, string? Error,
    DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    DateTimeOffset? RetryAt = null);

public interface IJobProgressSink { void SetTotal(int total); void ReportProgress(int done); }

public class JobProgressTracker : IJobProgressSink
{
    public JobSnapshot Snapshot { get; }
    public bool TryBegin();            // D2
    public void SetTotal(int total);
    public void AddToTotal(int n);
    public void ReportProgress(int done);
    public void Complete();
    public void Fail(string reason);
}
```

- **`Total` is `int?`.** Null means "not known yet". It never means zero: a run with nothing to do is `Complete` with `Total = 0`.
- **One subclass per job.** Each job gets an empty sealed subclass, so DI and constructors stay typed: `ResyncProgress`, `AiringFullRefreshProgress`, `SeriesBulkBuildProgress`, `SyncNowProgress`, `ReconcileProgress`, and `HeldDecisionProgress`, which adds the `Action` it was started with. `ListImportProgress` adds the quiet mode and the gate state (D8).
- **What's removed.** The old `I*ProgressTracker` interfaces, their classes and their phase enums go.
- **What's kept.** `TransferImportProgressTracker` stays as it is, because it carries the file's device name, export time and report. It gains `ToJobSnapshot()` for the combined read (D15), and its `TryOffer` slot already starts it only once.
- **Why a sink interface.** The services take an `IJobProgressSink?`, not a tracker. A service never knows which job it reports into, and passing null makes a run quiet (D6).

*Alternatives:*
- **Keep five interfaces and add Failed and a guard to each.** Rejected: that's five copies of one state machine, which is how three of them came to lack Failed.
- **Keyed singletons (`AddKeyedSingleton<JobProgressTracker>("resync")`).** Rejected: string keys at every injection site, with no compile-time check.

### D2. The tracker is the start gate

`TryBegin()` checks and sets under the tracker's lock:
- If the phase is Running, it returns false.
- Otherwise it moves to `Running`, with `Done = 0`, `Total = null`, `Error = null`, `StartedAt = now` and `FinishedAt = null`, and returns true.

Controllers become:

```csharp
if (progress.TryBegin()) trigger.Signal();
return Accepted(JobDto.From(progress.Snapshot));
```

- **One signal at a time.** A signal is sent only on the move into Running, and every run ends by leaving Running (D3). So at most one signal is ever outstanding, whether a run is waiting to start or already under way. The semaphore stays, as the wake-up.
- **The response.** It is always the job's snapshot, which says Running whether this request started the run or found it already going.
- **Coverage.** This fixes the double press and the queued second run for the re-sync and the airing refresh. It fixes the series build's unconditional `MarkPending()` too. The four actions in D4 go through the same `TryBegin` inside the runner.

*Alternative:* a busy flag inside each trigger, like `TransferImportTrigger`. Rejected: it duplicates the tracker's own Running state, and the two could disagree.

### D3. Every run ends, and says why it failed

- **Background services.** Each `catch (Exception ex)` in the three trigger-based background services now calls `progress.Fail(JobFailure.Describe(ex, service))`. The airing service's catch previously only logged.
- **Services.** `ResyncService` and the airing refresh's run replace `Start(total)` with `SetTotal(total)`. So progress no longer restarts from zero, and the snapshot `TryBegin` set keeps its `StartedAt`.
- **Wording.** `JobFailure.Describe` maps an exception to a sentence for the page:

| Exception | Reason |
|---|---|
| `MalAuthorizationRequiredException` | "The connection to MyAnimeList was lost." |
| `HttpRequestException`, or `TaskCanceledException` not from our own token (a timeout) | "{MyAnimeList \| AniList} couldn't be reached." |
| anything else | "Something went wrong — see the backend logs." |

- **Partial runs.** A run that finishes its loop with items left over ends with `Fail("{n} of {total} …")`, with wording per job. It does not call `Complete()`.
- **Shutdown.** An `OperationCanceledException` from the host's stopping token still just breaks the loop. The process is ending, and job state is in memory.

### D4. A background runner for the four in-request actions

`BackgroundJobRunner` is a singleton that is also registered as a hosted service:

```csharp
public bool TryStart(JobProgressTracker tracker,
    Func<IServiceProvider, IJobProgressSink, CancellationToken, Task> work);
```

- **Starting.** It calls `tracker.TryBegin()` and returns false if the job is already running. Otherwise it starts `work` on `Task.Run`, in its own DI scope, on the runner's stopping token (never the request's), and returns true.
- **Ending.** An exception calls `tracker.Fail(JobFailure.Describe(...))`. If `work` returns and the tracker is still Running, the runner calls `Complete()`, so a job can't be left Running by an oversight.
- **Shutdown.** In-flight tasks are kept in a set. `StopAsync` cancels them and awaits them, within the host's shutdown timeout.

Controllers call `runner.TryStart(...)` and answer `202` with the job DTO either way:
- `POST api/sync/now` → `pushService.DrainPendingAsync(sink, ct)`
- `POST api/sync/reconcile` → `reconciliationService.RunAsync(sink, ct)`
- `POST api/sync/held/accept` and `/decline` → `heldChangeService.AcceptAllAsync(sink, ct)` / `DeclineAllAsync(sink, ct)`, with `HeldDecisionProgress.Action` set before the start

*Alternative:* a trigger and a background service per action, the existing idiom. Rejected: that's four more pairs of identical wait-run-log loops, for work that has no startup or scheduled behaviour. The existing trigger-based jobs keep their shape (Non-Goals), and moving them over later needs only this runner.

### D5. Where progress comes from

- **Reading the list.** `IMalClient.GetFullUserAnimeListAsync(Action<int>? onPageRead = null, CancellationToken ct = default)`. After each page, `GetAllPagesAsync` calls the callback with the number of entries read so far. MAL's list endpoint has no total.
- **Reconciliation.** The total stays null for the whole run, and `ReportProgress(entriesRead)` is called per page. The comparison and the save make no MAL calls, so they aren't a separate stage, and the run ends with `Done` equal to the anime read.
  - *Rejected:* using the local list's size as the total. MAL's list and mine differ exactly when reconciliation matters, so that total would be a guess that looks like a fact.
- **Re-sync, series build, airing refresh.** They stay "Starting…" (total null) while they resolve their targets, then call `SetTotal`. Their list reads aren't counted.
- **Sync now.** `DrainPendingAsync(IJobProgressSink? progress = null, ct)` loads the pending entry ids and pending removal ids that aren't held before its first push, then calls `SetTotal(entries + removals)` and reports per item. It now returns `DrainResult(Pushed, NotPushed)`. The retry pass reads `Pushed` for its log line.
- **Held decisions.** `AcceptAllAsync` and `DeclineAllAsync(IJobProgressSink? progress = null, ct)` call `SetTotal(heldIds.Count)` and report per item. They already return `HeldChangeBulkResult(Succeeded, StillHeld)`.
- **Endings.** The work lambdas in D4 turn these results into endings:
  - Sync now: `NotPushed > 0` → `Fail("{NotPushed} of {total} couldn't be sent; they'll be retried automatically.")`
  - Accept all / Decline all: `StillHeld > 0` → `Fail("{StillHeld} of {total} couldn't be applied and are still held.")`
  - otherwise `Complete()`

### D6. Automatic runs pass no sink, and reconciliation runs one at a time

- **Quiet runs.** `ReconciliationService.RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default)`. The weekly service calls it with no sink, and the retry pass calls `DrainPendingAsync` with none. Neither can touch a job's state, because no job tracker is in reach. That is the whole of "told apart": it is decided by the caller, and nothing inspects which caller is running.
- **One run at a time.** A new singleton, `ReconciliationRunGate`, wraps a `SemaphoreSlim(1, 1)`. `RunAsync` awaits it before reading MAL and releases it in `finally`, so a manual run and the weekly run can't both delete and add diffs at once. A manual run that is waiting shows "Starting…". The weekly run is short (a few paged reads), so the wait is seconds.

*Alternative:* have the manual press reuse an in-flight weekly run's result. Rejected: the weekly run reports nowhere, so the manual job would have nothing to show until it ended.

### D7. The weekly check's outcome is persisted on `ReconciliationRunLog`

- **New columns.** `bool? LastRunFailed` and `string? LastRunError`.
- **At the attempt**, next to the existing `LastRunAt` stamp: `LastRunFailed = null` and `LastRunError = null`.
- **At the end:** `false` on success. On failure, `true` plus `JobFailure.Describe(ex, MyAnimeList)`, saved in a fresh scope (the run's own `DbContext` may be in a failed state).
- **What null means:** the run didn't record an ending, because it was interrupted or predates this change. The page then shows only the time, never a guess.
- **Why persisted:** at a weekly cadence, an in-memory record would be empty after most restarts.
- **Cadence:** unchanged. It still waits a full week from the attempt.

### D8. The list import: what counts as work, and when it shows

`InitialImportService.RunAsync`:

1. `progress.BeginRun(visibleFromStart: !await db.UserAnimeEntries.AnyAsync())`. Only an empty list is shown from the start.
2. Read the list, with the Bearer token.
3. **Work** is every edge whose anime has no `AnimeMetadata` row (a full fetch) or no `UserAnimeEntry` (entry only), minus the ids in `PendingEntryDeletions`. That exclusion fixes the re-added removal and matches `ReconciliationService`.
4. If there is no work, call `progress.EndWithoutWork()`: the shown state returns to NotStarted, and the gate counts the list as gone through.
5. Otherwise call `progress.Reveal(work.Count)`, then import each item and report `Done`. A per-item failure is counted and logged, and the anime stays missing so a later run retries it, as today.
6. At the end:
   - no failures → `Complete()`
   - failures → `Fail("{n} of {total} anime couldn't be fetched.")`

   Either way the gate counts the list as gone through.
7. An exception before step 3 calls `progress.FailBeforeRead(reason)`. That is shown only when the run was visible from the start, and otherwise leaves the previously shown state alone. The gate stays closed.

**What readers see.** `ListImportProgress` keeps the last shown snapshot apart from the current run. While a run is quiet, `Snapshot` returns the last shown one. `Gate` returns `(bool Running, bool WentThroughSinceStart, string? LastReadFailure, DateTimeOffset? RetryAt)`, for the file import's refusal (D10).

*Alternative:* count the whole MAL list as the total and skip the anime already stored, as today. Rejected: a run that fetches 3 anime would show 3 of 412, and a run with no work would still show a full bar on every start.

### D9. Retries live in `InitialImportBackgroundService`

- **Waiting.** The loop waits on the trigger, cancelled after a delay when a retry is planned: a linked `CancellationTokenSource` with `CancelAfter`. A cancellation that isn't the host stopping means "retry now".
- **The schedule.** `[1, 5, 15, 60, 60]` minutes. It advances after each run that couldn't finish: `FailBeforeRead`, or ended with fetch failures. It resets when a run finishes with nothing missing, and when a trigger signal arrives (re-authorizing). After the fifth retry nothing more is planned, and `RetryAt` is null.
- **Showing it.** `progress.SetRetryAt(at)` puts the time on the shown snapshot, so a shown failure says when the next try is.
- **Lost connection.** Before each run, the service reads the token row. With no token, or with `ConnectionLostAt` set, it neither runs nor plans a retry. A run that failed with `MalAuthorizationRequiredException` plans no retry either. The startup signal is sent only when a token exists and isn't lost.

*Alternative:* retry only at the next start. Rejected: the stack often starts at boot, before the network is up, and the next start may be a day away.

### D10. The file import's gate reads `ListImportProgress.Gate`

`TransferImportService.AcceptAsync` step 3 becomes:

| Gate | Refusal |
|---|---|
| running and shown | "The list is still being imported ({done} / {total})." (unchanged wording) |
| running and quiet | "The list is being checked against MyAnimeList." |
| connection lost (token row) | "The list can't be checked while the connection to MyAnimeList is lost. Re-authorize in Settings." |
| not gone through, last read failed | "The list couldn't be read from MyAnimeList. It will try again at {time}." or "…when the app next starts." |
| no run since start | "The list hasn't finished importing since the app started." (unchanged) |

A run that went through the list opens the gate, even one with anime it couldn't fetch. The file import's own report then lists choices for those anime as failures, which is what they are.

### D11. Refusal is told from outage by what the token endpoint answers

`RefreshAsync` returns a result in place of `OAuthToken?`:

```csharp
public abstract record MalRefreshResult
{
    public sealed record Refreshed(OAuthToken Token) : MalRefreshResult;
    public sealed record Refused(int StatusCode, string? Error) : MalRefreshResult;
    public sealed record Unavailable(string Detail) : MalRefreshResult;
}
```

- **Refused.** The token endpoint answered `400` or `401`. The `error` field is read from the JSON body on a best-effort basis, for the log. `tokenStore.MarkConnectionLostAsync(now)` is called, and a warning is logged.
- **Unavailable:**
  - an `HttpRequestException` with no response;
  - a `TaskCanceledException` when `ct` wasn't cancelled (a timeout);
  - any other non-success status (`5xx`, `429`, `403`).

  Nothing is recorded, and a warning is logged.
- **Refreshed.** It is saved as today.
- **The callback is unchanged.** `HandleCallbackAsync` keeps throwing on any failure, since it's interactive and the callback page reports it.

**Why 400/401.** RFC 6749 §5.2 defines the token endpoint's error responses:
- `400` for `invalid_grant`, which covers an expired or revoked refresh token, and for `invalid_request`;
- `401` for `invalid_client`.

Any other outcome is not the endpoint refusing this login.

*Alternatives:*
- **Match `error == "invalid_grant"` only.** Rejected: MAL doesn't document its error bodies, and `invalid_client` equally means the app can't sign in until someone acts.
- **Count consecutive failures.** Rejected: a long enough outage would eventually count as a refusal, which the request rules out.

### D12. A 401 on a signed-in call gets one forced refresh

In `MalAuthPacingHandler`, when a Bearer request is answered `401` and hasn't been retried for auth:
- It calls `tokenProvider.RefreshAfterRejectionAsync(rejectedAccessToken, ct)`. Under the existing `_refreshLock`, that:
  - re-reads the token row;
  - returns `(null, Lost: true)` if the row is already lost;
  - returns the stored token if its access token differs from the rejected one, because a concurrent caller already refreshed;
  - otherwise calls `RefreshAsync`.
- **Refreshed:** the handler disposes the 401 response, clones the request, and sends it again once, paced.
- **Refused:** the connection is now recorded as lost. The handler disposes the response and throws `MalAuthorizationRequiredException`.
- **Unavailable:** it returns the original 401 response, and the caller fails as today.
- **A second 401 on the retried request** is returned as it is.

The existing 403 backoff loop is unchanged. The auth retry is a separate, single flag.

### D13. The lost state is stored on the token row

- **The column.** `OAuthToken.ConnectionLostAt` (`DateTimeOffset?`).
- **Recording.** `IMalTokenStore.MarkConnectionLostAsync(DateTimeOffset at, ct)` sets it only if it's null, so the first time noticed is kept.
- **Clearing.** `SaveAsync`, which runs on every successful exchange including the authorization callback, sets it to null.
- **Readers:**
  - `MalTokenProvider.GetValidAccessTokenAsync` returns null when it's set, without a request or a refresh. So the handler throws `MalAuthorizationRequiredException` and nothing is sent.
  - `MalTokenRefreshBackgroundService` skips when it's set.
  - `InitialImportBackgroundService` doesn't signal or retry when it's set (D9).

*Alternative:* an in-memory flag. Rejected: after a restart the state would read Connected until the next refresh attempt. For a revoked token still inside its expiry window, that could be weeks, and it would lose the time the loss was noticed.

### D14. `GET api/mal-auth/status` reports three states

It returns `{ state: "Connected" | "Lost" | "NotConnected", lostAt: string | null }`, computed from the token row:
- no row → `NotConnected`;
- `ConnectionLostAt` set → `Lost`;
- otherwise `Connected`.

`App.tsx` shows the connect screen only for `NotConnected`, so a lost connection keeps the app open. Settings explains it.

### D15. `GET api/app-status`, one read of everything

The new `AppStatusController` returns:

```json
{
  "malConnection": { "state": "Lost", "lostAt": "2026-09-11T08:02:00Z" },
  "weeklyCheck": { "lastRunAt": "2026-09-08T03:12:00Z", "failed": false, "error": null },
  "jobs": {
    "listImport":    { "phase": "Failed", "done": 9, "total": 12, "error": "3 of 12 anime couldn't be fetched.", "startedAt": "…", "finishedAt": "…", "retryAt": "…" },
    "syncNow":       { "phase": "NotStarted", "done": 0, "total": null, "error": null, "startedAt": null, "finishedAt": null, "retryAt": null },
    "reconcile":     { … },
    "heldDecision":  { …, "action": "Accept" },
    "resync":        { … },
    "airingRefresh": { … },
    "seriesBuild":   { … },
    "fileImport":    { … }
  }
}
```

- **What it reads:** every tracker's snapshot (memory), the token row, and the run-log row. Those are two single-row reads by primary key, with no call to MAL or AniList.
- **`weeklyCheck`** is null when no row exists.
- **`listImport`** is the shown snapshot, so a quiet run appears as `NotStarted`.
- **`JobDto.From(JobSnapshot)`** is the single mapping. Every trigger `POST` returns one `JobDto`.
- **Removed:**
  - `GET api/sync/resync-from-mal/status`
  - `GET api/airing/refresh-all/status`
  - `GET api/series/build-all/status`
  - `ImportController` (`GET api/import/status`), which nothing reads
- **Kept:** `GET api/transfer/import/status` still serves the import's report.

*Alternative:* keep the per-job reads and have the page poll each. Rejected: that's eight requests a second while anything runs, and change 10 needs one consistent picture rather than eight reads taken at different moments.

### D16. The page polls one hook and reacts to endings

`hooks/useAppStatus.ts`:
- **`status`** is the latest `AppStatusDto`.
- **Polling.** It polls `getAppStatus()` every 1 s while any job is Running, and every 10 s otherwise. It polls only while `document.visibilityState === 'visible'`, and re-reads at once on `focus` and `visibilitychange`.
- **`applyJob(key, dto)`** merges a trigger's response at once. The first press therefore shows the bar without waiting for a poll, and the fast poll starts because a job is now Running.
- **`refresh()`** reads again.

`SettingsPage` keeps each job's previous phase in a ref. When a job leaves Running, it reloads what that job changes:

| Job that ended | Reloads |
|---|---|
| `syncNow` | `getSyncStatus` |
| `reconcile` | `getPendingReconciliationDiff` |
| `heldDecision` | `getHeldChanges`, `getSyncStatus` |
| `listImport` | `getSyncStatus` |
| `fileImport` | `getTransferImportStatus` (the report) |

The four old per-job polling effects, and the local flags such as `startingFullResync`, `reconciling` and `resyncing`, are replaced by the jobs' phases. A button that is waiting on its trigger's `POST` stays disabled for that moment through one `starting` flag per job.

Change 10 can lift this hook into a context without changing its shape.

### D17. The shared bar moves without a total

`JobProgress` takes `total: number | null`:
- **Indeterminate** when `total === null` and the phase is Running.
  - **The fill:** it gets `job-progress__fill--indeterminate`, a 30%-wide segment that slides across the track with a CSS `@keyframes`. Under `prefers-reduced-motion: reduce` it is instead a static full-width fill at lower opacity.
  - **ARIA:** `aria-valuenow` and `aria-valuemax` are left off, as ARIA specifies for an indeterminate progressbar, and `aria-valuetext` carries the words.
- **The words:**

| State | Text |
|---|---|
| total unknown, nothing counted | "Starting…" |
| total unknown, some counted | "Running… {done} {noun}" (e.g. "412 anime read") |
| total known | "Running… {done}/{total} {noun}" (as today) |
| failed | "Failed after {done}/{total} {noun} — {error}". With no total: "Failed — {error}". |
| retry planned | "Tries again at {time}." appended after a failure, or "Tries again when the app next starts." when a list import failure plans none |

- **Reasons.** `failureReason` now always comes from the job's `error`. The "see backend logs" fallback is the backend's own sentence (D3).

### D18. Held decisions share one job, and singles wait

- **One job for both.** `HeldDecisionProgress.Action` (`Accept` or `Decline`) is set just before `TryStart`, under the same lock, as `TryBegin(action)`. The two bulk endpoints share it, so whichever is pressed second gets the running job's DTO back and starts nothing.
- **Singles are refused meanwhile.** `POST api/sync/held/{id}/accept` and `/decline` answer `409 { error: "Held changes are being decided; try again when that finishes." }` while it's Running.
- **On the page:**
  - every Accept and Decline in the held section is disabled while the phase is Running;
  - the section shows the job's `JobProgress`, with the noun "decided";
  - the old `heldActingId === 'all'` state goes, and `heldActingId` remains for single rows only.

### D19. Settings page copy

- **The MyAnimeList list import block,** in the Sync group above the readout, rendered only when `listImport.phase !== 'NotStarted'`:
  - title "MyAnimeList list import";
  - hint "Brings in anime on your MyAnimeList list that this device doesn't have yet — runs when the app starts and after you re-authorize.";
  - a `JobProgress` with the noun "anime" and no button.
- **The weekly check** is a new readout row, "Weekly check", after "Last successful sync":
  - `{time}` when the outcome is null;
  - `{time} — no problems`;
  - `{time} — failed: {error}`;
  - "Not run yet" when there is no row.
- **The Account section:**

| State | Text |
|---|---|
| Connected | "Connected." |
| Not connected | "Not connected." |
| Lost | "The connection to MyAnimeList was lost on {time} — MyAnimeList stopped accepting this app's login. Your changes are kept here but aren't being sent to MyAnimeList, and anime added on MyAnimeList elsewhere aren't being brought in. Re-authorize to reconnect." |

  The Lost text uses the `settings-box__error` style. The "Re-authorize with MAL" link is unchanged.
- **The new bars.** Sync now and Run full reconciliation show a `JobProgress` in their `state` slot:
  - Sync now: the noun "sent".
  - Run full reconciliation: the noun "anime read", and the button reads "Reconciling…" while it runs.

## Risks / Trade-offs

- **[MAL answers 400/401 for something a re-authorization can't fix, such as a wrong client secret]** → It is recorded as lost all the same. The app can't sign in either way, and the log holds MAL's `error` value for diagnosis.
- **[A one-off 400 from MAL marks a healthy login as lost]** → Re-authorizing is one click and clears it. That was preferred over ever counting an outage as a refusal (D11).
- **[While lost, the 2-minute retry pass fails every pending push]** → Each failure is a local `MalAuthorizationRequiredException`, with no request sent. The warning log lines are the only cost, and the edits stay pending as the spec requires.
- **[List import retries repeat a permanently failing fetch]** → That's at most 5 retries per start. Each re-reads the list (a few paged requests) and fetches only the missing anime.
- **[A quiet list read that fails on a device with entries is invisible]** → It retries on its own. The file import's refusal says the list couldn't be read, and a lost connection is explained in Account.
- **[A manual reconciliation waits behind the weekly run]** → That's seconds, and it shows "Starting…" meanwhile.
- **[Polling every second]** → Only while a job runs and the tab is visible. Each read is in-memory plus two primary-key reads.
- **[Fire-and-forget tasks in the runner]** → They are tracked, cancelled and awaited on shutdown (D4). The runner, not the work, guarantees an ending.
- **[Removing per-job endpoints breaks an old frontend]** → The backend and frontend are deployed together (Migration Plan). The frontend is the only client.
- **[Skipping pending removals changes the import]** → It affects only anime whose removal hasn't reached MAL, and it matches what reconciliation already does.

## Migration Plan

1. **One migration, `ReportJobsAndLostMalConnection`**, generated in the `sdk:10.0` image. It adds:
   - `OAuthTokens.ConnectionLostAt` (`timestamptz NULL`);
   - `ReconciliationRunLogs.LastRunFailed` (`boolean NULL`);
   - `ReconciliationRunLogs.LastRunError` (`text NULL`).

   Existing rows keep nulls, which read as Connected and as "outcome not recorded".
2. **Deploy backend and frontend together** (`docker compose build backend frontend && docker compose up -d`). The auth status shape and the removed status endpoints make the old and new halves incompatible.
3. **Rollback.** `Down` drops the three columns. Roll back both halves together.

## Open Questions

None blocking:
- **Change 10 and "waiting on me".** Change 10 may also want the held count, and whether a reconciliation diff is waiting, in the same read. Both are cheap counts and could be added to `GET api/app-status` then. They're left out here because the request scopes that read to job and connection state.
