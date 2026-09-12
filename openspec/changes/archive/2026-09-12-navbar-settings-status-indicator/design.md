## Context

Change 09 (`report-jobs-and-lost-mal-connection`) left this change most of its groundwork:

- **One job shape.** `Services/Jobs/JobProgressTracker` holds `JobSnapshot(Phase, Done, Total?, Error, StartedAt, FinishedAt, RetryAt)` behind its own lock. `TryBegin()` moves a job to Running and stamps `StartedAt` in one locked step, and the starting request is answered with the job already Running — so "started but not yet begun" is already Running with a start time. `Complete()`/`Fail(reason)` stamp `FinishedAt`. Eight jobs report through it, seven as `JobProgressTracker` subclasses and the file import through its own tracker.
- **One read.** `AppStatusController` serves `GET api/app-status` as `{ malConnection, weeklyCheck, jobs }` from the eight tracker snapshots plus two single-row reads (`OAuthToken`, `ReconciliationRunLog`). It calls nothing outside the app.
- **One poll.** `hooks/useAppStatus.ts` polls that read every 1 s while any job is Running and every 10 s otherwise, only while the tab is visible, re-reading at once on `focus`/`visibilitychange`, and merges a trigger's own response through `applyJob` so the first press shows a bar immediately. Only `SettingsPage` mounts it.
- **The two surfaces to match.** `.updates-menu__dot` (`UpdatesMenu.css:54`) is an 8 px `--accent` circle with a 1.5 px `--bg` ring, absolutely placed 4 px from the bell's top-right corner. `JobProgress` (`SettingsPage.tsx:173-212`) renders a `role="progressbar"` track (`6 px`, `--code-bg`) with a `--accent` fill, either proportional or `--indeterminate` (a 30 %-wide fill sliding on `@keyframes job-progress-slide`, replaced by a static half-opacity fill under `prefers-reduced-motion`), plus the wording beside it.

**What is missing for the navbar:**

1. The read carries neither how many changes are held (`GET api/sync/status`, from `UserAnimeEntryRepository.GetSyncStatusAsync`) nor whether a reconciliation diff is waiting (`GET api/sync/reconcile/pending`, which returns the whole diff and is far too heavy to poll).
2. Nothing records that an outcome has been seen, so nothing can distinguish "a job finished" from "a job finished and I was looking at it".
3. `TransferImportProgressTracker.ToJobSnapshot()` reports `StartedAt`/`FinishedAt` as null and its total as `0` before the file is read. The first two make the file import unorderable and its outcome unidentifiable; the third draws an empty determinate bar where the spec asks for a moving one.
4. `ListImportProgress.Snapshot` **hides** the base member with `new` and answers with a frozen snapshot of the last *shown* run while a quiet run is in flight. Anything holding the tracker as a `JobProgressTracker` silently reads the wrong snapshot.
5. `useAppStatus` is per-page state, so the navbar would poll a second time and disagree with the page for up to a second.

**Constraints:**

- The backend compiles and tests only in the `mcr.microsoft.com/dotnet/sdk:10.0` image, from a copy under `/private/tmp` (a bind mount of `~/Documents` fails).
- Backend tests use the EF in-memory provider, which does not support `ExecuteUpdateAsync`.
- The frontend has no test runner; it builds with nvm's Node 22 and is verified by hand in the running app.
- The database is PostgreSQL; migrations run at startup from `Program.cs`.
- The navbar renders on every page, so anything it reads is read from every page.

## Goals / Non-Goals

**Goals:**

- One dot on the Settings gear covering five facts, clearing on the terms each fact deserves, identical in every browser on the device.
- One thin bar on the gear while a reported job runs, oldest first, in both the determinate and indeterminate states, from the same definition the Settings page uses.
- One server-held answer to "have I seen this outcome", so opening Settings anywhere clears the dot everywhere.
- One poll and one snapshot behind both the navbar and the Settings page.
- No call to MyAnimeList or AniList on the navbar's account, from any page.

**Non-Goals:**

- Carrying any of it between devices.
- A count, a tooltip, a menu, or any text on the gear.
- Changing the poll cadences, the jobs, what they report, the weekly cadence, or the routine background services.
- Showing anything for the weekly check's differences (the diff already does), or for routine work.
- Reporting an outcome as seen from anywhere but the Settings page.
- A dot or bar on any other navbar control.

## Decisions

### D1. The read grows a `sync` block, and `GET api/sync/status` folds into it

`GET api/app-status` gains one member:

```
sync: { pendingCount, heldCount, lastSyncedAt, diffPending }
```

- `pendingCount`, `heldCount` and `lastSyncedAt` come from the existing `IUserAnimeEntryRepository.GetSyncStatusAsync` — the same method `GET api/sync/status` uses today, so the two cannot disagree.
- `diffPending` is `db.PendingReconciliationDiffs.AnyAsync(ct)`. `ReconciliationService` stores a diff only when it has differences (`:120`) and both accept and cancel delete the row, so existence is exactly "a diff is waiting".

`GET api/sync/status` is then **removed**, and `SettingsPage` reads these three figures from the shared status instead of its own fetch. Only that page ever called it.

This costs the read three counts, one `MAX`, and one `EXISTS` on top of the two single-row reads it already does — all on indexed columns of a local database, no joins, and no outside call. That stays inside 09's "cheap enough to repeat every second".

*Why fold rather than add:* `heldCount` would otherwise be defined in two endpoints, and the page would keep a fetch whose result the poll already carries. Folding also fixes a small staleness: the page's pending figure is currently refreshed only on mount and when a job ends, so an edit made on another page doesn't show up until something else happens.

*Alternatives:*
- **A separate `GET api/navbar-status`.** Rejected: two shapes and two polls for one picture, which is the drift 09 set out to remove.
- **Serving the dot and the bar already computed** (`{ needsAttention: true, activeJob: {…} }`). Rejected: it duplicates facts the payload already carries, and it puts presentation rules in the controller. The derivation is deterministic from the snapshot (D8), so every browser agrees either way.
- **Counting held items through `HeldChangeService.GetHeldAsync`** so the dot matches the page exactly. Rejected outright: that read calls MyAnimeList once per held item.

### D2. `outcomeSeen` lives on the snapshot, guarded by `FinishedAt`

`JobSnapshot` gains `bool OutcomeSeen` (default false), and `JobProgressTracker` gains:

```csharp
public virtual void MarkOutcomeSeen(DateTimeOffset finishedAt)
```

Under the tracker's lock it sets the flag only when the snapshot is Complete or Failed **and** its `FinishedAt` equals `finishedAt`; otherwise it does nothing. `TryBeginCore` already builds a fresh `JobSnapshot` for each run, so starting a job clears the flag for free.

`JobDto` carries `outcomeSeen`, so it rides on both the combined read and every trigger response.

*Why guard on `FinishedAt`:* the reporter names the run it saw. Without the guard, a report in flight while a *new* run starts and ends could mark an outcome seen that nobody ever saw. The window is milliseconds, but the guard is one comparison and it makes the rule testable.

*Alternatives:*
- **A per-job run number** in the DTO, reported back instead of a timestamp. Cleaner equality in principle, but `FinishedAt` is already in the DTO and is already exact for in-memory jobs.
- **A separate acknowledgement store** keyed by job name. Rejected: the flag's lifetime is exactly the run's, and the tracker already owns that lifetime under one lock.
- **Clearing outcomes on read** ("reading the page's status marks it seen"). Rejected: any browser's poll would clear it, including one that never showed the page.

### D3. `Snapshot` becomes virtual, and the list import overrides both members

`JobProgressTracker.Snapshot` becomes `virtual`, and `ListImportProgress.Snapshot` changes from `new` to `override`. That removes a live trap (a `JobProgressTracker`-typed reference reading past the list import's shown-snapshot rule) and is what lets `MarkOutcomeSeen` be dispatched correctly.

`ListImportProgress` then overrides `MarkOutcomeSeen` to acknowledge **whichever snapshot it is currently showing**, mirroring `Snapshot`'s own choice:

- while the current run is shown (`_visible`), delegate to `base.MarkOutcomeSeen`;
- while it is quiet, set the flag on the frozen `_shown` snapshot when that snapshot's `FinishedAt` matches.

Without the override, a shown failure followed by a quiet retry could never be acknowledged: the page shows `_shown`'s end time, while the base snapshot is Running with none.

Lock order stays as it already is throughout this class — the subclass's `_lock` first, then the base's — so no new deadlock is possible (`Snapshot` and `Gate` already nest that way).

### D4. The file import joins the shared shape properly

`TransferImportStatusSnapshot` gains `StartedAt`, `FinishedAt` and `OutcomeSeen`:

- `MarkPending` stamps `StartedAt` (it is called from the accepting request, which is when the run starts — the same moment `TryBegin` stamps it for every other job);
- `Complete` and `Fail` stamp `FinishedAt`;
- `MarkOutcomeSeen(finishedAt)` applies the same guard as D2.

`ToJobSnapshot()` passes all three through, and reports `Total = 0 ? null : Total`, so a run that has not yet read the file reports an unknown total and draws a moving bar rather than an empty one. A run that genuinely had nothing to do then reads "Complete — 0 items" instead of "0/0", which is no worse.

*Alternative:* special-case the file import in the ordering and the report (sort it last, acknowledge it by phase alone). Rejected: it is one of the eight jobs the spec lists, and "every job reports when its run started and ended" is already a requirement it simply fails to meet.

### D5. `POST api/app-status/seen`, with a name-to-tracker switch beside the read

```
POST api/app-status/seen
{ "jobs": [ { "name": "syncNow", "finishedAt": "2026-09-12T10:00:00.123456+00:00" } ],
  "weeklyCheckLastRunAt": "2026-09-12T09:00:00+00:00" }
```

- **204** on success, including an empty body and an empty list.
- **400** for a job name the controller doesn't have — a typo must not fail silently.
- Each named job's `MarkOutcomeSeen(finishedAt)` applies its own guard, so an unmatched time records nothing and still answers 204: the reporter is telling us about a run that has since moved on, which is not an error.
- The weekly part sets `ReconciliationRunLog.LastRunOutcomeSeen` only when `LastRunAt` still equals the reported time (D6).

The name-to-tracker mapping is a single `switch` expression on the controller, returning `Action<DateTimeOffset>?` — `"syncNow" => syncNowProgress.MarkOutcomeSeen`, and so on. It sits immediately beside the read's own object literal, with a comment tying the two lists together, so a job added to one and not the other is visible in review.

The page sends back the exact timestamp string the read gave it, so no formatting question arises; `System.Text.Json` round-trips `DateTimeOffset` losslessly, and both the in-memory snapshot and the run-log row are compared against the value they themselves produced.

*Alternatives:*
- **A `JobRegistry`** (name → tracker) shared by the read and the report, so the two lists are one. Rejected for now as more machinery than it buys: the file import's tracker is not a `JobProgressTracker`, and the held pair serves a different DTO, so the registry would need either an interface plus a per-entry DTO factory or `object`-typed returns. Two eight-line lists in one file, adjacent, are easier to review than that indirection. Worth revisiting if a ninth job arrives.
- **`PUT api/app-status/jobs/{name}/seen`, one call per outcome.** Rejected: opening the page with three ended jobs would make three requests.

### D6. The weekly check's answer is persisted, because its outcome is

`ReconciliationRunLog` gains `bool LastRunOutcomeSeen` (not null, default false). `ReconciliationBackgroundService.RecordRunAttemptAsync` resets it to false where it already resets `LastRunFailed`/`LastRunError`, so each weekly run's outcome is unseen until reported.

A job's flag can live in memory because a restart takes the whole outcome with it. The weekly outcome survives restarts, so an in-memory flag would re-raise a dot I had already dealt with on every restart.

Existing rows get `false`, with no data fix-up: if the last weekly run genuinely failed and I haven't seen why, one dot after deploying is correct rather than noise.

*Alternatives:*
- **Treat a failed weekly check as "clears when resolved"** — no flag at all, the dot standing until a run succeeds. Rejected: only the weekly service writes the run log, so nothing I can press would clear it and the dot would sit for up to a week.
- **Keep it in memory with the job flags** and accept the dot returning after a restart. Rejected as the one avoidable recurrence in the design; the column is one line in the recorder.

### D7. `useAppStatus` becomes an app-wide provider

`hooks/useAppStatus.ts` moves to `context/AppStatusContext.tsx` as `AppStatusProvider` plus a `useAppStatus()` consumer, mounted in `AppShell` around both the navbar and the routes (`AppShell` is itself mounted only once a token is on file, which is also the only state in which a navbar exists).

The polling body is unchanged: 1 s while any job in the last snapshot is Running, 10 s otherwise, visible tabs only, an immediate re-read on `focus` and `visibilitychange`, and `applyJob` merging a trigger's response at once. `SettingsPage` keeps calling `useAppStatus()` and now gets the shared instance, so:

- there is one request, not two;
- a press on the page moves the navbar's bar in the same render;
- reporting an outcome as seen on the page clears the navbar's dot in the same render (D9).

The idle cadence stays at 10 s rather than being relaxed for app-wide use: it is what the page already does, the payload is a handful of local reads, and it is what makes a dot raised by a background job appear promptly. `focus` covers the case that matters for a tab left open.

*Alternative:* have the navbar mount its own `useAppStatus`. Rejected: two polls, and the two surfaces disagreeing for up to a second after every press and every report.

### D8. Three pure derivations, shared by the navbar and the page

`context/AppStatusContext.tsx` also exports three functions over an `AppStatusDto | null`, so the rules live in one place and every browser applies them identically:

- **`unseenOutcomes(status)`** → `{ jobs: { name, finishedAt }[], weeklyCheckLastRunAt: string | null }`: every job whose phase is `Complete` or `Failed` with `outcomeSeen === false` and a non-null `finishedAt`, plus the weekly check's `lastRunAt` when it `failed === true` and is unseen. This is both what the dot counts and exactly what the page reports (D9).
- **`settingsNeedsAttention(status)`** → `boolean`: `sync.heldCount > 0 || sync.diffPending || malConnection.state === 'Lost' || unseenOutcomes(...)` is non-empty. A null status is `false` — nothing is claimed before the first read lands.
- **`oldestRunningJob(status)`** → `JobStatusDto | null`: among jobs with phase `Running`, the smallest `startedAt`; ties, and a Running job with no `startedAt`, fall back to the fixed key order of `AppStatusJobsDto`, which is the same in every browser.

`state === 'NotConnected'` needs no handling: `App.tsx` shows the connect screen instead of the shell.

### D9. The page reports what it displays, once, and merges the answer locally

In `SettingsPage`, one effect on the shared status:

- computes `unseenOutcomes(appStatus)`;
- drops entries already reported in this page's lifetime, held in a ref keyed by `name + finishedAt` (and the weekly `lastRunAt`), so a poll landing before the server's flag flips doesn't re-report;
- `POST`s the rest, and on success merges `outcomeSeen: true` into the shared status through `applyJob` for each job, plus the weekly flag, so the navbar's dot clears in the same render rather than up to 10 s later;
- on failure, forgets the in-flight keys so the next poll tries again, and says nothing — `performFetch`'s connection notice already covers an unreachable backend.

Because the effect runs on every status change while the page is mounted, "on open" and "as each job ends while open" are the same code path, which is what makes the spec's two rules one mechanism.

*Alternative:* report on mount only, and again in the ended-job effect. Rejected: two paths for one rule, and the ended-job effect would have to re-derive what the first already knew.

### D10. One dot definition, one bar definition

**The dot.** `.updates-menu__dot` is replaced by `.navbar__status-dot` in `Navbar.css`, used by both the bell and the gear. Navbar.css is always loaded wherever either control renders (the navbar renders the Updates menu), so no import changes. `.navbar__settings` gains `position: relative`, which the bell's button already has.

**The bar.** The track and fill move out of `SettingsPage.css` into a shared `components/JobProgressTrack.tsx` + `.css`:

```tsx
JobProgressTrack({ done, total, valueText?, className? })
```

- `total === null` → the indeterminate fill; otherwise a `width: pct%` fill.
- `valueText` present → `role="progressbar"` with the same ARIA `JobProgress` carries today (`aria-valuemin/now/max` omitted while indeterminate, as the spec for an indeterminate progressbar asks, with the wording in `aria-valuetext`). Absent → `aria-hidden="true"`, for the navbar's decorative sliver.
- `className` is how the navbar makes it a sliver: `.navbar__settings-progress` positions it absolutely 8 px from each side and 5 px from the bottom, 2 px tall, inside the gear's 40 px box.

`SettingsPage`'s `JobProgress` keeps its wrapper, its wording and its failed variant, and renders this component for the track; `.job-progress--failed .job-progress-track__fill` keeps the failed colour. The indeterminate keyframes and the `prefers-reduced-motion` override move with the component, so there is one of each.

*Alternative:* duplicate ten lines of CSS in `Navbar.css` (the precedent `UpdateCard.css` set for the "New" badge's dot). Rejected here: this is the *same* bar and the *same* dot in the same navbar row, and a drift would read as a bug rather than as a variation.

### D11. `SettingsLink`, and what the gear announces

`Navbar.tsx`'s inline Settings `NavLink` moves to `components/Navbar/SettingsLink.tsx`, taking `settingsClassName` and `GearIcon` with it. It reads the shared status and renders the link, the gear, the dot when `settingsNeedsAttention`, and the bar when `oldestRunningJob` is non-null.

Both the dot and the bar are `aria-hidden`; the facts ride on the link's accessible name, as the bell's dot already does:

| needs attention | job running | accessible name |
|---|---|---|
| no | no | `Settings` |
| yes | no | `Settings, needs attention` |
| no | yes | `Settings, a job is running` |
| yes | yes | `Settings, needs attention, a job is running` |

The name says *whether*, never *how much*: it names no counts and no progress, so a focused gear isn't re-announced every second while a bar advances, and the numbers stay where they are explained in words — on the Settings page.

*Alternative:* expose the sliver as a real `role="progressbar"` with a live `aria-valuetext`. Rejected: a progressbar nested inside a link is awkward to navigate, and a value changing every second announces noise for a decoration.

## Risks / Trade-offs

- **[A held item that MyAnimeList already agrees with keeps the dot until Settings is opened]** → Accepted and specified. The cheap count is the only count the navbar may take (D1), opening the page is what clears such an item, and the next poll drops the dot.
- **[A second browser can flash the dot for a second while the first has Settings open]** → It polls at 1 s while the job runs and may see the ended, unreported outcome just before the first browser's report lands. Accepted: it clears on the following poll.
- **[A Settings page in a hidden tab reports nothing]** → The poll is visible-tabs-only, so an outcome isn't reported as seen until that tab is looked at again. Correct, if anything: I did not watch it finish.
- **[A quiet list import run erases an unseen failure]** → `EndWithoutWork()` resets the shown snapshot to not-started, so a retry that finds nothing missing drops the dot the earlier failure raised. Accepted: the failure resolved itself, which is what the list-import spec already says a quiet run means.
- **[Each automatic retry of the list import can raise the dot again]** → It is a run of a reported job and ends like one. Since the dot is one bit and stays raised until Settings is opened, a sequence of retries raises nothing extra until I have actually looked.
- **[The 10 s idle poll now runs on every page]** → Seven indexed local reads per poll, no outside calls, and it is the cadence the Settings page already used. If it ever needs relaxing, it is one constant.
- **[Removing `GET api/sync/status` breaks an old frontend against a new backend]** → Deploy both together, as this repo always does (`docker compose build`).
- **[The sync readout loses its "Couldn't load sync status." fallback]** → It now shares the page's loading state, and the poll keeps the last-known snapshot when a read fails, so a transient failure no longer blanks the readout at all.
- **[`Snapshot` turning virtual changes dispatch for existing callers]** → That is the point: today a `JobProgressTracker`-typed reference to `ListImportProgress` reads the wrong snapshot. Every current caller of the public member either holds the concrete type or wants the override.
- **[Both the dot and the bar are `aria-hidden`, so the name is the only carrier]** → The name states both facts (D11), and the Settings page carries the detail in words.

## Migration Plan

1. Deploy backend and frontend together. On startup the migration adds `ReconciliationRunLogs.LastRunOutcomeSeen` (`boolean NOT NULL DEFAULT FALSE`) before the background services run.
2. Mixed versions are not supported for one release: an old frontend calls the removed `GET api/sync/status`, and a new frontend calls `POST api/app-status/seen`, which an old backend answers 404.
3. First load: nothing to migrate in the browser. No key, no storage, no per-browser state is introduced or retired.
4. **Rollback:** `Down` drops the column. A failed weekly check would then raise nothing, as it does today.

## Open Questions

None blocking. Two things are deliberately left for later, each a small addition if wanted:

- **A `JobRegistry`** folding the read's list and the report's switch into one (D5). Worth doing when a ninth job arrives.
- **The bar on the Settings page's own navbar.** It shows there too, beside the page's fuller bars. Suppressing it would be a special case for no stated benefit, so it stays.
