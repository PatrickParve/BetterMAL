## Why

Three write paths change my list without me touching it: the startup auto-import (`InitialImportService`, which fires on every backend restart and quietly creates entries for anime missing locally), accepting a reconciliation diff (`ReconciliationService.AcceptPendingDiffAsync`), and the corrective re-sync (`ResyncService.RunAsync`). All three mutate `UserAnimeEntry` directly and write nothing to `ActivityLog`.

`ActivityLog` is the single source of truth behind both surfaces that report what happened to my list — the profile page's "Latest updates" box and its full edit-history overlay both read it, via `ActivityLogRepository` → `ProfileService`. Only `UserAnimeEntryEditService` (my own edits and removals) and `CompletedEntryReopenService` write to it. So anime that arrive, get overwritten, or get completed because of MAL are invisible: an anime simply appears in my list with no record of when or why. This is purely a write-side gap, and closing it at the three write paths fixes both surfaces at once with no change to how either reads.

Separately, the Settings page's sync controls do not say what they do. "Run full reconciliation" and "Run corrective re-sync" read alike, but one holds its findings for review and the other overwrites my list immediately and silently creates entries for anime it finds on MAL — which is the path that produced the anime that prompted this change.

## What Changes

- **An activity record carries where the change came from.** `ActivityLog` gains a `Source` — `BetterMal` for my own edits (what every existing row is), and `MalStartupImport`, `MalReconciliation`, or `MalResync` for the three MAL-origin paths.
- **The three MAL-origin paths record what they applied, field by field.** Not "this entry changed" but the same granularity my own edits already produce: an `Added` row for an entry that did not exist, and otherwise one row per field that actually moved — status, episodes watched, score, start date, finish date, rewatch count — each carrying the same human-readable detail text the local path writes, so the existing feed composer renders them with no change of its own. Reconciliation and re-sync get a before/after snapshot of the local entry at the point of applying, which neither takes today.
- **A status change to Completed from MAL reads as a completion**, exactly as a local one does, rather than as a generic status change — otherwise the most interesting MAL-origin event would be filtered out of "Latest updates", which shows completions but not other status changes.
- **The import that establishes the baseline records nothing.** An import running while nothing has been recorded yet is establishing what my list already is, not reporting events, so a fresh install does not open on hundreds of identical "Added" rows — and because the test is "no history yet" rather than "first run", an interrupted first import stays unrecorded when it resumes. Every import that runs once history exists — the catch-up case — records normally.
- **Both surfaces show MAL-origin entries in the same list**, same ordering, same collapsing, same phrasing, marked with a small "via MAL" tag so they read as one history with an origin rather than as a second system.
- **Recaps are unaffected.** The recap's logged-progress arm reads `BetterMal` rows only, so recap figures stay exactly what they are today rather than absorbing progress dated to whenever a sync happened to run.
- **The Settings page says what each sync control does** before it is pressed: which ones show me a review first, which one applies immediately, and what "immediately" covers — including that the corrective re-sync silently creates entries for anime not yet tracked locally.

**Not changing:** the startup auto-import still fires on every backend restart with no throttling, opt-out, or schedule change; the weekly reconciliation cadence and its accept/decline flow are untouched; the corrective re-sync still applies immediately with no review step. No sync decision changes — only what is recorded once a change has actually been applied.

## Capabilities

### New Capabilities

- `activity-recording`: the write-side contract for the activity log — which paths record to it, at what granularity, with which origin, and the rule that recording never influences what a path does or pushes anything back to MyAnimeList. It is cross-cutting by nature (three capabilities' write paths share one rule), in the same way `overlay-behaviour` owns a rule every overlay shares.

### Modified Capabilities

- `profile-stats`: "Latest updates" and the full edit history include MAL-origin entries, in one list with local edits, marked as coming from MyAnimeList; every existing filtering, collapsing, and phrasing rule applies to them unchanged.
- `settings-page`: every control that starts sync work, and the accept/decline actions on a pending diff, state what they will do — whether the change is reviewable or immediate, and what it touches.
- `list-recaps`: the logged-progress arm counts only progress recorded in the app, not progress observed on a later sync.

**Unchanged capabilities:** `initial-import` and `mal-write-sync` keep every requirement they have. What the import, reconciliation, and re-sync *do* to entries is untouched; only what they record about it is new.

## Impact

**Backend**

- `Models/ActivityLog.cs` — a `Source` field and the `ActivityChangeSource` enum, appended in the same never-renumbered way `ActivityChangeType` is.
- `Data/AnimeTrackerDbContext.cs` + a new migration — the column, stored as a string like `ChangeType`, with existing rows backfilled to `BetterMal`.
- A new shared change-recorder (`Services/Entries/`) holding the entry snapshot, the field-by-field diff, and the detail-string formats — the formats move here from `UserAnimeEntryEditService` so the two writers cannot drift apart and break `ActivityFeedComposer`'s parsing.
- `Services/Import/InitialImportService.cs`, `Services/Sync/ReconciliationService.cs`, `Services/Sync/ResyncService.cs` — each records what it applied.
- `Data/Repositories/ActivityLogRepository.cs` — the recap query filters to `BetterMal`.
- `Services/Profile/ProfileDto.cs`, `Services/Profile/ProfileService.cs` — the source travels to the client.

**Frontend**

- `api/types.ts`, `pages/ProfilePage.tsx`, `components/EditHistoryOverlay.tsx` (+ CSS) — the "via MAL" tag on a row.
- `pages/SettingsPage.tsx` — per-control explanations; the Sync group's two buttons currently have none at all, unlike the Data tools group's actions.

**Not affected**

`ActivityFeedComposer`, `ProfileService`'s feed building and history collapsing, `RecapWatchLog`, and the `/api/profile/activity` shape beyond one added field: MAL-origin rows are written in formats these already parse. `PendingReconciliationDiff` and `ReconciliationDiffChangeType` stay the pre-accept review structure they are — the new recording happens where a change reaches `UserAnimeEntry`, for all three paths alike. Nothing written here sets `PendingSync`, so no recorded row can push anything back to MyAnimeList.

**Discovered but out of scope:** `ResyncService` overwrites a local Rewatching entry's status with MAL's `watching`, and `AcceptPendingDiffAsync` does the same whenever some other field of a Rewatching entry differs — both demoting a rewatch, which `mal-write-sync`'s "Reconciliation does not demote a rewatch" forbids for the compute step. Neither is fixed here (no sync decision changes), but this change makes the demotions visible in the history for the first time.
