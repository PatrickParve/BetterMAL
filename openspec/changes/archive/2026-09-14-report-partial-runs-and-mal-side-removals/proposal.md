## Why

The 2026-09-13 code triage (`docs/ISSUE_TRIAGE.md`) found three sync and job correctness bugs. All three were re-checked against the current code.

- **B2.** Two background jobs report "complete" even when some anime failed.
- **R2.** One unrecognized MyAnimeList list-status string aborts a whole run.
- **PF3.** Reconciliation never notices an anime deleted on MyAnimeList's website. If I later edit that anime here, the next push adds it back on MyAnimeList.

The fixes are small and all touch the sync and job area. The companion change `remove-corrective-resync` has already landed (`8e7deb0`), so `ResyncService.cs` and B5 are gone and are not part of this change.

**Verified against the code** (2026-09-13, at `8e7deb0`):

- **B2.**
  - `AiringFullRefreshBackgroundService.cs:38-39` ignores what `RefreshManyAsync` returns and always calls `progress.Complete()`.
  - `SeriesBulkBuildBackgroundService.cs:95-105` logs a failed build and still calls `Complete()`.
  - `RefreshManyAsync`'s only return value, `zeroRows`, counts two different things (`EpisodeScheduleRefreshService.cs:48-68`): "AniList has no data for this anime", which is not a failure, and a thrown exception, which is.
  - `background-jobs` already forbids this: "A run that gets through its work but cannot do all of it SHALL end as failed". The import and sync now already follow that rule.
- **R2.** `MalMappingExtensions.ToWatchStatus` throws on any status outside the five MAL uses today. That throw is reached with no per-item catch from:
  - `ReconciliationService.cs:80`. The whole run aborts before the diff is saved.
  - `InitialImportService.cs:101`, the entry-only branch. The whole import aborts.
  - `HeldChangeService.cs:44,65,103`. The held-changes review fails to load.
  - **Two sites the triage missed:**
    - `HeldChangeService.cs:224` and `:256`, the decline paths. A single decline answers 500, and decline-all aborts partway through.
    - `InitialImportService.cs:134`, inside `ImportOneAsync`. That one is inside the per-item `try`, but it is miscounted as "couldn't be fetched" and costs a wasted details fetch.
- **PF3.**
  - `ReconciliationService.RunLockedAsync` only walks MAL's list forward.
  - `ReconciliationDiffChangeType` has only `Added` and `Updated`.
  - `mal-write-sync` has no rule for an anime that MAL no longer lists but this app still has.
  - A push (`EntryPushService.cs:36`) stamps `LastSyncedAt`, which lets a removal proposal tell "MAL dropped it" apart from "it was pushed after MAL's list was read".

## What Changes

- **Partial runs end as failed (B2).**
  - The full airing-date refresh counts anime whose refresh threw. It ends as failed, saying how many, when any did.
  - "AniList has no data for this anime" still isn't a failure.
  - Build all series counts failed targets and ends as failed the same way.
  - Both reuse the existing `Fail("{n} of {total} …")` reporting. No new job state or UI.
- **An unrecognized MyAnimeList status is skipped, never guessed (R2).** An anime whose MyAnimeList list status the app doesn't recognize:
  - is left out of that reconciliation run and that import run, with nothing created, updated, cached or pushed for it;
  - is logged as a warning that names the anime id and the raw status;
  - makes the run end as not clean:
    - a manual reconciliation and the import end as failed, saying how many were skipped;
    - the weekly check records a failure with the same reason, so it shows on the "Weekly check" line;
  - is treated by the held-changes review and decline exactly as an unreadable MyAnimeList side. The review shows "MyAnimeList unavailable", nothing self-clears, and a decline changes nothing.
- **Reconciliation detects anime removed on MyAnimeList (PF3).**
  - A run also proposes removing each local entry that MyAnimeList no longer lists. It skips:
    - entries with an unsent local edit;
    - anime with a queued removal;
    - anime MyAnimeList did list but that were skipped for an unrecognized status;
    - entries pushed after MyAnimeList's list was read.
  - The proposal is a new diff type, `RemovedOnMal`, held in the same pending diff and shown in the same Settings review.
  - Accepting it deletes the local entry. Nothing is pushed to MyAnimeList, since it is already in that state, and nothing is recorded in the activity log, since this is a sync path. An entry edited or pushed since the diff was computed is left alone.
  - Declining (Cancel) leaves the entry as it is.
- **Frontend.**
  - `ReconciliationDiffChangeType` gains `'RemovedOnMal'`.
  - The diff row labels it "Removed on MyAnimeList" and shows the values it has locally.
- **Internal API shapes change, no breaking change for any client.**
  - `ReconciliationResult` gains two counts.
  - `IEpisodeScheduleRefreshService.RefreshManyAsync` returns a small result record instead of an `int` that no caller reads.
  - The pending-diff endpoint's `changeType` may now be `"RemovedOnMal"`. The frontend is its only client and ships in the same build.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `background-jobs`: "A run always ends as complete or failed" now names series that could not be built and anime skipped for an unrecognized status as work left undone, says that an anime AniList has no data for is not, and adds scenarios for the airing-date refresh and build all series.
- `series-page`: "Build all series from my list". A run with failed targets now ends as failed, saying how many, instead of complete.
- `mal-write-sync`:
  - **Rewritten:** "Full reconciliation computes a reviewable diff". Removals count as differences, and a run that skipped anime for an unrecognized status ends as failed (manual) or records a failure (weekly).
  - **Added:** "Reconciliation proposes removing anime MyAnimeList no longer lists": what is detected, what is excluded, and what accept and decline do.
  - **Added:** "A MyAnimeList list status the app does not recognize is never guessed": reconciliation skips the anime, and the held-changes review and decline treat MyAnimeList's side as unreadable.
- `initial-import`:
  - **Rewritten:** "Visible import progress indicator". An anime with an unrecognized status is skipped, not added, and the run ends as failed, saying how many.
  - **Rewritten:** "An import that could not finish tries again on its own". A run that skipped anime counts as one that could not finish.

## Impact

- **Backend:**
  - `Services/Airing/`: `EpisodeScheduleRefreshService.cs`, `IEpisodeScheduleRefreshService.cs` and `AiringFullRefreshBackgroundService.cs`.
  - `Services/Series/SeriesBulkBuildBackgroundService.cs`.
  - `Services/Mal/MalMappingExtensions.cs`: a non-throwing status check, with one list of known statuses.
  - `Services/Sync/`: `ReconciliationService.cs`, `IReconciliationService.cs`, `ReconciliationBackgroundService.cs` and `HeldChangeService.cs`.
  - `Services/Import/InitialImportService.cs`.
  - `Services/Jobs/JobFailure.cs`: one shared wording for "skipped for an unrecognized status".
  - `Controllers/SyncController.cs`: the manual reconcile ends as failed on skips.
  - `Models/PendingReconciliationDiff.cs`: the new enum value.
- **No migration.** `ChangeType` is stored as text through `HasConversion<string>()`, and the new diff type reuses the existing value columns.
- **Backend tests:**
  - **New or extended:** reconciliation removal detection and accept, unrecognized-status skips in reconciliation, import and held changes, the airing refresh and bulk build ending as failed, and `RefreshManyAsync`'s split counts.
  - **Updated:** the fakes that build `ReconciliationResult` or implement `RefreshManyAsync` (`SyncControllerTests`, `ReconciliationBackgroundServiceTests`, `AiringFullRefreshBackgroundServiceTests`).
- **Frontend:** `api/types.ts` and `pages/SettingsPage.tsx`, for the diff row label only.
- **Docs:**
  - `SETTINGS.md` ("Run full reconciliation") and `CODE_GUIDE.md` (reconciliation, import and job notes), both tracked.
  - `docs/ISSUE_TRIAGE.md` (local, gitignored): B2 and PF3 move to Fixed, R2 is resolved, and B5 stays untouched.
- **Out of scope:**
  - every other triage item (R1, R3–R5, B3, B4, S1–S4, N1–N4, SP5, PF1, PF2, PF4–PF6);
  - any "completed with a note" job state;
  - any automatic mapping of an unknown status;
  - changes to the import's retry schedule.
