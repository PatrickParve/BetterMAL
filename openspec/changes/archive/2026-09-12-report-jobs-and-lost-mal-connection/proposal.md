## Why

The Settings page is where long-running work, and anything waiting on me, should show up. Today it doesn't manage that:
- several of its jobs don't report reliably;
- four big actions show no progress at all;
- a MyAnimeList connection that MAL has stopped accepting is never noticed.

This change makes the page accurate on its own. It also gathers the job and connection state into one cheap read, which a later change (10) will show in the navbar. Nothing is added to the navbar here.

**Verified against the code** (2026-09-11), in addition to what the request lists:

- **Build all series can queue a second run too.** `SeriesController.cs:154-159` calls `MarkPending()` on every press, not only the first. A press from a second browser while a build runs:
  - resets the running build's progress to 0/0;
  - queues a second build behind it.
- **Several jobs learn their total late, and MAL never gives one.** `InitialImportService.cs:25-26` and `ResyncService.cs:34-35` read the whole MAL list before they call `Start`. The list endpoint carries no total: `MalClient.GetAllPagesAsync` (`:190-206`) just follows `paging.next`.
- **The list import brings back a removal that hasn't reached MAL.** Its entry-only branch (`InitialImportService.cs:50-59`) never checks `PendingEntryDeletions`. So on the next start, an anime I removed, whose removal is still queued, is re-added from MAL. Reconciliation already skips these (`ReconciliationService.cs:20, 33-37`).
- **The file import depends on the list import's phase.** `TransferImportService.cs:23-30` refuses a file until `ImportPhase.Complete`. Any change to how the list import reports has to keep that gate working.
- **A revoked login goes unnoticed for up to a month.**
  - `MalAuthPacingHandler.cs:36` retries only 403, and hands a 401 from a signed-in call back to the caller.
  - `MalTokenProvider.cs:27` refreshes only within 10 minutes of expiry, and the access token lives about 31 days.
  - So an access token MAL has revoked keeps being sent until it nears expiry.
- **The weekly and manual reconciliation can overlap.** Each deletes the held diff and adds its own. If they overlap, two diffs can be left behind, and `AcceptPendingDiffAsync` (`ReconciliationService.cs:133`) applies whichever it reads first.

## What Changes

**Every job has one lifecycle, kept on the server**
- **One shared tracker for every job.** It records:
  - the phase: NotStarted, Running, Complete or Failed;
  - done-of-total, where the total can be unknown;
  - a failure reason;
  - when the run started and finished.

  It replaces the four per-job trackers and phase enums that lack Failed.
- **Starting once.** A press starts its job only when the job is neither starting nor running, and the response already says Running. A second press, from any browser, starts nothing. This covers "Correct imported data", "Airing dates", "Build all series", and the four actions below.
- **Ending.** Every run ends as Complete or Failed. A run that throws records a plain reason ("MyAnimeList couldn't be reached", "The connection to MyAnimeList was lost") or points to the backend logs.
- **Storage.** Progress stays in memory, as today, and a restart clears it.

**Sync now, Run full reconciliation, Accept all and Decline all run in the background**
- Their endpoints start a job and answer `202` with its state. They no longer do the work inside the request.
- **What each reports:**
  - **Sync now:** edits sent, out of those pending.
  - **Accept all and Decline all:** changes decided, out of those held. They share one job, so neither can start while the other runs, and single held decisions are refused meanwhile.
  - **Run full reconciliation:** anime read so far while it pages the list. It has no total.
- A run that couldn't finish every item ends as Failed and says how many were left, e.g. "sent 3 of 5".

**The weekly check stays quiet**
- Reconciliation reports progress only when the caller hands it somewhere to report. The manual job does, and the weekly run doesn't, so the weekly run never shows a bar. The 2-minute push retry, which shares Sync now's code, is quiet the same way.
- The weekly and manual runs never run at once.
- `ReconciliationRunLog` also records whether the last weekly run failed, and why. The Sync readout shows it in one line, next to "Last successful sync".

**The MAL list import reports only when it has work**
- **Work** is each anime on my MAL list that this device's list lacks, and the import's total counts only those. A run that finds none shows nothing.
- **When it shows.** It stays quiet while it reads the list, except on a device with an empty list: a first import is shown from the start.
- **How it ends.** It ends as Complete, or as Failed with how many anime couldn't be fetched.
- **Retries.** A run that couldn't finish tries again on its own, after 1, 5, 15, 60 and 60 minutes, then waits for the next start. The page says when it will next try.
- **Pending removals.** It leaves alone an anime whose removal is still pending.
- **Lost connection.** It neither runs nor retries while the connection is lost. Re-authorizing starts it.
- **The file import's gate.** The gate is kept. It opens once a run has gone through the whole list since the app started, and its refusal says why the file can't be imported yet.

**A lost MAL connection is noticed and explained**
- **Refusal vs. outage.** A refresh that MAL's token endpoint answers with `400` or `401` records the connection as lost, with the time. No answer, a timeout, or any other status is an outage and records nothing.
- **Revoked logins.** A `401` on a signed-in call triggers one forced refresh and one retry, so a revoked login is noticed the first time it is used.
- **While lost:**
  - the app sends no signed-in request and attempts no refresh;
  - the lost state survives a restart;
  - re-authorizing clears it.
- **BREAKING (internal API):** `GET api/mal-auth/status` returns `{ state: Connected | Lost | NotConnected, lostAt }` in place of `{ connected }`.
- **The app stays usable.** A lost connection keeps me in the app. Only NotConnected shows the connect screen.
- **The Account section explains it.** It says:
  - that the connection was lost, and when;
  - that my changes aren't being sent;
  - that I need to re-authorize.

**One cheap read of all of it**
- A new `GET api/app-status` returns:
  - every job's state;
  - the connection state;
  - the weekly check's last outcome.

  It reads from memory plus two single-row reads, and never calls MyAnimeList.
- The Settings page polls it while the tab is visible: every second while a job runs, every 10 seconds otherwise.
- **BREAKING (internal API):** these per-job status reads are removed:
  - `GET api/sync/resync-from-mal/status`
  - `GET api/airing/refresh-all/status`
  - `GET api/series/build-all/status`
  - `GET api/import/status`

  `GET api/transfer/import/status` stays, for the import's report.

**Settings page**
- The shared progress bar can move without a total ("Starting…", "412 anime read").
- New bars appear for:
  - Sync now;
  - Run full reconciliation;
  - the held changes' Accept all and Decline all;
  - the MAL list import, as a report in the Sync group, shown only while it has something to report.
- Each button is disabled by its job's own state, so every browser agrees.

## Capabilities

### New Capabilities

- `background-jobs`:
  - the lifecycle every job shares;
  - starting a job once;
  - a run never staying Running;
  - automatic runs of shared work being told apart from runs I started;
  - the one read of all job states and the connection state.

### Modified Capabilities

- `settings-page`:
  - **Rewritten:**
    - "Settings are organised into named groups": Sync holds the list import's report and the weekly check line, and Account holds the connection's three states.
    - "Changes held for review are surfaced on the Settings page": Accept all and Decline all run in the background with progress.
    - "Background jobs report progress the same way": more jobs, totals that aren't known yet, and failure reasons.
  - **New requirements:**
    - a job starts on the first press;
    - the list import's report;
    - the weekly check line;
    - the lost-connection explanation.
- `initial-import`:
  - "Background full-list import after authorization" is rewritten: it also runs at every start and after re-authorizing, and not while the connection is lost.
  - "Visible import progress indicator" is rewritten: shown only when there is work, and a total that counts only the work.
  - New requirements: it leaves a pending removal alone, and it tries again on its own.
- `mal-write-sync`:
  - "Manual sync now" and "Full reconciliation computes a reviewable diff" are rewritten: they run in the background and report, and the weekly run is quiet and records its outcome.
  - A new requirement: deciding every held change runs in the background.
- `mal-api-integration`, with new requirements:
  - a refused sign-in is recorded as a lost connection;
  - a rejected access token is refreshed once before giving up;
  - nothing signs in while the connection is lost;
  - re-authorizing restores it;
  - the connection state is reported.
- `device-transfer`: "No import while the list is being imported" is rewritten for quiet runs, and for runs that couldn't read the list.

## Impact

- **Backend:**
  - **New `Services/Jobs/`:**
    - the shared tracker and its snapshot, plus one tracker per job;
    - a background runner for the four in-request actions;
    - failure-reason wording.
  - **Controllers:**
    - `SyncController`, `AiringController` and `SeriesController` change their triggers and lose their status reads.
    - `MalAuthController` changes its status shape.
    - `ImportController` is removed.
    - A new `AppStatusController` serves the combined read.
  - **Sync:**
    - `ReconciliationService` takes a progress sink and a run gate.
    - `ReconciliationBackgroundService` records the weekly outcome.
    - `EntryPushService.DrainPendingAsync` takes a progress sink.
    - `HeldChangeService` changes its bulk methods.
  - **Import:** `InitialImportService` and `InitialImportBackgroundService` get the work rule, quiet runs, retries, and the pending-removal skip.
  - **Transfer:** `TransferImportService`'s gate.
  - **MAL:**
    - `IMalClient.GetFullUserAnimeListAsync` takes a page callback.
    - `MalOAuthService.RefreshAsync` returns a result.
    - `MalTokenStore`, `MalTokenProvider`, `MalTokenRefreshBackgroundService` and `MalAuthPacingHandler` handle the lost state.
  - **Models and migration:** `OAuthToken` gains `ConnectionLostAt`, and `ReconciliationRunLog` gains `LastRunFailed` and `LastRunError`, in one migration.
  - **Wiring and tests:** `Program.cs` registrations change, the existing tracker and controller tests are updated, and new tests are added.
- **Frontend:**
  - `api/types.ts` and `api/client.ts`: the job DTO, the combined read, the new connection status, and the changed trigger responses.
  - A new `hooks/useAppStatus.ts`.
  - `App.tsx`: the first-run gate.
  - `pages/SettingsPage.tsx` and `.css`: the indeterminate bar, the new bars, the list import report, the weekly line, and the Account states.
- **Docs:** CODE_GUIDE's HTTP API table, its Sync, Import and MAL auth sections, and its "Background jobs at a glance" table.
- **No change:**
  - the explanation texts of existing controls, apart from the new states;
  - the weekly cadence;
  - the 2-minute retry cadence;
  - the navbar;
  - the file import's own behaviour, apart from its refusal wording.
