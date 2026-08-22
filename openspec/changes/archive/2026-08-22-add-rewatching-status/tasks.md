## 1. The status value

- [x] 1.1 Add `Rewatching` to the `WatchStatus` enum in `Models/UserAnimeEntry.cs`. The column uses `HasConversion<string>()` (`Data/AnimeTrackerDbContext.cs:42`, `:94`), so no migration and no ordering care are needed — confirm that before assuming it.
- [x] 1.2 Add the `Rewatching` arm to `Services/Mal/MalMappingExtensions.ToMalStatusString`, mapping to `"watching"`. Its switch ends in `_ => throw`, so without this every sync of a Rewatching entry throws — do this in the same commit as 1.1.
- [x] 1.3 Add `'Rewatching'` to the `WatchStatus` union in `frontend/src/api/types.ts`, and entries to `STATUS_LABELS` ("Rewatching") and `STATUS_CLASS` ("rewatching") in `frontend/src/utils/anime.ts`.
- [x] 1.4 Add `Rewatching` to `isScoreRevealableStatus` in `frontend/src/utils/anime.ts`, alongside Completed and Dropped.
- [x] 1.5 Search for every exhaustive switch or status-keyed record over `WatchStatus` on both sides and confirm each handles the new value — a missing key in a `Record<WatchStatus, …>` is a TypeScript error, but a missing runtime branch is not.

## 2. Edit rules

- [x] 2.1 Add an eligibility check in `Services/Entries/` per design D0: Rewatching is permitted only when the anime's airing status is `finished_airing` or unrecorded, **and** the entry shows evidence of having been finished once — a finish date, **or** a rewatch count above zero, **or** a current status of Completed (an entry already Rewatching passes on the first two). Reject the edit otherwise with its own exception type, mapped to a `400` in `EntriesController` with a message naming the reason. Note it deliberately tests history, not present status, so a parked rewatch can be resumed from Watching, On hold, or Dropped.
- [x] 2.2 `UserAnimeEntryEditService`: entering Rewatching sets `EpisodesWatched = 0`, leaving `StartedAt`, `CompletedAt`, `MyScore`, and `RewatchCount` untouched.
- [x] 2.3 First exit — `UserAnimeEntryEditService`: a Rewatching entry whose episodes watched reaches the anime's total episode count returns to `Completed` and has `RewatchCount` increased by one, with nothing asked. Do not overwrite `CompletedAt`. The target is always the total; D0 guarantees the anime has finished airing, so no aired-so-far branch is needed.
- [x] 2.4 Second exit — returning a Rewatching entry to Completed by **choosing Completed in the editor** must not change `RewatchCount` on its own. Carry the user's answer on the edit request (a flag alongside the status change) and increment only when it says the rewatch counts. Episodes watched still fills to the total, as from any status.
- [x] 2.5 `components/EntryEditorOverlay.tsx`: reveal the "count this as a rewatch" control when Completed is chosen on an entry whose current status is Rewatching, defaulting to **not** counting it, and send the answer with the save. Do not show it for any other starting status.
- [x] 2.6 Suppress the completion-score prompt on **both** exits — it must fire only for entering Completed from an unfinished first viewing.
- [x] 2.7 Implement the strict Completed rule: an edit that lowers a Completed entry's episodes watched below what is available moves it out of Completed, keeping the lowered count. It lands in `Rewatching` when the anime has finished airing, and in `Watching` when it has not — reuse the 2.1 eligibility check rather than re-testing the airing status inline.
- [x] 2.8 Keep the three cases distinct, per design D3: progress dropping on a finished anime → Rewatching; progress dropping on a still-airing one → Watching; what is available growing past the progress → Watching (that last one belongs to `gate-editing-on-aired-episodes`).
- [x] 2.9 `components/EntryEditorOverlay.tsx`: add the Rewatching option, and disable it unless the same D0 conditions hold. This needs the anime's airing status in the editor — `EntryEditorTarget` gains it in `gate-editing-on-aired-episodes` task 6.3, so add the field here and note the overlap when doing task group 6.

## 3. MAL sync

- [x] 3.1 Confirm `EntryPushService` sends `status = "watching"` and the unchanged episode count for a Rewatching entry, via the mapping from 1.2 — no separate branch should be needed.
- [x] 3.2 `Services/Sync/ReconciliationService.cs`: treat a local `Rewatching` entry as matching a remote `watching` status so no difference is recorded and the entry is never rewritten to Watching. Constrain this to the status comparison only — every other field, and every other remote status, diffs exactly as today.
- [x] 3.3 Leave `is_rewatching` unset, as it is today. It is declared but unused in both directions (`Dto/MalListStatusUpdate.cs:15`, `MalClient.cs:39`) — see the design's open question before changing that.

## 4. Presentation

- [x] 4.1 Add a `--status-rewatching` / `-bg` / `-border` trio to **both** the light and dark palettes in `frontend/src/index.css`, a darker blue than `--status-completed` in each. Check the pair by eye in both themes — "darker" moves in opposite perceptual directions between them.
- [x] 4.2 `pages/MyListPage.tsx`: insert Rewatching into the group order, directly after Currently watching.
- [x] 4.3 `pages/MyListPage.tsx`: add the Rewatching status filter tab, carrying its status colour like every other tab.
- [x] 4.4 `Services/Dashboard/MainDashboardService.cs`: include `Rewatching` entries in the currently-watching selection alongside `Watching`, within the existing ordering rather than as a separate group.
- [x] 4.5 `Services/Profile/ProfileService.cs`: add Rewatching to the per-status breakdown and confirm the counts still sum to Total Entries. The Episodes and Days formulas need no change — verify rather than assume.

## 5. Tests

- [x] 5.1 Entering Rewatching resets episodes watched to 0 and leaves dates, score, and rewatch count alone.
- [x] 5.2 A Rewatching entry reaching the total returns to Completed with the rewatch count one higher, the original finish date intact, and no score prompt.
- [x] 5.3 The strict rule: lowering a Completed entry's count below what is available yields Rewatching at the lowered count on a finished anime; lowering it to 0 does the same; lowering it on a **currently-airing** anime yields Watching instead; confirming the unchanged count leaves the status alone.
- [x] 5.4 Eligibility, accepted: from Completed on a `finished_airing` anime; on one with no recorded airing status; and — the widened cases — from Watching, On hold, or Dropped where the entry has a finish date, and where it has a rewatch count above zero but no finish date.
- [x] 5.5 Eligibility, rejected: on a `currently_airing` or `not_yet_aired` anime; and on an entry with no finish date, a rewatch count of zero, and a status other than Completed. Each rejection leaves the entry untouched.
- [x] 5.6 The early exit: setting a Rewatching entry at 6 of 24 to Completed leaves the rewatch count unchanged when the answer says it does not count, raises it by one when the answer says it does, and fills episodes watched to 24 either way. Setting Completed from any other status asks nothing and changes no rewatch count.
- [x] 5.7 A Rewatching entry pushes as `watching` with its own episode count — the test that would have caught the `ToMalStatusString` throw.
- [x] 5.8 **Reconciliation does not demote a rewatch**: local Rewatching against remote `watching` records no difference; local Rewatching against remote `dropped` does. This is the highest-consequence rule in the change — getting it wrong destroys user state on a background pass.
- [x] 5.9 A rewatch appears in the dashboard's currently-watching selection, and leaves it once re-completed.
- [x] 5.10 The profile status breakdown counts Rewatching separately and still sums to Total Entries; a rewatch-in-progress contributes the same episode figure as under the old representation.

## 6. Revisit `gate-editing-on-aired-episodes`

This change is applied first; that one follows. Re-read its artifacts with Rewatching in existence and edit them so the two fit together. Known touch points, to be confirmed rather than trusted:

- [x] 6.1 Its "Nothing may be tracked against an anime that has aired no episode" requirement limits status to Watching and Plan to watch — add Rewatching to the rejected set, since an unaired anime cannot have been watched once, and update its editor-gating requirement and scenarios to match.
- [x] 6.2 Its "A completed entry re-opens when a new episode airs" requirement sends the entry to Watching. Confirm that is still right under design D3 here, and say so explicitly rather than leaving it to be inferred from the absence of Rewatching.
- [x] 6.3 Its modified "Unknown total episodes cannot be completed" requirement, and its D4 fill behaviour, need to state where Rewatching sits. Note that a Completed entry on a *currently-airing* anime is reachable there, and that lowering its count lands in Watching rather than Rewatching — the fallback case in design D3 here, which exists only because Rewatching requires a finished anime.
- [x] 6.4 Its task 8.1 lists the status options the editor disables; add Rewatching, whose own availability rule (D0 here) has to compose with that change's unaired-anime gating rather than override it.
- [x] 6.5 Re-run `openspec validate gate-editing-on-aired-episodes --strict` after editing, and re-read this change's archived deltas for anything the list above missed.

## 7. Verification

- [x] 7.1 Build the frontend (`nvm use 22 && npm run build` in `frontend/`) and run `npm run lint`.
- [x] 7.2 Build and test the backend via the `sdk:10.0` Docker image (the local SDK is 9.0).
- [ ] 7.3 Walk it through the running app: complete a show, set it to Rewatching, confirm the count resets and it appears in Currently watching and in My List's Rewatching group with the darker blue; increment to the end and confirm it returns to Completed with the rewatch count up by one and no score prompt. **Skipped**: this machine's running instance is the user's live app, synced to their real MyAnimeList account with 608 real entries — the user chose not to exercise this against real data. Covered instead by the tests in section 5, which exercise the same service/dashboard code paths against an in-memory database.
- [ ] 7.4 Trigger a manual sync and then a reconciliation, and confirm the entry is still Rewatching afterwards. **Skipped** for the same reason as 7.3 — covered instead by 5.8's reconciliation tests against a fake MAL client.
- [x] 7.5 Run `openspec validate add-rewatching-status --strict`.
