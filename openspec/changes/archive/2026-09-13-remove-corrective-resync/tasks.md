## 1. Backend: remove the re-sync

- [x] 1.1 Delete `Services/Sync/ResyncService.cs`, `IResyncService.cs`, `ResyncBackgroundService.cs`, `ResyncTrigger.cs` and `IResyncTrigger.cs`.
- [x] 1.2 In `Services/Jobs/JobTypes.cs`, remove `ResyncProgress` and its doc comment.
- [x] 1.3 In `Program.cs`:
  - remove `AddSingleton<ResyncProgress>()`;
  - remove the "Corrective full re-sync" block (`IResyncTrigger`, `IResyncService`, `ResyncBackgroundService`) and its comment;
  - in the background-jobs comment, change "the other four keep their existing trigger/background-service shape" to "the other three";
  - re-point the airing full-refresh comment, which says it "mirrors the corrective MAL re-sync's" shape, at the build-all-series shape or a neutral wording.
- [x] 1.4 In `Controllers/SyncController.cs`, remove the `resyncTrigger`/`resyncProgress` constructor parameters, and `TriggerResyncFromMal` with its doc comment (`POST api/sync/resync-from-mal`).
- [x] 1.5 In `Controllers/AppStatusController.cs`:
  - remove the `resyncProgress` constructor parameter;
  - remove the `resync` entry from the `jobs` object;
  - remove the `"resync"` arm from `MarkSeen`'s switch;
  - change both "eight names" comments to "seven names".
- [x] 1.6 Update the doc comments that name deleted classes:
  - `Services/Mal/MalMappingExtensions.cs` `ApplyTo`: say it serves an already-existing entry, as when a held change is declined (`HeldChangeService`), rather than a corrective re-sync.
  - `Services/Updates/AnimeMetadataChangeDetector.cs`: drop the "extracted here once `ResyncService` needed the same detection" clause.
  - `Services/Airing/AiringFullRefreshBackgroundService.cs`: change "Manual-only, like ResyncBackgroundService" to say manual-only on its own, or like `SeriesBulkBuildBackgroundService`.
  - `Services/Airing/AiringRefreshTriggerBackgroundService.cs`: change "mirrors ImportTrigger/ResyncTrigger's" to "mirrors ImportTrigger's".
- [x] 1.7 Confirm that `grep -rni "resync\|re-sync" backend/AnimeTracker.Api --include='*.cs'` finds nothing outside `Migrations/`. The past migrations that mention `MalResync` stay as they are.

## 2. Backend tests

- [x] 2.1 Delete `Services/Sync/ResyncServiceActivityTests.cs` and `Services/Sync/ResyncServiceRewatchingTests.cs`.
- [x] 2.2 In `Controllers/SyncControllerTests.cs`:
  - remove `TriggerResyncFromMalGivesRunningOnBothOfTwoPresses` and `TriggerResyncFromMalSignalsExactlyOnceAcrossTwoPresses`;
  - remove the `resyncTrigger`/`resyncProgress` parameters and constructor arguments from `CreateController`;
  - drop "re-sync starts once like the other trigger-based jobs" from the header comment.
- [x] 2.3 In `Controllers/AppStatusControllerTests.cs`, remove `new ResyncProgress()` from `CreateController`, and the `body.jobs.resync` assertions in `TheResponseCarriesEveryJobKeyMalConnectionAndWeeklyCheck` and `TheResponseCarriesSyncAndOutcomeSeenOnEveryJobAndTheWeeklyCheck`.
- [x] 2.4 Add no new test for `resync` being refused by `POST api/app-status/seen`. The existing `MarkSeenWithAnUnknownNameAnswers400` covers any name missing from the switch; confirm it still passes.
- [x] 2.5 Build and test in the SDK image:
  - copy `backend/` to `/private/tmp/bm-build/`, excluding `bin/` and `obj/`;
  - run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`;
  - confirm that `MalMappingExtensionsTests`, `ReconciliationServiceRewatchingTests`, `HeldChangeServiceDeclineTests` and `HeldChangeServiceSelfClearingTests` pass, since they still cover the shared rewatch rule (design Risks).

## 3. Frontend

- [x] 3.1 In `api/client.ts`, remove `triggerResyncFromMal` and its comment.
- [x] 3.2 In `api/types.ts`, remove `resync` from `AppStatusJobsDto`.
- [x] 3.3 In `context/AppStatusContext.tsx`, remove `'resync'` from `JOB_KEY_ORDER`.
- [x] 3.4 In `pages/SettingsPage.tsx`:
  - remove the `triggerResyncFromMal` import, the `startingFullResync` state and `handleResyncFromMal`;
  - remove the whole "Correct imported data" `SettingsAction` inside "Data tools";
  - keep "Airing dates", build all series and the force-refresh picker exactly as they are.
- [x] 3.5 Confirm that `grep -rn "resync\|re-sync\|Correct imported" frontend/src` finds only sync now's "Resync now"/"Resyncing…" label and the unrelated "re-syncs" comments in `AnimeDetailPage.tsx` and `SeriesPage.tsx`.
- [x] 3.6 Build with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`. `tsc` must pass with no reference to `jobs.resync` left.
- [x] 3.7 Check by hand in the running app:
  - Data tools shows "Airing dates", build all series and the force-refresh picker, and no "Correct imported data";
  - starting "Airing dates" still shows its bar on the Settings page and in the navbar;
  - the navbar gear's dot still clears once an outcome has been seen.

## 4. Specs: Purpose paragraphs (design D4)

- [x] 4.1 In `openspec/specs/background-jobs/spec.md`'s Purpose, remove "the corrective re-sync," from the job list.
- [x] 4.2 In `openspec/specs/activity-recording/spec.md`'s Purpose, change "the background import, an accepted reconciliation diff, and the corrective re-sync" to "the background import and an accepted reconciliation diff".
- [x] 4.3 In `openspec/specs/settings-page/spec.md`'s Purpose, change "a several-minute MAL re-sync" to "a minutes-long airing-date refresh".
- [x] 4.4 Confirm that `grep -rni "re-sync\|resync" openspec/specs` finds only the requirements this change's deltas replace or remove, and nothing in any Purpose.

## 5. Tracked docs (design D6)

- [x] 5.1 In `SETTINGS.md`:
  - remove the "Run corrective re-sync" section and its quick-reference row;
  - in "Re-authorize with MAL", change "reconciliation/re-sync fail immediately" to "reconciliation fails immediately";
  - check that nothing else in the file still points at the removed section.
- [x] 5.2 In `CODE_GUIDE.md`:
  - **MAL endpoint table:** drop "corrective re-sync" from the `GET anime/{id}` callers, and "re-sync" from the `GET users/@me/animelist` callers.
  - **HTTP API table:** remove the `POST /api/sync/resync-from-mal` row.
  - **Job-tracker list:** drop `ResyncProgress`.
  - **Trigger-based jobs:** change "three older trigger-based jobs (re-sync, airing refresh, series build)" to two.
  - **Sync section:** remove the `ResyncService` and `ResyncTrigger`/`ResyncBackgroundService` bullets.
  - **Metadata-writer note:** drop `Services/Sync/ResyncService` (the corrective full re-sync) from the change detector's callers.
  - **Trigger-sharing note:** the note says `AiringController`'s full-refresh trigger and "`SyncController`'s resync trigger" share the `TryBegin` pattern, "so none of the three can be double-pressed". Drop the resync trigger and fix the count.
  - **`SettingsPage` row:** drop "corrective re-sync".
  - **"`PendingSync` is sacred" note:** change "both reconciliation and re-sync must (and do, at compute time) skip" to "reconciliation must (and does, at compute time) skip".
  - **Background-jobs table:** remove the `ResyncBackgroundService` row.
  - **Check:** `grep -ni "resync\|re-sync" CODE_GUIDE.md` finds nothing.

## 6. Validate and close out

- [x] 6.1 Run `openspec validate remove-corrective-resync --strict`.
- [x] 6.2 Update `docs/ISSUE_TRIAGE.md`:
  - replace B5's entry with a short note that the corrective re-sync was removed (change `remove-corrective-resync`), which ends the bug rather than fixing it;
  - in the "Found while checking" list, mark B5 superseded;
  - drop B2's `ResyncService` evidence and its "re-sync" mention in "How you'd notice", so B2 covers only the airing refresh and build all series;
  - in the ISSUES #16 table row, point at the removal;
  - in R2's evidence, drop the "`ResyncService.cs:79` is the only caller inside a per-item catch" line;
  - in R3's evidence, drop "and re-sync" from the `GetAllPagesAsync` callers;
