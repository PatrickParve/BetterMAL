## Why

The Settings page is where anything waiting on me shows up, and where long-running work reports its progress. Nothing outside that page says so. To find out whether a job finished, whether changes are held for review, or whether MyAnimeList has stopped accepting this device's login, I have to open the page and look.

Change 09 (`report-jobs-and-lost-mal-connection`) built exactly the read this needs — `GET api/app-status`, one cheap answer covering every job's state and the connection — and deliberately stopped short of the navbar, leaving it to "a later change (10)". This is that change: the navbar's Settings gear grows the same dot the updates bell carries, and a thin progress bar along its bottom edge while a job runs.

**Verified against the code (2026-09-12).** The request describes the code as it was *before* change 09 landed (commit `92a93f4`). What is there now:

- **The combined read already exists.** `AppStatusController` serves `GET api/app-status` from the tracker snapshots plus two single-row reads, and calls nothing outside the app.
- **The per-job trackers are already one type.** `IResyncProgressTracker`, `IAiringFullRefreshProgressTracker`, `ISeriesBulkBuildProgressTracker` and `IImportProgressTracker` are gone, replaced by `JobProgressTracker` subclasses (`ResyncProgress`, `AiringFullRefreshProgress`, `SeriesBulkBuildProgress`, `SyncNowProgress`, `ReconcileProgress`, `HeldDecisionProgress`, `ListImportProgress`). The eight jobs the request lists are exactly the eight keys of `GET api/app-status`'s `jobs`.
- **The Settings page no longer polls per job.** `SettingsPage.tsx:306` uses `useAppStatus` (`frontend/src/hooks/useAppStatus.ts`), which polls the combined read every 1 s while any job is Running, every 10 s otherwise, only while the tab is visible, with an immediate re-read on `focus`/`visibilitychange`. So "should the settings page read the same status" is already answered for jobs — what is left is to *share one poll* with the navbar rather than run a second one.
- **`JobDto` already carries `startedAt`.** `TryBegin` stamps it in the same locked step that moves the job to Running, and the starting request is answered with the job already Running — so a job that has been started but whose work has not begun is already Running with a `startedAt`, which is what "oldest" needs.
- **Two things the read does not carry:** how many changes are held (`UserAnimeEntryRepository.GetSyncStatusAsync`, `:18`, served separately by `GET api/sync/status`), and whether a reconciliation diff is waiting (`ReconciliationService.cs:120` stores one only when it has differences; `GET api/sync/reconcile/pending` returns the whole diff).
- **The file import is not fully in the shared shape.** `TransferImportProgressTracker.ToJobSnapshot` reports `StartedAt`/`FinishedAt` as null, and its total as `0` rather than unknown while it has not read the file yet. Both matter here: without a start time it cannot take part in "the oldest job", without an end time its outcome cannot be reported as seen, and a zero total draws a stalled-looking empty bar instead of a moving one.
- **Line references, current:** the Settings gear is `Navbar/Navbar.tsx:82-84`; the dot to match is `UpdatesMenu.tsx:77` styled at `UpdatesMenu.css:54`; the bar to match is `JobProgress` at `SettingsPage.tsx:173-212`, styled at `SettingsPage.css:155-201`.
- **One routine job's cadence differs from the request's:** `EpisodeScheduleRefreshBackgroundService` runs 20 s after start and then every 6 hours, not hourly. It stays silent either way, along with `MetadataRefreshBackgroundService` (10 min), `RelationAdjudicationBackgroundService`, `PendingSyncRetryBackgroundService` (2 min), `DebouncedEntrySyncScheduler` and `MalTokenRefreshBackgroundService` — none of them reports into a job tracker, so none of them can raise a dot or a bar.

## What Changes

**The Settings gear carries a dot**

- The same dot as the updates bell's, in the same position, from **one shared definition** rather than a copy: the bell and the gear both use it, so they cannot drift apart.
- It shows when any of these is true:
  - changes are held for review;
  - a reconciliation diff is waiting, whoever computed it;
  - the MyAnimeList connection is lost;
  - a job ended — complete or failed — and its outcome has not been reported as seen.
- It clears when the underlying fact clears: held changes decided, the diff accepted or cancelled, the connection re-authorized, the outcome seen.
- It carries no count. One dot, whatever the number of reasons.

**"Seen" is recorded on the server, so every browser agrees**

- A job's ended outcome carries `outcomeSeen`. The Settings page reports an outcome as seen **because it is displaying it**: on mount for the outcomes already there, and as each further job ends while the page is open. That covers both of the request's rules with one mechanism — opening the page clears a finished job's dot, and a job I watched finish on the page never raises one.
- The report is guarded by the outcome's own `finishedAt`, so a run that ends in the moment between the page's read and its report cannot be marked seen by a report meant for the previous run.
- Starting a job clears its flag, so the next run's outcome raises the dot again.
- Job state stays in memory: a restart returns every job to not-started, which takes its dot with it.
- The weekly check's outcome is the one exception — it is persisted (`ReconciliationRunLog`) and so is its seen flag, or a restart would re-raise a dot I had already dealt with.

**The weekly check raises a dot only when it fails**

- A weekly run that finds differences raises no dot of its own: the diff it stores does that.
- A weekly run that failed raises the dot until I open Settings, where the "Weekly check" line says what happened.

**A thin progress bar along the gear's bottom edge**

- Shown while any job is Running: the list import (which is Running only when it has anime to fetch), sync now, run full reconciliation, accept all / decline all, the corrective re-sync, the airing-date refresh, the build-all-series run, and the import from a file.
- No text, no counts. Filled in proportion when the total is known, moving when it is not — the same two states, the same colours and the same reduced-motion behaviour as the Settings page's own bar, from **one shared bar component** used by both.
- Nothing else can draw it. The weekly check and the 2-minute push retry are already not reported as jobs, and no other background work reports progress at all.
- With more than one job running it shows the one started first, counting a job whose work has not begun yet; when that one ends the bar moves to the next still running, and the one that ended raises the dot.
- The dot and the bar show together when both apply.

**One poll for the whole app**

- `useAppStatus` becomes an app-wide provider mounted in `AppShell`, above the navbar and the routes. The navbar and the Settings page read the same snapshot from one poll, at the cadence that already exists: 1 s while a job runs, 10 s otherwise, visible tabs only, and immediately on focus.
- A trigger's own response is still merged at once, so a press on the Settings page shows the navbar's bar without waiting for the next poll.
- `GET api/app-status` gains what the dot needs and `GET api/sync/status` is folded into it:
  - **BREAKING (internal API):** `GET /api/sync/status` is removed. Its three figures become `app-status`'s `sync` block, alongside whether a diff is waiting.
  - Every job gains `outcomeSeen`; the weekly check gains it too.
  - **New:** `POST /api/app-status/seen` reports outcomes as seen.
- The read stays cheap and still calls nothing outside the app: in-memory snapshots plus seven indexed local reads.
- The held count in the read is the database's count. The Settings page's own "Held for review" figure keeps coming from the held list, which prunes items MyAnimeList already agrees with — so the dot can stand for a held item that clears itself when I open Settings. That is accepted: opening the page is what resolves it, and the next poll drops the dot.

**Accessible labelling**

- The gear's accessible name carries both facts — "Settings", "Settings, needs attention", "Settings, a job is running", or both — so neither the dot nor the bar is carried by appearance alone. Both are `aria-hidden`, as the bell's dot already is.

## Capabilities

### New Capabilities

- `navbar-settings-status`: what the navbar's Settings control reports without being opened — when the dot appears and what clears it, what raises no dot, the progress bar and which work draws it, which job it shows when several run, that every browser on the device agrees, and how both are labelled for a screen reader.

### Modified Capabilities

- `background-jobs`:
  - **Rewritten:** "Job and connection state are read together" — the one read also carries what is waiting for me to decide (held count, whether a diff is waiting), the pending-push figures, and, per job, whether its outcome has been seen.
  - **New requirement:** a job's outcome can be reported as seen, guarded by the time that run ended; a new run clears it; the weekly check's own answer is kept across restarts.
  - **No requirement change, but a fix:** "Every background job shares one lifecycle" already asks every job — the file import included — to report when its run started and ended, and its total as unknown rather than zero. The file import reports none of the three. It has to, for the bar to move before the file is read and for its outcome to be identifiable.
- `settings-page`:
  - **New requirement:** the page reports the outcomes it shows as seen — those already on it when it opens, and each one that ends while it is open, including a failed weekly check.
  - **Rewritten:** "The sync readout separates what is retrying from what is waiting on me" — the figures come from the shared status read, and the held figure keeps coming from the held list.
- `navigation-and-search`:
  - **Rewritten:** "Navbar layout" — the Settings control is no longer labelling-frozen: it carries a status indicator and a progress bar, defined by `navbar-settings-status`, while staying a link to the Settings page that navigates on click.

## Impact

- **Backend:**
  - `Services/Jobs/`: `JobSnapshot` gains `OutcomeSeen`; `JobProgressTracker` gains a guarded `MarkOutcomeSeen`, and its `Snapshot` becomes virtual so `ListImportProgress`'s shown-snapshot override stops being a hidden member; `JobDto` carries the flag.
  - `Services/Import/ListImportProgress`: overrides both, so a quiet run's frozen outcome is the one reported and acknowledged.
  - `Services/Transfer/TransferImportProgressTracker`: start and end times, an unknown total, and `MarkOutcomeSeen`.
  - `Controllers/AppStatusController`: the `sync` block, `POST api/app-status/seen`, and the one name-to-tracker mapping both actions read.
  - `Controllers/SyncController`: `GET api/sync/status` removed.
  - `Models/ReconciliationRunLog` + migration: the weekly outcome's seen flag, reset by `ReconciliationBackgroundService` at attempt time.
  - Tests: the tracker, the two special trackers, the controller's read and its new report, and the weekly reset.
- **Frontend:**
  - `api/types.ts` / `api/client.ts`: `outcomeSeen`, the `sync` block, `reportOutcomesSeen()`, and `getSyncStatus` removed.
  - `context/AppStatusContext.tsx` (new, replacing `hooks/useAppStatus.ts`): the provider, the hook, and the pure derivations — needs-attention, the oldest running job, and the outcomes not yet seen.
  - `AppShell.tsx`: mounts the provider.
  - `components/JobProgressTrack.tsx` + `.css` (new): the bar itself, in both its states, used by the navbar and by the Settings page.
  - `components/Navbar/SettingsLink.tsx` (new) + `Navbar.tsx` / `Navbar.css`: the gear with its dot, bar and accessible name, and the shared dot class.
  - `components/Updates/UpdatesMenu.tsx` / `.css`: the bell uses the shared dot class.
  - `pages/SettingsPage.tsx` / `.css`: reports outcomes as seen, drops its own sync-status fetch and the reload branches that only refreshed it, and renders the shared bar.
- **Docs:** CODE_GUIDE's HTTP API table, its `Services/Jobs/` and frontend sections, and "Background jobs at a glance"; `docs/PROJECT_CHEAT_SHEET.md`'s endpoint row.
- **No change:** the poll cadences, the jobs themselves and what they report, the weekly cadence, the held-change and diff review flows, the updates bell's own behaviour, and every routine background service.
