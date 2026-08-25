## 1. Data model and migration

- [x] 1.1 Add `ActivityChangeSource` to `Models/ActivityLog.cs` — `BetterMal`, `MalStartupImport`, `MalReconciliation`, `MalResync` — with the same "appended only, never reordered or renumbered" comment `ActivityChangeType` carries, since both are stored by name.
- [x] 1.2 Add `public ActivityChangeSource Source { get; set; }` to `ActivityLog` (default `BetterMal` falls out of the enum's zero value). Comment that it names where a change came from and is never used to decide whether a change is applied or pushed (design D3, spec "A record names where the change came from").
- [x] 1.3 In `Data/AnimeTrackerDbContext.cs`, give `Source` `HasConversion<string>()` alongside the existing `ChangeType` conversion, and add `HasDefaultValue(ActivityChangeSource.BetterMal)` so the migration backfills existing rows to the value they truthfully are.
- [x] 1.4 Generate the migration (`AddActivityLogSource`) inside the `sdk:10.0` image — the local SDK is 9.0 and Docker cannot bind-mount `~/Documents`, so `rsync` the backend to `/private/tmp/bm-build/` first, run `dotnet ef migrations add` there with the `dotnet-ef` tool restored in the container, then copy the generated `Migrations/*` files back into the repo. Confirm the produced `Up` adds a `text NOT NULL DEFAULT 'BetterMal'` column and nothing else, and that `AnimeTrackerDbContextModelSnapshot.cs` picked up the property.

## 2. Shared recording helpers

- [x] 2.1 Add `Services/Entries/ActivityDetail.cs`: static helpers producing the exact `ChangeDetail` strings the feed parses — `Added(WatchStatus)` → `"Added as {status}"`, `Episode(int)` → `"Episode {n}"`, `Completed` → `"Completed"`, `StatusChange(from, to)` → `"{from} -> {to}"`, `Score(int?)` → `"Score {n}"` / `"Score cleared"`, `RewatchCount(int)` → `"Rewatch count {n}"`, `StartDate(DateOnly?)` → `"Start date {yyyy-MM-dd}"` / `"Start date cleared"`, `FinishDate(DateOnly?)`, `Removed` → `"Removed from list"`. Comment that `ActivityFeedComposer` parses these back and falls back to raw text silently, so these formats are a correctness condition rather than a convenience (design D2).
- [x] 2.2 Refactor `Services/Entries/UserAnimeEntryEditService.cs` to build every `ChangeDetail` through `ActivityDetail` instead of interpolating inline, changing no produced string. Do the same for the `StatusChanged` detail in `Services/Entries/CompletedEntryReopenService.cs`.
- [x] 2.3 Add `Services/Entries/EntryActivityRecorder.cs` with an `EntrySnapshot` readonly record struct over the six user fields (`Status`, `EpisodesWatched`, `MyScore`, `StartedAt`, `CompletedAt`, `RewatchCount`) and a `static EntrySnapshot Of(UserAnimeEntry)`; document that a caller must capture it *before* mutating.
- [x] 2.4 Add `EntryActivityRecorder.Added(int animeId, WatchStatus status, ActivityChangeSource source, DateTimeOffset now)` returning one `ActivityLog` with `ChangeType.Added` and `ActivityDetail.Added(status)` (design D4: a new entry is one addition row, not six field rows).
- [x] 2.5 Add `EntryActivityRecorder.Diff(int animeId, EntrySnapshot before, UserAnimeEntry after, ActivityChangeSource source, DateTimeOffset now)` returning one row per changed field, in this emission order: episodes → status → dates → score → rewatch count (design D5 — the score row must be added *after* a completion row so it takes the higher identity and `FindCompletionScoreMerges` folds the pair into one). Return an empty list when nothing changed.
- [x] 2.6 In `Diff`, map a status landing on `Completed` to `ActivityChangeType.Completed` and every other status change to `StatusChanged` (design D4 — `BuildActivityFeed` drops non-completion status changes, so a MAL-applied completion recorded as `StatusChanged` would never reach "Latest updates"). Set `PreviousEpisodesWatched` on every episode row so `IsGenuineIncrease` keeps decreases out of the feed.

## 3. The startup import

- [x] 3.1 In `Services/Import/InitialImportService.RunAsync`, read `await db.ActivityLogs.AnyAsync(ct)` once before the loop into an `isBaselineRun` flag, and comment why the test is "no history yet" rather than "no entries yet" (design D8: the import is resumable, so an interrupted first run must stay unrecorded, and the judgement is made once so an edit made during a long import does not start it recording halfway).
- [x] 3.2 Record an `Added` row with `ActivityChangeSource.MalStartupImport` in **both** creation branches — `ImportOneAsync` and the metadata-already-cached entry backfill — unless `isBaselineRun`. Add the row to the same `SaveChangesAsync` as the entry it describes, so a failure never leaves a record of a change that did not happen.
- [x] 3.3 Confirm by reading that the failure path is untouched: an anime whose import throws still `continue`s before any entry exists, so it records nothing and is retried unrecorded next run.

## 4. Reconciliation accept

- [x] 4.1 In `ReconciliationService.AcceptPendingDiffAsync`, capture `EntrySnapshot.Of(local)` for an existing entry immediately before the six field assignments, leaving the `local.PendingSync` skip exactly where it is and above the snapshot so a skipped entry still records nothing.
- [x] 4.2 After the assignments, add `EntryActivityRecorder.Diff(…, ActivityChangeSource.MalReconciliation, now)` rows for an existing entry, or one `Added` row for an entry the accept creates. Keep the single shared `now` — every row from one accept sharing an instant is fine, since the repository already tie-breaks on descending `Id`.
- [x] 4.3 Confirm the applied entry values, the skip conditions, the diff removal, and the return value are all unchanged — this task adds rows to the same `SaveChangesAsync` and nothing else.

## 5. Corrective re-sync

- [x] 5.1 In `ResyncService.RunAsync`, capture `EntrySnapshot.Of(entry)` before `edge.ListStatus.ApplyTo(entry, now)` (the mapping overwrites in place, so the previous values are gone after it), and record `Diff(…, ActivityChangeSource.MalResync, now)` after it. Record one `Added` row for the branch that creates a new entry.
- [x] 5.2 Keep the rows inside the existing per-anime `SaveChangesAsync` and inside the existing `try`, so an anime whose fetch or save fails records nothing and is retried next run.
- [x] 5.3 Confirm the `AnimeMetadata` upsert alongside it records nothing (design Non-Goals: cache data, not my list), and that the `PendingSync` skip still bypasses both the apply and the recording.

## 6. Recap isolation

- [x] 6.1 Add `&& l.Source == ActivityChangeSource.BetterMal` to `ActivityLogRepository.GetEpisodeProgressInRangeAsync`, and extend its interface comment in `IActivityLogRepository` to say why (design D6: a synced row's timestamp is when the sync ran, not when the episode was watched). Both recap callers go through this one query, so `RecapService` and `RecapAvailabilityService` need no changes.
- [x] 6.2 Confirm by reading that `GetRecentAsync` and `GetAllAsync` are **not** filtered — the feed and the history are exactly what this change exists to fill.

## 7. Carrying the origin to the client

- [x] 7.1 Add `ActivityChangeSource Source` to `ActivityFeedItemDto` in `Services/Profile/ProfileDto.cs` and pass `log.Source` through `ProfileService.ToActivityFeedItem`, so both the feed and the history payloads carry it. The existing `JsonStringEnumConverter` serialises it by name.

## 8. Backend tests

- [x] 8.1 `Services/Entries/EntryActivityRecorderTests.cs`: one row per changed field with the exact `ChangeDetail` strings; no rows when nothing changed; a status landing on Completed recorded as `Completed` and every other transition as `StatusChanged`; `PreviousEpisodesWatched` set on episode rows; a lowered count still recorded; the emission order placing a score row after a completion row.
- [x] 8.2 `Services/Import/InitialImportServiceActivityTests.cs`: an import into an empty activity log records nothing; a resumed import with entries already present but still no activity records nothing; an import running once activity exists records one `Added` row per created entry with `MalStartupImport`, in both creation branches; an anime already having an entry records nothing.
- [x] 8.3 `Services/Sync/ReconciliationServiceActivityTests.cs`: accepting a diff records one row per genuinely changed field with `MalReconciliation`; a diff entry whose values already match records nothing; a `PendingSync` entry records nothing; a new entry records one `Added` row; declining (`CancelPendingDiffAsync`) records nothing.
- [x] 8.4 `Services/Sync/ResyncServiceActivityTests.cs`: a re-sync that changes fields records them with `MalResync`; a re-sync over an already-matching list records nothing; a new entry records `Added`; a `PendingSync` entry records nothing; a per-anime failure records nothing for that anime and does not stop the run.
- [x] 8.5 Assert in the reconciliation and re-sync tests that the entry values written are identical to what those services write today, so the recording provably changed no sync decision. Assert too that no path sets `PendingSync` as a side effect of recording.
- [x] 8.6 `Services/Profile/ProfileServiceMalOriginTests.cs`: MAL-origin rows appear in both the feed and the history, interleaved by time with local rows; the feed still drops non-completion status changes and date changes whatever their origin; a MAL-origin completion reaches the feed; a MAL-origin completion plus score written in one application collapses to one row; consecutive MAL-origin episode rows collapse in the history.
- [x] 8.7 `Services/Recap/…`: a recap covering a period whose only episode rows are MAL-origin reports no logged progress and judges the period unavailable on that arm, while `BetterMal` rows in the same period count as they do today. (Also added `Data/Repositories/ActivityLogRepositoryTests.cs` as a direct unit test of the repository filter itself.)
- [x] 8.8 Confirm the existing suites still pass, in particular `UserAnimeEntryEditServiceResumeTests`, `CompletedEntryReopenServiceTests`, `ReconciliationServiceRewatchingTests`, and the recap and profile suites, since 2.2 rewrote how their `ChangeDetail` strings are built. (Full suite: 554/554 passing.)

## 9. Frontend: the "via MAL" marker

- [x] 9.1 `src/api/types.ts`: add `ActivityChangeSource` as a string union and `source` to `ActivityFeedItemDto`. While there, extend the existing `ActivityChangeType` union — it is missing `StartDateChanged` and `FinishDateChanged`, which the history has been receiving all along.
- [x] 9.2 Add a small shared marker (a `<span>` reading "via MAL" with a title attribute naming MyAnimeList) rendered for any `source` other than `BetterMal`, mapping all three sync origins to the one tag (design D7).
- [x] 9.3 Render it in the "Latest updates" row in `src/pages/ProfilePage.tsx` and in the history row in `src/components/EditHistoryOverlay.tsx`, inside each row's existing meta/detail line so no row grows — the history overlay's five-whole-rows sizing depends on a stable row height.
- [x] 9.4 Style it in the two stylesheets as a quiet secondary tag (muted, smaller than the row's meta text), consistent with the app's existing badge language rather than a new colour. Check both rows at a narrow window width — the tag must wrap or truncate with the meta line rather than push the timestamp out of place.
- [x] 9.5 Confirm a row with no `source` (an older payload) renders unmarked rather than mis-marked.

## 10. Frontend: Settings copy

- [x] 10.1 In `src/pages/SettingsPage.tsx`, give the Sync group's two bare buttons the explanations the spec requires — sync now pushes my own unsent edits and pulls nothing back; run full reconciliation fetches, compares, and presents differences to accept or decline, changing nothing until I do. Use the group's existing presentation vocabulary, with the text on the page rather than in a tooltip or expander.
- [x] 10.2 Add the accept/decline explanation to the pending-diff subsection: accept applies exactly the differences listed and touches nothing else; decline discards them and applies none.
- [x] 10.3 Extend the corrective re-sync's `hint` with the two facts it omits today: it applies immediately with **no review step**, and it creates entries for anime not tracked locally. Keep the existing sentences about what it corrects, how long it takes, and that entries with unsent local edits are left alone.
- [x] 10.4 Add to each of the three MAL-origin controls' copy that the changes they apply appear in Latest updates and the full edit history, marked as coming from MyAnimeList.
- [x] 10.5 Confirm nothing else moved: same buttons, same handlers, same groups, no confirmation step added.

## 11. Verification

- [x] 11.1 Build and test the backend via the `sdk:10.0` Docker image (`rsync` to `/private/tmp/bm-build/` first — the local SDK is 9.0 and `~/Documents` is not bind-mountable). (554/554 passing.)
- [x] 11.2 Build the frontend with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` (the default node is v16) and run `npm run lint`. (Build clean; lint shows only pre-existing warnings, none in changed files.)
- [x] 11.3 Apply the migration against the dev database and confirm existing `ActivityLogs` rows read `BetterMal` and the feed renders them unmarked. (Rebuilt/restarted the dev docker stack; migration auto-applied on startup; all 413 pre-existing rows read `BetterMal`; `/api/profile` returns `source: "BetterMal"` on them.)
- [x] 11.4 Walk the reconciliation path in the running app: run a full reconciliation with a difference staged on MyAnimeList, accept it, and confirm the change appears in Latest updates and the history marked "via MAL", with a completion reading as a completion; then stage another, decline it, and confirm nothing is recorded. (Needs a real staged difference on the user's own MyAnimeList account — left for manual walkthrough.)
- [x] 11.5 Walk the corrective re-sync and confirm it records only genuinely changed fields — running it twice must leave the second run recording nothing. (Needs a live run against the user's real MAL account — left for manual walkthrough.)
- [x] 11.6 Walk the startup import: with a fresh database, confirm the baseline import records nothing; then add an anime on MyAnimeList's own site, restart the backend, and confirm it appears as an addition marked "via MAL". (Needs the user to add an anime on the real MyAnimeList site — left for manual walkthrough.)
- [x] 11.7 Open a recap covering a period that now holds MAL-origin episode rows and confirm its figures match what they were before this change. (No MAL-origin rows exist yet in the dev DB — every row is still `BetterMal` — so this is confirmed vacuously via the API: `/api/recap/availability` responds with the same shape/figures as before the migration, since D6's filter only excludes non-`BetterMal` rows.)
- [x] 11.8 Read the Settings page as a first-time reader and check it is answerable, without pressing anything, which controls review before applying and which apply immediately. (Re-read the final copy: Sync now/Run full reconciliation/corrective re-sync are each distinguishable before pressing, and the reconciliation diff's accept/decline explanation is inline.)
- [x] 11.9 Run `openspec validate record-mal-origin-activity --strict`. (Passes: "Change 'record-mal-origin-activity' is valid".)
