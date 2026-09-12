## 1. Backend: the seen flag on the shared job shape

- [x] 1.1 In `Services/Jobs/JobProgressTracker.cs` (design D2, D3):
  - add `bool OutcomeSeen = false` to `JobSnapshot`, as its last member, with a comment that it is only meaningful once a run has ended
  - make `Snapshot` `virtual`
  - add `public virtual void MarkOutcomeSeen(DateTimeOffset finishedAt)`: under the lock, set the flag only when the phase is `Complete` or `Failed` **and** `FinishedAt == finishedAt`; otherwise do nothing
  - confirm `TryBeginCore`'s fresh `JobSnapshot` already clears it, and say so in its comment
- [x] 1.2 Add `OutcomeSeen` to `JobDto` and `HeldDecisionJobDto` (`JobDto.cs`), mapped from the snapshot.
- [x] 1.3 In `Services/Import/ListImportProgress.cs` (design D3):
  - change `Snapshot` from `new` to `override`
  - override `MarkOutcomeSeen`: under `_lock`, delegate to `base.MarkOutcomeSeen` while `_visible`, and otherwise set the flag on `_shown` when `_shown.FinishedAt == finishedAt`
  - note in the class comment that the override acknowledges whichever snapshot `Snapshot` is showing, and that lock order stays subclass-then-base as the rest of the class already does
- [x] 1.4 In `Services/Transfer/` (design D4):
  - add `StartedAt`, `FinishedAt` and `OutcomeSeen` to `TransferImportStatusSnapshot`
  - `MarkPending` stamps `StartedAt`; `Complete` and `Fail` stamp `FinishedAt`
  - add `MarkOutcomeSeen(DateTimeOffset finishedAt)` to the interface and the class, with the same guard as 1.1
  - `ToJobSnapshot()` passes all three through and reports `Total = 0 ? null : Total`, with a comment on why an unknown total must not read as zero

## 2. Backend: the weekly check's seen flag

- [x] 2.1 Add `public bool LastRunOutcomeSeen { get; set; }` to `Models/ReconciliationRunLog.cs`, with a comment that it belongs to the run `LastRunAt` names (design D6).
- [x] 2.2 In `ReconciliationBackgroundService.RecordRunAttemptAsync`, reset it to false where `LastRunFailed`/`LastRunError` are already reset.
- [x] 2.3 Generate the `NavbarSettingsStatusIndicator` migration with `dotnet ef migrations add` in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp`, with its Designer file and the model snapshot. Check that it adds `ReconciliationRunLogs.LastRunOutcomeSeen` (`boolean NOT NULL DEFAULT FALSE`) and nothing else, with a `Down` that only drops that column.

## 3. Backend: the combined read and the report

- [x] 3.1 In `AppStatusController`, add the `sync` block (design D1):
  - inject `IUserAnimeEntryRepository` and call `GetSyncStatusAsync` for `pendingCount`, `heldCount` and `lastSyncedAt`
  - add `diffPending` from `db.PendingReconciliationDiffs.AnyAsync(ct)`
  - add `outcomeSeen` to the `weeklyCheck` object from the new column
  - keep the controller free of any outside call, and note in its summary that the held count is the stored count, not the pruned one
- [x] 3.2 Add `POST api/app-status/seen` to the same controller (design D5):
  - body `{ jobs: [{ name, finishedAt }], weeklyCheckLastRunAt }`, every part optional
  - a `switch` expression mapping each job name to that tracker's `MarkOutcomeSeen`, placed immediately beside the read's own list with a comment tying the two together
  - an unknown name returns `400`; an unmatched `finishedAt` records nothing and still returns `204`
  - the weekly part sets `LastRunOutcomeSeen` only while `LastRunAt` still equals the reported time, and saves once
  - return `204`
- [x] 3.3 Remove `GET api/sync/status` from `SyncController` (design D1), leaving `GetSyncStatusAsync` in place for the combined read.

## 4. Backend tests

- [x] 4.1 `JobProgressTrackerTests`: `MarkOutcomeSeen` with a matching `FinishedAt` sets the flag; a different time sets nothing; a Running job sets nothing; a never-run job sets nothing; `TryBegin` after a seen outcome reports unseen again.
- [x] 4.2 `ListImportProgress` tests (extend the import test support): a shown failure is acknowledged while quiet, through `_shown`'s end time; a run shown from the start is acknowledged through the live snapshot; an unmatched time changes nothing.
- [x] 4.3 `TransferImportProgressTracker` tests: `MarkPending` stamps a start; `Complete`/`Fail` stamp an end; `ToJobSnapshot` reports an unknown total before `Start(total)` and the real total after; `MarkOutcomeSeen` applies the guard.
- [x] 4.4 `AppStatusControllerTests`:
  - the response carries `sync` with all four figures, `outcomeSeen` on every job, and `outcomeSeen` on `weeklyCheck`
  - `diffPending` is true only while a `PendingReconciliationDiff` row exists
  - the fakes still throw if any MyAnimeList client is called
  - `POST /seen` marks a named job's outcome; a stale `finishedAt` marks nothing and answers `204`; an unknown name answers `400`
  - the weekly part marks only on a matching `LastRunAt`
- [x] 4.5 `ReconciliationBackgroundService` test: a new attempt resets `LastRunOutcomeSeen` to false along with the outcome fields.
- [x] 4.6 Update every test that reads `GET api/sync/status` or constructs the changed records/snapshots.
- [x] 4.7 Build and run the whole backend test project in the `sdk:10.0` image, from a fresh `/private/tmp` copy. Record the pass count. (1264/1264 passed)

## 5. Frontend: data and the shared status

- [x] 5.1 In `api/types.ts`:
  - add `outcomeSeen: boolean` to `JobStatusDto`, and to `WeeklyCheckDto`
  - add `AppStatusSyncDto` (`pendingCount`, `heldCount`, `lastSyncedAt`, `diffPending`) and a `sync` member on `AppStatusDto`
  - remove `SyncStatusDto`
- [x] 5.2 In `api/client.ts`: add `reportOutcomesSeen(body)` (a `fetchVoid` POST, following `markUpdatesSeen`'s shape) and remove `getSyncStatus`.
- [x] 5.3 Create `context/AppStatusContext.tsx` from `hooks/useAppStatus.ts` (design D7), unchanged in cadence and in `applyJob`, exposing `AppStatusProvider` and `useAppStatus()`, plus an `applyWeeklyOutcomeSeen()` merge for the weekly flag. Delete `hooks/useAppStatus.ts`.
- [x] 5.4 Add the three pure derivations to that module (design D8): `unseenOutcomes`, `settingsNeedsAttention`, `oldestRunningJob`, each taking `AppStatusDto | null` and each commented with the rule it carries, including the fixed key order that breaks a tie.
- [x] 5.5 Mount `<AppStatusProvider>` in `AppShell.tsx` around the navbar and the routes.

## 6. Frontend: the shared bar, the shared dot, and the gear

- [x] 6.1 Create `components/JobProgressTrack.tsx` + `.css` (design D10): the track and fill, determinate and indeterminate, with the keyframes and the `prefers-reduced-motion` override moved out of `SettingsPage.css`; `role="progressbar"` with the current ARIA when `valueText` is given, `aria-hidden` when it isn't; `className` for the navbar's sliver.
- [x] 6.2 Rewrite `SettingsPage`'s `JobProgress` to render `JobProgressTrack` for its track, keeping its wrapper, wording, ARIA text and failed variant. In `SettingsPage.css`, drop the moved rules and re-point `.job-progress--failed .job-progress__fill` at the new fill class.
- [x] 6.3 In `Navbar.css`: add `.navbar__status-dot` (the current `.updates-menu__dot` rules, comment included), give `.navbar__settings` `position: relative`, and add `.navbar__settings-progress` — absolute, 8 px from each side, 5 px from the bottom, 2 px tall, with a matching border-radius.
- [x] 6.4 Point `UpdatesMenu.tsx` at `navbar__status-dot` and delete `.updates-menu__dot` from `UpdatesMenu.css`.
- [x] 6.5 Create `components/Navbar/SettingsLink.tsx` (design D11): move `settingsClassName` and `GearIcon` there, read the shared status, render the dot and the sliver, and compose the accessible name from D11's table. Use it from `Navbar.tsx`.

## 7. Frontend: the Settings page reports what it shows

- [x] 7.1 Add the reporting effect to `SettingsPage` (design D9): derive `unseenOutcomes`, skip keys already reported in this page's lifetime (a ref keyed by name + `finishedAt`, plus the weekly `lastRunAt`), `POST` the rest, merge `outcomeSeen: true` into the shared status on success, forget the keys on failure, and say nothing either way.
- [x] 7.2 Drop the page's own sync-status fetch (design D1): read `pendingCount` and `lastSyncedAt` from `appStatus.sync`, remove the `status` state, the `getSyncStatus` calls in `load()` and in the ended-job effect, and the "Couldn't load sync status." branch. Leave the held figure reading from the held list, and leave the diff's own fetch and reload branch alone.
- [x] 7.3 Update the comments that describe `useAppStatus` as the page's own poll, so none describes the old arrangement.

## 8. Docs

- [x] 8.1 Update CODE_GUIDE:
  - **HTTP API table:** `GET /api/app-status` gains the `sync` block and `outcomeSeen`; add `POST /api/app-status/seen`; remove `GET /api/sync/status`
  - **`Services/Jobs/` section:** the seen flag, its `FinishedAt` guard, and the list import's override
  - **"Background jobs at a glance":** the weekly row gains `LastRunOutcomeSeen`
  - **Frontend sections:** `AppStatusContext` in place of `useAppStatus` (provider, derivations), `JobProgressTrack`, `SettingsLink`, and the `SettingsPage` row's note that the gear now carries the dot and the bar
- [x] 8.2 Update `docs/PROJECT_CHEAT_SHEET.md`'s endpoint row for the removed `GET /api/sync/status`.

## 9. Verification

- [x] 9.1 Run `npm run build` and `npm run lint` in `frontend/` with nvm's Node 22. Both must pass with no new warnings.
- [x] 9.2 Rebuild and restart the stack (`docker compose build backend frontend && docker compose up -d`). Confirm `__EFMigrationsHistory` lists `NavbarSettingsStatusIndicator`, the column exists, and `GET /api/app-status` carries `sync` and every `outcomeSeen`.
- [x] 9.3 Walk the dot in the running app:
  - **Held changes:** create them by editing and restarting the backend within the debounce. The gear shows the dot; open Settings and leave again — it is still showing; decide them all — it clears.
  - **A diff:** run full reconciliation against a deliberately diverged entry. The dot shows, survives opening the page, and clears on accept or cancel.
  - **A finished job:** start the airing-date refresh, navigate away before it ends. The dot appears when it ends, and clears when Settings is opened — and in a second browser as soon as it is focused.
  - **Watched to the end:** start the same job and stay on the page. No dot.
  - **Failed:** with the backend container disconnected from the network, the airing refresh fails and raises the dot. Reconnect afterwards.
  - **Weekly failure:** set `ReconciliationRunLogs.LastRunAt` to 8 days ago, disconnect the network, restart. The dot shows, opening Settings clears it, and a restart leaves it clear. Restore the previous `LastRunAt`.
  - **Restart:** a finished job's dot is gone after a backend restart; a held change's dot is not.
- [x] 9.4 Walk the bar in the running app:
  - a determinate job (build all series) fills the sliver; reconciliation, which has no total, moves it
  - a second browser shows the same sliver without a reload
  - start build-all-series, then the airing refresh: the sliver stays on the build, hands over when it ends, and the build raises the dot
  - a file import with anime to fetch moves the sliver before its total is known
  - with the list import quiet at startup, and with the metadata refresh running, no sliver and no dot
  - under `prefers-reduced-motion`, the indeterminate sliver is still and distinct from a filled one
- [x] 9.5 Check the gear's accessible name in all four states (unaffected, attention only, running only, both) with the browser's accessibility inspector.
- [x] 9.6 Restore the dev database's rows to the state they had before 9.3 and 9.4.
- [x] 9.7 Run `openspec validate navbar-settings-status-indicator`.
