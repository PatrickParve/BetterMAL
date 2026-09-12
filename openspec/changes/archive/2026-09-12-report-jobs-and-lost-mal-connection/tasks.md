## 1. Backend: the shared job tracker and runner

- [x] 1.1 Create `Services/Jobs/` with `JobPhase`, `JobSnapshot` (with `int? Total` and `RetryAt`), `IJobProgressSink` and `JobProgressTracker` (design D1). Every method takes the tracker's lock. `TryBegin()` returns false while Running, and otherwise resets to Running with `Total = null` and stamps `StartedAt` (D2). `Complete()` and `Fail(reason)` stamp `FinishedAt`.
- [x] 1.2 Add one empty sealed subclass per job:
  - `ResyncProgress`
  - `AiringFullRefreshProgress`
  - `SeriesBulkBuildProgress`
  - `SyncNowProgress`
  - `ReconcileProgress`
  - `HeldDecisionProgress`, which adds `Action` and `TryBegin(action)`, setting both under the lock (D18)

  Register each as a singleton in `Program.cs`.
- [x] 1.3 Add `JobFailure.Describe(Exception ex, string service)` with the three wordings in design D3. A `TaskCanceledException` counts as a timeout only when it isn't from the caller's token.
- [x] 1.4 Add `BackgroundJobRunner` (D4) and register it as a singleton and as a hosted service (`AddHostedService(sp => sp.GetRequiredService<BackgroundJobRunner>())`):
  - `TryStart(tracker, work)` runs `work` in a new scope, on the runner's stopping token.
  - An exception calls `Fail(Describe(...))`.
  - If `work` returns with the tracker still Running, the runner calls `Complete()`.
  - In-flight tasks are tracked, and cancelled and awaited in `StopAsync`.
- [x] 1.5 Add `JobDto.From(JobSnapshot)`:
  - `phase` as a string, `done`, `total` (nullable), `error`, `startedAt`, `finishedAt`, `retryAt`
  - `HeldDecisionProgress` adds `action`

  Add `TransferImportProgressTracker.ToJobSnapshot()` (D15).

## 2. Backend: the existing trigger-based jobs start once and always end

- [x] 2.1 **Re-sync:** replace `IResyncProgressTracker`/`ResyncProgressTracker`/`ResyncPhase` with `ResyncProgress`. `ResyncService` calls `SetTotal(edges.Count)` instead of `Start`. `ResyncBackgroundService`'s catch calls `progress.Fail(JobFailure.Describe(ex, "MyAnimeList"))` (D3).
- [x] 2.2 **Airing:** replace `IAiringFullRefreshProgressTracker` and its class and enum with `AiringFullRefreshProgress`. `AiringFullRefreshBackgroundService` calls `SetTotal`, and its catch calls `Fail(Describe(ex, "AniList"))`.
- [x] 2.3 **Series build:** replace `ISeriesBulkBuildProgressTracker` and its class and enum with `SeriesBulkBuildProgress`. Remove `MarkPending()`. `SeriesBulkBuildBackgroundService` calls `SetTotal`, and its existing `Fail()` call becomes `Fail(Describe(ex, "MyAnimeList"))`.
- [x] 2.4 In `SyncController.TriggerResyncFromMal`, `AiringController.TriggerFullRefresh` and `SeriesController.TriggerBulkBuild`, signal only when `TryBegin()` returns true, and always return `Accepted(JobDto.From(snapshot))` (D2).
- [x] 2.5 Remove the three status reads (D15):
  - `GET api/sync/resync-from-mal/status`
  - `GET api/airing/refresh-all/status`
  - `GET api/series/build-all/status`

  Also remove their `ToDto` helpers.

## 3. Backend: sync now, reconciliation and held decisions move to the runner

- [x] 3.1 Change `IMalClient`/`MalClient.GetFullUserAnimeListAsync` to `(Action<int>? onPageRead = null, CancellationToken ct = default)`. `GetAllPagesAsync` calls it after each page with the running entry count (D5). Update every caller and test fake.
- [x] 3.2 Change `IEntryPushService.DrainPendingAsync` to `(IJobProgressSink? progress = null, CancellationToken ct = default)`:
  - load both id lists before the first push, then `SetTotal(entries + removals)`
  - report after each item
  - return `DrainResult(Pushed, NotPushed)`

  Update `PendingSyncRetryBackgroundService` to pass no sink and log `Pushed` (D5, D6).
- [x] 3.3 Add a singleton `ReconciliationRunGate` wrapping `SemaphoreSlim(1, 1)`. `ReconciliationService.RunAsync(IJobProgressSink? progress = null, CancellationToken ct = default)`:
  - awaits the gate before reading MAL, and releases it in `finally`
  - passes `done => progress?.ReportProgress(done)` as `onPageRead`
  - never calls `SetTotal` (D5, D6)
- [x] 3.4 Change `IHeldChangeService.AcceptAllAsync` and `DeclineAllAsync` to take `IJobProgressSink? progress = null`. Each calls `SetTotal(ids.Count)` and reports after each item; the per-item rules are unchanged (D5).
- [x] 3.5 In `SyncController`, `POST api/sync/now`, `api/sync/reconcile`, `api/sync/held/accept` and `api/sync/held/decline` call `runner.TryStart(...)` and return `Accepted(JobDto)`. Their work lambdas end the job:
  - `NotPushed > 0` → `Fail("{n} of {total} couldn't be sent; they'll be retried automatically.")`
  - `StillHeld > 0` → `Fail("{n} of {total} couldn't be applied and are still held.")`
  - otherwise `Complete()`

  The held pair uses `HeldDecisionProgress.TryBegin(action)` (D4, D5, D18).
- [x] 3.6 `POST api/sync/held/{animeId}/accept` and `/decline` return `409 { error }` while `HeldDecisionProgress` is Running (D18).

## 4. Backend: the weekly check's outcome

- [x] 4.1 Add `bool? LastRunFailed` and `string? LastRunError` to `Models/ReconciliationRunLog.cs`, with a comment that null means the run recorded no ending (D7).
- [x] 4.2 In `ReconciliationBackgroundService`:
  - reset both fields to null where `LastRunAt` is stamped
  - after `RunAsync` succeeds, set `LastRunFailed = false`
  - on failure, save `true` and `JobFailure.Describe(ex, "MyAnimeList")` from a fresh scope

  It passes no sink (D6, D7).

## 5. Backend: the MAL connection

- [x] 5.1 Add `DateTimeOffset? ConnectionLostAt` to `Models/OAuthToken.cs`. In the token store:
  - add `IMalTokenStore.MarkConnectionLostAsync(DateTimeOffset at, ct)`, which sets it only when it's null
  - make `MalTokenStore.SaveAsync` clear it (D13)
- [x] 5.2 Add `MalRefreshResult` (`Refreshed`/`Refused`/`Unavailable`) and change `IMalOAuthService.RefreshAsync` to return it (D11):
  - split the send from `EnsureSuccessStatusCode`
  - a `400`/`401` answer: read `error` from the body on a best-effort basis, mark the connection lost, and return `Refused`
  - no response, a timeout, or another status: return `Unavailable`
  - success: save and return `Refreshed`

  `HandleCallbackAsync` still throws on any failure.
- [x] 5.3 In `MalTokenProvider` (D12, D13):
  - `GetValidAccessTokenAsync` returns null, without a refresh, when `ConnectionLostAt` is set
  - add `RefreshAfterRejectionAsync(string rejectedAccessToken, ct)`. Under `_refreshLock` it re-reads the row and returns one of:
    - `(null, Lost: true)` if the row is lost
    - the stored token if its access token differs from the rejected one
    - otherwise the outcome of `RefreshAsync`
- [x] 5.4 In `MalAuthPacingHandler`, handle a `401` on a Bearer request once, using a separate flag from the 403 loop (D12):
  - **refreshed:** dispose the response, clone the request and send it again
  - **refused:** throw `MalAuthorizationRequiredException`
  - **unavailable:** return the original response
  - a 401 on the retried request is returned as it is
- [x] 5.5 `MalTokenRefreshBackgroundService` skips when the token row is lost, and logs according to the `MalRefreshResult`.
- [x] 5.6 `GET api/mal-auth/status` returns `{ state: "Connected" | "Lost" | "NotConnected", lostAt }` (D14).

## 6. Backend: the MAL list import

- [x] 6.1 Replace `IImportProgressTracker`/`ImportProgressTracker`/`ImportPhase` with `ListImportProgress : JobProgressTracker` (D8):
  - `BeginRun(bool visibleFromStart)`, `Reveal(int total)`, `EndWithoutWork()`, `FailBeforeRead(string reason)`, `SetRetryAt(DateTimeOffset?)`
  - `Snapshot` returns the last shown snapshot while the current run is quiet
  - `Gate` returns `(Running, WentThroughSinceStart, LastReadFailure, RetryAt)`
- [x] 6.2 Rewrite `InitialImportService.RunAsync` to steps 1-7 of design D8:
  - `visibleFromStart` when the list has no entries
  - the work set excludes `PendingEntryDeletions` ids
  - `EndWithoutWork()` when the set is empty
  - count per-item failures, and end with `Complete()` or `Fail("{n} of {total} anime couldn't be fetched.")`
  - on an exception before the work set is computed, call `FailBeforeRead(...)` and rethrow
- [x] 6.3 In `InitialImportBackgroundService` (D9):
  - wait on the trigger with a `CancelAfter` retry delay from an injectable schedule (default `[1, 5, 15, 60, 60]` minutes)
  - advance the schedule after a run that couldn't finish, and reset it after a clean run or a trigger signal
  - publish the next time through `SetRetryAt`
  - skip the run, and plan nothing, when the token row is missing or lost
  - plan no retry after a `MalAuthorizationRequiredException`
  - send the startup signal only when a token exists and isn't lost
- [x] 6.4 Rewrite `TransferImportService.AcceptAsync` step 3 to read `ListImportProgress.Gate` and the token row, with the five refusals in design D10.
- [x] 6.5 Delete `Controllers/ImportController.cs` and the old import tracker files. Update `Program.cs` registrations.

## 7. Backend: the combined read and the migration

- [x] 7.1 Add `AppStatusController` with `GET api/app-status`, returning `malConnection`, `weeklyCheck` and `jobs` as design D15 lays out. It reads the tracker snapshots, the token row and the run-log row (`AsNoTracking`), and calls nothing outside the app.
- [x] 7.2 Generate the `ReportJobsAndLostMalConnection` migration with `dotnet ef migrations add` in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`, with the Designer file and the snapshot update. Check that it:
  - adds `OAuthTokens.ConnectionLostAt` (`timestamptz NULL`), `ReconciliationRunLogs.LastRunFailed` (`boolean NULL`) and `LastRunError` (`text NULL`)
  - has a `Down` that only drops the three columns

## 8. Backend tests

- [x] 8.1 `JobProgressTrackerTests`:
  - a second `TryBegin` while Running returns false and leaves the snapshot alone
  - `TryBegin` after Complete, and after Failed, returns true and resets `Done`, `Total` and `Error`
  - `Fail` keeps `Done` and stamps `FinishedAt`
  - `Total` is null after `TryBegin`
- [x] 8.2 `BackgroundJobRunnerTests`:
  - work that throws `HttpRequestException` ends Failed with the "couldn't be reached" reason
  - work that returns without ending the tracker ends Complete
  - `TryStart` while Running returns false and never invokes the work
- [x] 8.3 Controller tests:
  - `SeriesControllerBulkBuildTests` updated: two presses give Running both times, and exactly one signal is observable (the trigger's `WaitAsync` completes once)
  - add the same pair for the re-sync and airing triggers
  - the four runner endpoints return `202` with Running, and a second call doesn't start a second run
  - a single held decision returns `409` while `HeldDecisionProgress` runs
- [x] 8.4 `AiringFullRefreshBackgroundService` test: a refresh that throws leaves the tracker Failed with the AniList reason. Update `SeriesBulkBuildBackgroundServiceTests` for the new tracker type.
- [x] 8.5 `MalOAuthServiceRefreshTests`, with a fake `HttpMessageHandler` behind `IHttpClientFactory`. Cover:
  - `400` and `401` → `Refused`, with the connection marked lost
  - `503`, `429` and `403` → `Unavailable`, with nothing marked
  - a thrown `HttpRequestException` → `Unavailable`
  - a timeout `TaskCanceledException` → `Unavailable`
  - a `200` → `Refreshed`, saved
- [x] 8.6 `MalTokenStore` tests (in-memory provider):
  - `MarkConnectionLostAsync` keeps the first time
  - `SaveAsync` clears it
- [x] 8.7 `MalTokenProvider` tests:
  - a lost row returns null and never calls `RefreshAsync`
  - `RefreshAfterRejectionAsync`:
    - returns the stored token without a refresh when it differs from the rejected one
    - refreshes when they match
    - two concurrent calls refresh once
- [x] 8.8 `MalAuthPacingHandler` tests, with a fake inner handler and provider. Cover:
  - `401` → refresh → the request is sent again once and its response returned
  - refused → `MalAuthorizationRequiredException`
  - unavailable → the original `401` returned
  - a second `401` is returned without another refresh
  - a client-id request's `401` is left alone
- [x] 8.9 `InitialImportService` tests. Extend `InitialImportServiceActivityTests`' setup, or add `InitialImportServiceWorkTests`. Cover:
  - nothing missing → shown snapshot NotStarted, and the gate went through
  - 2 missing out of 10 on MAL → total 2
  - a metadata-only anime is work, with the entry added and no fetch
  - a pending-removal anime is neither work nor re-added
  - one fetch failure → Failed "1 of 2", with the gate went through
  - a list-read failure with entries present → shown snapshot unchanged, and the gate not gone through
  - the same failure with an empty list → Failed shown
  - a no-work run clears an earlier Failed
- [x] 8.10 `InitialImportBackgroundService` tests, with a millisecond schedule injected. Cover:
  - a list-read failure is retried on schedule and stops after the fifth
  - a clean run ends the sequence
  - a trigger signal resets it
  - a lost token row → no run and no retry
  - `MalAuthorizationRequiredException` → no retry
  - `RetryAt` is published while a retry is planned
- [x] 8.11 Reconciliation tests. Cover:
  - `RunAsync` with a sink reports each page's cumulative count and never sets a total (fake client paging through the callback)
  - `RunAsync` with no sink touches nothing
  - two overlapping `RunAsync` calls sharing one gate leave exactly one `PendingReconciliationDiff`
  - `ReconciliationBackgroundService` records `LastRunFailed = false` on success, and `true` plus the reason on failure
- [x] 8.12 `EntryPushService.DrainPendingAsync` with a sink: the total is edits plus removals, and it returns `(Pushed, NotPushed)`. `HeldChangeService` bulk methods report their total and per-item progress; extend the `HeldChangeService*Tests` support.
- [x] 8.13 Update `TransferImportServiceTests` for the gate, with one test per refusal in design D10, plus "went through with fetch failures → accepted".
- [x] 8.14 `AppStatusControllerTests`:
  - the response carries every job key, `malConnection` and `weeklyCheck`
  - a quiet list import reads NotStarted
  - a lost token row reads `Lost` with `lostAt`
  - no MAL client is resolved: the fakes throw if called
- [x] 8.15 Update every other test that references the removed types or signatures:
  - `ResyncService*Tests`
  - `InitialImportServiceActivityTests`
  - `TransferImport*Tests`
  - `TransferControllerTests`
  - fakes of `IMalClient`, `IEntryPushService`, `IHeldChangeService`, `IMalOAuthService` and `IMalTokenStore`
- [x] 8.16 Build and run the whole backend test project in the `sdk:10.0` image, from a fresh `/private/tmp` copy. Record the pass count. (1245/1245 passed, 0 failures, clean build.)

## 9. Frontend: data

- [x] 9.1 In `api/types.ts`:
  - add `JobPhase`, `JobStatusDto` (`total: number | null`, `error`, `startedAt`, `finishedAt`, `retryAt`), `HeldDecisionJobDto` (with `action`), `WeeklyCheckDto`, `MalConnectionState`, `MalAuthStatus` (`{ state, lostAt }`) and `AppStatusDto`
  - remove `ResyncStatusDto`, `AiringFullRefreshStatusDto`, `SeriesBulkBuildStatusDto`, their phase types, and `ReconciliationResultDto`
- [x] 9.2 In `api/client.ts`:
  - add `getAppStatus()`
  - make `syncNow`, `runReconciliation`, `acceptAllHeldChanges`, `declineAllHeldChanges`, `triggerResyncFromMal`, `triggerAiringFullRefresh` and `triggerSeriesBulkBuild` return `JobStatusDto` (`HeldDecisionJobDto` for the held pair)
  - remove the three per-job status getters
  - rename `getImportStatus` (it reads the transfer import) to `getTransferImportStatus`
- [x] 9.3 In `App.tsx`, show the connect screen only for `state === 'NotConnected'` (D14).
- [x] 9.4 Create `hooks/useAppStatus.ts` per design D16:
  - poll every 1 s while any job is Running and every 10 s otherwise, only while visible
  - re-read on `focus` and `visibilitychange`
  - expose `status`, `refresh()` and `applyJob(key, dto)`

## 10. Frontend: Settings page

- [x] 10.1 `JobProgress` takes `total: number | null`, plus `retryAt` and a `noRetryPlanned` flag, with the wording table and ARIA of design D17. In `SettingsPage.css`:
  - add `.job-progress__fill--indeterminate`, with a `@keyframes` slide
  - add a `prefers-reduced-motion: reduce` override to a static fill
- [x] 10.2 Replace the four per-job polling effects and their status states with `useAppStatus` (D16):
  - keep one `starting` flag per button, for the moment its `POST` is in flight
  - apply each trigger response with `applyJob`
  - add the ended-job reload table, using a ref of previous phases
  - load the transfer import report through `getTransferImportStatus` at mount and when `fileImport` ends
- [x] 10.3 Give "Sync now" and "Run full reconciliation" a `JobProgress` (nouns "sent" and "anime read"). Their buttons are disabled from the job's phase, reading "Resyncing…" and "Reconciling…" while running (D19).
- [x] 10.4 In the held section (D18):
  - show the `heldDecision` job's `JobProgress` (noun "decided")
  - disable every row and bulk button while it runs
  - show the job's `error` when it failed
  - drop `heldActingId === 'all'`
  - show a single decision's `409` as the section's error
- [x] 10.5 Add the MyAnimeList list import block at the top of the Sync group, rendered only when `listImport.phase !== 'NotStarted'`, with the D19 title and hint, the noun "anime", and the retry wording.
- [x] 10.6 Add the "Weekly check" readout row after "Last successful sync", with the four wordings in D19.
- [x] 10.7 Render the Account group's three states. The Lost text uses `settings-box__error`, and the re-authorize link is unchanged (D19).
- [x] 10.8 Update the comments that say "three background jobs" or describe the in-request actions, so none describes the old behaviour.

## 11. Docs

- [x] 11.1 Update CODE_GUIDE:
  - **HTTP API table:**
    - add `GET /api/app-status`
    - mark the sync now, reconcile, held bulk and trigger POSTs as `202` with a job
    - note the single held decisions' `409`
    - change `GET /api/mal-auth/status` to `{state, lostAt}`
    - remove the four deleted status reads
  - **A new `Services/Jobs/` section:** the tracker, the runner, and `JobFailure`
  - **Section updates:** `Services/Sync/` (the sinks, the gate, the weekly outcome), `Services/Import/` (work, quiet runs, retries, the pending-removal skip), and the MAL OAuth notes (refusal vs. outage, a 401 refreshed once, the lost state)
  - **"Background jobs at a glance":** the list import's retries, the weekly outcome, and the runner's jobs
  - **Frontend section:** `useAppStatus`

## 12. Verification

- [x] 12.1 Run `npm run build` and `npm run lint` in `frontend/` with nvm's Node 22. Both must pass with no new warnings.
- [x] 12.2 Rebuild and restart the stack (`docker compose build backend frontend && docker compose up -d`). Confirm:
  - `__EFMigrationsHistory` lists `ReportJobsAndLostMalConnection`
  - the three columns exist with null values
  - Account reads "Connected."
  - Settings shows no list import block after a start with nothing new
- [x] 12.3 Walk the jobs in the running app:
  - **First press:** one press on "Run corrective re-sync" and on "Refresh all airing dates" each shows a bar at once, and a second browser shows the same bar with its button disabled.
  - **Crash:** with the backend container disconnected from the network (`docker network disconnect`), the airing refresh ends Failed with "AniList couldn't be reached". Reconnect afterwards.
  - **Sync now:** with edits pending, it shows a counting bar and ends Complete.
  - **Reconciliation:** it shows a moving bar with the anime read, then Complete, and the diff reloads.
  - **Held changes:** create them by editing and restarting the backend within the 8-second debounce. Accept all shows its bar, and every accept and decline stays disabled until it ends.
  - **List import:** delete one `UserAnimeEntries` row for an anime on MAL (noting the row), then restart. The import block appears with a total of 1 and ends Complete.
  - **Weekly check:** set `ReconciliationRunLogs.LastRunAt` to 8 days ago, disconnect the network, and restart. The weekly line says it failed because MyAnimeList couldn't be reached, with no bar. Reconnect, and restore the previous `LastRunAt`.
- [x] 12.4 Walk the connection:
  - **Outage:** with the network disconnected and the token's `ExpiresAt` set in the past, a sync reads as an outage: Account still reads "Connected." Restore `ExpiresAt`, and reconnect.
  - **Refusal:** set the token's `RefreshToken` to an invalid value and `ExpiresAt` in the past. The next signed-in call records Lost, and Account explains it.
  - **Survives restart:** after a restart it still reads Lost, and the logs show no refresh attempts.
  - **Re-authorize:** re-authorizing clears it, and the list import runs.
- [x] 12.5 Restore the dev database's rows to the state they had before 12.3 and 12.4.
- [x] 12.6 Run `openspec validate report-jobs-and-lost-mal-connection`.
