## Why

The Settings page's "Correct imported data" action (the corrective re-sync) overlaps with work that already runs elsewhere, and it carries a bug: it can bring back an anime whose removal hasn't reached MyAnimeList yet (`docs/ISSUE_TRIAGE.md` B5). Instead of patching B5, this change deletes the feature. With it gone, B5 can no longer happen, so no separate fix is needed.

**Verified against the code** (2026-09-13):

- **Entry data is already covered.** `ReconciliationService` ("Run full reconciliation") reads the same MyAnimeList list and applies status, episodes watched, score and dates when I accept. The difference is that I review the changes first. The re-sync applies every change straight away, with no review.
- **Metadata is already covered.** `MetadataRefreshBackgroundService` runs a **full-detail** fetch for every anime on my list, on a staleness tier (`metadata-refresh`): daily for airing shows, and as rarely as every 28 days for shows that finished long ago. A full-detail fetch costs one request against MAL's rate limit, the same as a one-field fetch. So the tiered job already fills in everything the re-sync fills in (English title, duration, source, broadcast, genres, relations, ranks). It just spreads the work out over time.
- **Nothing else depends on it.** `ResyncBackgroundService` runs only when `POST api/sync/resync-from-mal` signals it. Its own doc comment says "no auto-signal on startup". No other code calls `IResyncService` or `IResyncTrigger`.
- **The rewatch rule has other users.** `MalListStatus.ApplyTo` is the shared rewatch rule. After `ResyncService` is deleted, it is still called when a held change is declined (`HeldChangeService.cs:224`) and when a new entry is built (`ToUserAnimeEntry`). Reconciliation calls `MalStatusResolution.ResolveAgainstLocal` directly. The shared helpers stay, and so does the spec rule that every read-back goes through them.
- **B5's mechanism, for the record.** `ResyncService` creates an entry for every MAL list item that has no local entry, without checking `PendingEntryDeletions`. The import and reconciliation both make that check. When the queued removal later pushes, `EntryPushService.PushPendingDeletionAsync` sees the re-created entry, treats it as re-added, and pushes it back to MyAnimeList.

**Accepted trade-off.** There is no longer a way to refresh metadata for the whole list at once. A fix to how metadata is mapped reaches an old, finished show only when its tier next comes due, which can take up to 28 days.

## What Changes

- **BREAKING (internal API):** `POST api/sync/resync-from-mal` is removed.
- **BREAKING (internal API):** the combined `GET api/app-status` read no longer has a `jobs.resync` entry. `POST api/app-status/seen` now refuses the job name `resync` as unknown.
- **Removed from the Settings page:** the "Correct imported data" action in Data tools, with its button, its explanation and its progress bar. The "Airing dates" and build-all-series actions next to it stay unchanged.
- **Removed from the backend:**
  - `ResyncService`, `IResyncService`, `ResyncBackgroundService`, `ResyncTrigger`, `IResyncTrigger`
  - the `ResyncProgress` tracker and its four registrations in `Program.cs`
  - the re-sync's tests
- **Nothing replaces it.** No action or job is added, and no remaining job changes what it does.
- **Specs:** every requirement and scenario that names the corrective re-sync is removed or updated.
  - A requirement or scenario that exists only for the re-sync is removed.
  - A scenario that illustrates a general rule, and happens to use the re-sync as its example, is re-pointed at a job that remains.
  - A list of jobs or sync paths loses the re-sync entry.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `mal-write-sync`:
  - **Removed:** "A corrective re-sync does not demote a rewatch".
  - **Rewritten:** "Reconciliation does not demote a rewatch" takes over the one part of the removed requirement that still applies to other paths. Every read-back onto an existing entry goes through one shared rewatch rule, and the rule has no effect when an entry is being created.
- `initial-import`:
  - **Removed:** "Corrective full re-sync upsert".
- `background-jobs`:
  - **Rewritten:** "Every background job shares one lifecycle" loses the re-sync from its job list.
  - **Rewritten:** "Every background job shares one lifecycle", "A job is started once", "Job state is kept on the server, in memory" and "A job's outcome can be reported as seen" move their re-sync example scenarios to jobs that remain.
- `settings-page`:
  - **Rewritten:** "Settings are organised into named groups": Data tools no longer lists the re-sync.
  - **Rewritten:** "A preference is visibly not a job": the action example drops the re-sync.
  - **Rewritten:** "Background jobs report progress the same way": the re-sync leaves the job list.
  - **Rewritten:** "A job starts on the first press": the re-sync leaves the job list, and its "shows its bar at once" scenario is removed.
  - **Rewritten:** "Every sync control states what it will do": the re-sync leaves the covered controls, and its explanation bullet and two re-sync-only scenarios are removed. Two other scenarios are renamed or reworded so they no longer name it.
- `navbar-settings-status`:
  - **Rewritten:** "The Settings control signals that something needs me": the failed-job scenario uses the full airing-date refresh.
  - **Rewritten:** "The Settings control shows a progress bar while a job runs": the re-sync leaves the job list.
- `activity-recording`:
  - **Rewritten:** "The sync paths record nothing" covers two sync paths rather than three, and its re-sync scenario is removed.
- `artwork-selection`:
  - **Rewritten:** "A chosen picture survives every MAL sync" drops the re-sync from its list of syncs.
- `list-recaps`:
  - **Rewritten:** "A recap counts only progress recorded in the app" drops the re-sync from its example and its scenario.
- `metadata-refresh`:
  - **Rewritten:** "Full detail fetch reserved for import, tiered refresh, and detail view" drops the re-sync from the paths allowed to fetch full detail.

## Impact

- **Backend:**
  - **Deleted:** `Services/Sync/ResyncService.cs`, `IResyncService.cs`, `ResyncBackgroundService.cs`, `ResyncTrigger.cs`, `IResyncTrigger.cs`.
  - **Edited:**
    - `Services/Jobs/JobTypes.cs`: `ResyncProgress` is removed.
    - `Program.cs`: the four registrations and the re-sync comments are removed.
    - `Controllers/SyncController.cs`: the constructor parameters and the endpoint are removed.
    - `Controllers/AppStatusController.cs`: the constructor parameter, the `jobs.resync` entry and the seen switch arm are removed, and the "eight names" comments are corrected.
  - **Comments:** doc comments that name the deleted classes are updated in `MalMappingExtensions.cs`, `AnimeMetadataChangeDetector.cs`, `AiringFullRefreshBackgroundService.cs` and `AiringRefreshTriggerBackgroundService.cs`.
- **Backend tests:**
  - **Deleted:** `ResyncServiceActivityTests.cs` and `ResyncServiceRewatchingTests.cs`. The rewatch rule keeps its coverage through the reconciliation and held-change tests.
  - **Edited:** `SyncControllerTests.cs` loses its two re-sync tests and the re-sync constructor arguments. `AppStatusControllerTests.cs` loses the `ResyncProgress` argument and the `jobs.resync` assertions.
- **Frontend:**
  - `api/client.ts`: `triggerResyncFromMal` is removed.
  - `api/types.ts`: `AppStatusJobsDto.resync` is removed.
  - `context/AppStatusContext.tsx`: `'resync'` is removed from `JOB_KEY_ORDER`.
  - `pages/SettingsPage.tsx`: the action block, `startingFullResync` and `handleResyncFromMal` are removed.
- **Docs:**
  - `SETTINGS.md` (tracked): the "Run corrective re-sync" section and quick-reference row are removed, and the mentions in other sections are reworded.
  - `CODE_GUIDE.md` (tracked): the re-sync is removed from the API table, the Sync and job sections, and the background-jobs table.
  - `docs/ISSUE_TRIAGE.md` (local, gitignored): B5 is replaced with a note that the feature was removed, and B2's re-sync evidence and the "B2 + B5" commit grouping are updated.
- **Specs, Purpose paragraphs:** the Purpose text of `background-jobs`, `activity-recording` and `settings-page` names the re-sync. Delta specs can't change a Purpose, so those three paragraphs are edited directly in `openspec/specs/`.
- **No change:**
  - database schema or migrations; the job was in-memory only, and past migrations that mention `MalResync` stay as they are
  - reconciliation, the tiered metadata refresh, the airing-date refresh, build all series, the list import and the file import
  - the shared rewatch helpers (`MalListStatus.ApplyTo`, `MalStatusResolution`)
- **Out of scope:** the separate B2/R2/PF3 fixes in `docs/ISSUE_TRIAGE.md`, apart from B2's evidence line losing its re-sync part.
