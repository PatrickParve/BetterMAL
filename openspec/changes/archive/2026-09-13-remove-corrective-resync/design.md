## Context

**The feature today.** "Correct imported data" in the Settings page's Data tools group starts a background job:

| Piece | File | Role |
|---|---|---|
| Endpoint | `Controllers/SyncController.cs` `TriggerResyncFromMal` | `POST api/sync/resync-from-mal`: `ResyncProgress.TryBegin()`, then `ResyncTrigger.Signal()`, answers `202` with the `JobDto` |
| Signal | `Services/Sync/ResyncTrigger.cs`, `IResyncTrigger.cs` | `SemaphoreSlim(0,1)` |
| Loop | `Services/Sync/ResyncBackgroundService.cs` | waits on the signal, runs the service in a scope, `Fail(JobFailure.Describe(ex, "MyAnimeList"))` on a throw |
| Work | `Services/Sync/ResyncService.cs`, `IResyncService.cs` | full MAL list, then one full-detail fetch per anime. Upserts `AnimeMetadata` through `AnimeMetadataChangeDetector`, and applies `MalListStatus.ApplyTo` to every entry that isn't `PendingSync`, creating the missing ones |
| Tracker | `Services/Jobs/JobTypes.cs` `ResyncProgress` | a singleton, reported as `jobs.resync` by `AppStatusController` |
| UI | `pages/SettingsPage.tsx`, `api/client.ts`, `api/types.ts`, `context/AppStatusContext.tsx` | action block, trigger call, DTO field, job-key order |

**What stays after deletion, and depends on nothing being removed:**
- `MalListStatus.ApplyTo(UserAnimeEntry)` is still called by `ToUserAnimeEntry`, which serves the import, reconciliation and held changes, and directly by `HeldChangeService.cs:224`.
- `MalStatusResolution.ResolveAgainstLocal` is still called by `ReconciliationService`.
- `IAnimeMetadataChangeDetector` is still used by `MetadataRefreshService` and every other metadata writer.
- `JobProgressTracker`, `JobFailure` and `BackgroundJobRunner` still serve the other jobs.

**Constraints:**
- The backend targets .NET 10. The local SDK is 9.0, so it builds and tests only in `mcr.microsoft.com/dotnet/sdk:10.0`, from a copy under `/private/tmp`, because Docker can't bind-mount `~/Documents`.
- The frontend has no test runner. It builds with nvm's Node 22 and is checked by hand in the running app.
- `openspec/specs/metadata-refresh/spec.md` and `artwork-selection/spec.md` have **uncommitted** edits in the working tree from the 2026-09-13 triage fixes. This change's deltas for those two capabilities were copied from the working-tree text.

## Goals / Non-Goals

**Goals:**
- Remove every code path, registration, DTO field, UI element and test that exists only for the corrective re-sync.
- Leave no spec requirement, scenario, Purpose line or tracked doc describing it as present.
- Keep every general rule that a re-sync-only requirement also stated, where the rule still holds for other paths.
- Leave every remaining job's behaviour, wording and endpoints exactly as they are.

**Non-Goals:**
- Adding anything in its place: no "refresh all metadata now" action, and no change to the tiered refresh's cadence or caps.
- Fixing B5 separately. With the feature gone, B5 can't happen.
- The B2/R2/PF3 fixes in `docs/ISSUE_TRIAGE.md`.
- Rewording the Data tools group's "corrective and backfill jobs" hint or the page subtitle's "corrective tools" (D5).
- Editing `ISSUES.md`, or the local `docs/` notes other than `ISSUE_TRIAGE.md` (D6).

## Decisions

### D1. Delete, don't deprecate

The endpoint, services, trigger and tracker are removed outright, in one change. Nothing outside the app calls the endpoint: the frontend is the only client, and it ships in the same build.

**Alternative considered:** keep the endpoint and answer `410 Gone` for a release. Rejected, because there is no external consumer to warn.

### D2. The shared rewatch-rule clause moves to "Reconciliation does not demote a rewatch"

"A corrective re-sync does not demote a rewatch" (`mal-write-sync`) mostly describes the re-sync. Two of its sentences, though, are general:
- every read-back onto an existing entry resolves the incoming status through **one shared rule**;
- that rule has **no effect on an entry being created**.

Both still hold: `HeldChangeService` applies status through `ApplyTo`, and reconciliation through `ResolveAgainstLocal`. Two held-change requirements in the same spec (`mal-write-sync/spec.md:207, 265`) point back at "the same rewatch-preserving rule every other MyAnimeList read-back applies".

The requirement is removed, and those two sentences move word for word into "Reconciliation does not demote a rewatch", which already defines the rule. No scenario is added for them, because the held-change decline scenario already tests the shared rule on a second path.

**Alternatives considered:**
- *Delete the whole requirement, as first proposed.* Rejected: the one statement that every read-back shares a single rule would disappear.
- *Keep it renamed as a cross-path requirement.* Rejected: that restructures the spec rather than cleaning it up.

### D3. Example scenarios move to a remaining job; re-sync-only scenarios are removed

The deciding question for each scenario is whether it tests a general rule with the re-sync as its example, or tests the re-sync itself.

| Scenario | Kind | Action |
|---|---|---|
| `background-jobs` "A failure carries its reason" | general | now run full reconciliation (the runner's failure wording is `"MyAnimeList"`) |
| `background-jobs` "A second press while running starts nothing" | general | now run full reconciliation |
| `background-jobs` "Leaving the page keeps the run" | general | now the full airing-date refresh |
| `background-jobs` "An outcome is reported as seen" | general | now the build-all-series run |
| `navbar-settings-status` "A failed job raises it" | general | now the full airing-date refresh failing because AniList couldn't be reached (the wording it really uses) |
| `settings-page` "An action reads as an action" | general, and already names two other jobs | the re-sync is dropped from the list |
| `settings-page` "Correct imported data shows its bar at once" | re-sync only | removed; "Airing dates shows its bar at once" already covers the rule |
| `settings-page` "The reviewable and the immediate are tellable apart" | re-sync only (it contrasts the two pull paths) | removed; the requirement body still makes every explanation say whether it reviews or applies immediately |
| `settings-page` "Silent entry creation is called out" | re-sync only | removed; the body's rule, that a control which creates entries says so, stays |
| `settings-page` "Sync now is not confused with a re-sync" | general (about sync now) | renamed "Sync now says it only pushes", content unchanged |
| `settings-page` "The sync paths say they leave no history" | general | renamed "Accepting a diff says it leaves no history", with the re-sync dropped from its WHEN |
| `activity-recording` "The corrective re-sync records nothing" | re-sync only | removed; "An accepted diff records nothing" covers the same shape |

The swapped examples spread across jobs that have no scenario in that requirement yet, so each requirement doesn't lean on a single job.

### D4. Purpose paragraphs are edited in place

Delta specs can only carry requirements. The Purpose text of `background-jobs`, `activity-recording` and `settings-page` names the re-sync, so those three paragraphs are edited directly in `openspec/specs/` during apply. Archiving merges requirements only and leaves Purpose text alone, so the two kinds of edit can't collide.
- `settings-page`'s example "a several-minute MAL re-sync" becomes "a minutes-long airing-date refresh".
- The other two paragraphs lose the re-sync from their lists, and "three sync paths" becomes "two".

### D5. Data tools keeps its "corrective and backfill" wording

The airing-date refresh ("in case something looks wrong") and the single-anime force-refresh still correct cached data, and build all series backfills. The group hint, the page subtitle, and the Files group's "SHALL NOT hold … a corrective or backfill job" all stay accurate, so none of them change.

### D6. Docs: tracked docs are updated, local notes only where asked

- **`SETTINGS.md`** is tracked and will be public. It gives the re-sync its own section, a quick-reference row, and a mention under "Re-authorize with MAL" ("reconciliation/re-sync fail immediately"). All of that is removed or reworded.
- **`CODE_GUIDE.md`** is tracked. Every re-sync mention there goes: the MAL endpoint table, the HTTP API table, the job and Sync sections, the `SettingsPage` row, and the background-jobs table.
  - A line that also names other callers loses only the re-sync.
  - A line describing a pattern "like the re-sync" is re-pointed at a job that remains.
- **`docs/ISSUE_TRIAGE.md`** is gitignored. It is updated as the request asks: B5 is superseded, B2's re-sync evidence goes, and the "B2 + B5" commit grouping becomes B2 alone.
- **Left alone:**
  - the other local notes in `docs/`, as the request names only `ISSUE_TRIAGE.md`;
  - `ISSUES.md`, a historical list that the triage already supersedes.

## Risks / Trade-offs

- **[No instant whole-list metadata refresh]** A mapping fix reaches an old, finished show only when its tier comes due, up to 28 days later. → Accepted (see proposal). A single anime can still be refreshed straight away from its detail page or with the force-refresh picker.
- **[A tab left open across the upgrade]** An already-loaded page still runs the old bundle. It reads `jobs.resync.phase` from a status that no longer has the field, and may throw until it is reloaded.
  - Reloading fixes it.
  - The old tab can't send `resync` in a seen report either: a restart leaves every job not started, and a job that never ended has nothing to report.
  - → No mitigation beyond a reload. This is a single-user, same-build deployment.
- **[Rewatch rule coverage]** `ResyncServiceRewatchingTests` goes with the service. → The rule stays covered by `MalMappingExtensionsTests`, `ReconciliationServiceRewatchingTests`, `HeldChangeServiceDeclineTests` and `HeldChangeServiceSelfClearingTests`. Apply confirms they still pass.
- **[Uncommitted spec edits underneath the deltas]** The `metadata-refresh` and `artwork-selection` deltas carry text from the working tree. If those working-tree edits were discarded before archiving, archiving would bring them back through the MODIFIED blocks. → Commit the pending triage spec edits before this change is archived.

## Migration Plan

- No data migration: job state was in memory only, and no table or column belonged to the re-sync. Past migrations that mention `MalResync` stay as history.
- Deploy is the usual `docker compose build` of backend and frontend together.
- Rollback is a git revert of the change's commit.
