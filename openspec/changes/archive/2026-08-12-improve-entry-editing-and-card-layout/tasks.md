## 1. Persistence for removals

- [x] 1.1 Add `ActivityChangeType.Removed` as the last member of the enum in `Models/ActivityLog.cs` (appended, never reordered — values are persisted as ints) (D11)
- [x] 1.2 Add `Models/PendingEntryDeletion.cs`: `AnimeId` (PK), `Anime` navigation, `RequestedAt` — no relationship to `UserAnimeEntry`, since the entry is gone by the time a row exists (D1)
- [x] 1.3 Register `PendingEntryDeletions` in `AnimeTrackerDbContext` with `AnimeId` as the key and a cascade-delete FK to `AnimeMetadata`, matching how `TopAnimeSelection` is configured
- [x] 1.4 Create the EF migration via the `sdk:10.0` Docker image and confirm it adds only the new table (the enum change needs no schema change)

## 2. Removal write path (backend)

- [x] 2.1 Add `DeleteMyListStatusAsync(int animeId, CancellationToken)` to `IMalClient`/`MalClient`: `DELETE anime/{id}/my_list_status` with `MalAuthMode.Bearer`, treating 404 as success and throwing on any other non-success status (D3)
- [x] 2.2 Add `RemoveEntryAsync(int animeId, CancellationToken)` to `IUserAnimeEntryEditService`/`UserAnimeEntryEditService`: throw `EntryNotFoundException` when no entry exists; otherwise delete the `UserAnimeEntry`, add a `Removed` ActivityLog row, and add a `PendingEntryDeletion` — all in one `SaveChangesAsync` (D1)
- [x] 2.3 After that save, trigger the removal push directly rather than through `IEntrySyncScheduler` — a removed entry has nothing left to coalesce with (D2)
- [x] 2.4 `UpdateEntryAsync`: when it creates a new entry (`isNew`), delete any `PendingEntryDeletion` for that anime in the same transaction, so a re-add cancels a queued delete (D4)
- [x] 2.5 Add `PushPendingDeletionAsync(int animeId)` to `IEntryPushService`/`EntryPushService`: no-op when no pending deletion exists; on success delete the record, and if an entry now exists for that anime set `PendingSync = true` and schedule its sync (the re-add-mid-flight race, D4); on failure leave the record and log at warning, matching the edit path (D3)
- [x] 2.6 Extend `DrainPendingAsync` to drain pending deletions after pending edits, so the 2-minute retry service and manual "sync now" both cover removals with no changes of their own (D3)
- [x] 2.7 Add `DELETE /api/anime/{animeId}/entry` to `EntriesController`, returning 204 on success and 404 when the anime has no entry
- [x] 2.8 `ReconciliationService.RunAsync`: load the set of anime ids with a pending deletion up front and `continue` past any remote edge whose anime is in that set, before the "missing locally → Added" branch; extend the completion log line with the skipped-removal count (D1)

## 3. Dates and rewatch bound (backend)

- [x] 3.1 `UserAnimeEntryEditRequest`: add `StartedAt` and `CompletedAt` as `DateOnly?`, plus whichever mechanism distinguishes "absent" from "explicitly null" so a date can be cleared — pick the shape that reads cleanest against the existing DTO and note it in the file (D6)
- [x] 3.2 Add `ApplyDates` to `UserAnimeEntryEditService`, called after `ApplyStatus`: set or clear each date when the request carries it, skip when unchanged, and log an activity row per changed date
- [x] 3.3 Validate in `ApplyDates` that the resulting finish date is not earlier than the resulting start date (comparing post-edit values, not stored ones), throwing `ArgumentOutOfRangeException` so `EntriesController` maps it to 400 (D6)
- [x] 3.4 Confirm the automatic rules stay fill-if-empty after `ApplyDates` runs: `StartedAt` is only set when null, `CompletedAt` uses `??=` in both `ApplyEpisodesWatchedAsync` and `ApplyStatus`, and neither clears a date when status moves away from Completed (D5)
- [x] 3.5 `ApplyRewatchCount`: reject a value above 100 as well as below 0, with a message naming the limit
- [x] 3.6 Verify the spec's date scenarios by hand against a running backend: manual start date survives an episode increment; manual finish date survives completion; a cleared start date is re-filled by the next 0 → >0 increment; finish-before-start is rejected

## 4. Removals in the activity feed

- [x] 4.1 `ProfileService.BuildActivityFeed`: add `ActivityChangeType.Removed` to the pass-through case group, leaving `IsProgressEvent` untouched so a removal never collapses with an episode run (D11)
- [x] 4.2 Add `Removed: 'Removed from list'` to `CHANGE_TYPE_LABELS` in `frontend/src/utils/anime.ts` and `'Removed'` to the `ActivityChangeType` union in `frontend/src/api/types.ts`
- [x] 4.3 Verify a removal appears in both Latest updates and the full history with its title and picture intact, since `AnimeMetadata` outlives the entry

## 5. Entry editor — delete, dates, sizing

- [x] 5.1 Add `deleteEntry(animeId)` to `frontend/src/api/client.ts` using `fetchVoid` with `method: 'DELETE'`
- [x] 5.2 Add `onDeleted?: () => void` to `EntryEditorTarget` in `types.ts`; call it from the overlay after a successful delete, then `onClose()`
- [x] 5.3 `EntryEditorOverlay`: render a Delete action only when `entry` is non-null, opening an in-overlay confirmation step that names the anime and states the entry will be removed from MAL too; dismissing it returns to the editor unchanged
- [x] 5.4 `EntryEditorOverlay`: disable the whole form while a delete is in flight and show a failure message in the existing `entry-editor__error` slot without closing, matching how a failed save behaves
- [x] 5.5 `EntryEditorOverlay`: add the collapsed `<details>` "Dates" disclosure with start-date and finish-date `<input type="date">` fields seeded from `entry.startedAt`/`entry.completedAt`, sending each only when changed and blocking a save where finish precedes start (D6)
- [x] 5.6 `EntryEditorOverlay`: cap the rewatch-count input at `max={100}` alongside the existing `min={0}`
- [x] 5.7 `EntryEditorOverlay.css`: extend the shared `.entry-editor__field select, .entry-editor__field input` rule with `width: 100%`, `box-sizing: border-box`, and a shared height so every control matches, including the date fields and anything added later (D7)
- [x] 5.8 `EntryEditorOverlay.css`: style the delete action as destructive and visually separate from Cancel/Save, and style the confirmation step

## 6. Removal wired through the pages

- [x] 6.1 Verify `EntryEditorContext` needs no change beyond passing the target through — `onDeleted` rides on `EntryEditorTarget` like `onSaved` does
- [x] 6.2 `MyListPage`: pass `onDeleted` that drops the item from `setItems`, so the row disappears without a reload
- [x] 6.3 `TopAnimePage`: pass `onDeleted` that clears that row's `entry`, flipping its button back to Add
- [x] 6.4 `AnimeDetailPage`: pass `onDeleted` that clears the entry so the page shows its add-to-list actions again
- [x] 6.5 `HomePage` and `SeasonPage` need no `onDeleted` wiring: neither opens the entry editor today (no Add/Edit action on their rows — season/browse cards render through `AnimeCard` with no `actions` slot filled in), so there's no removal entry point on either page to wire. Confirmed by inspection; `proposal.md`'s Impact list corrected to match.
- [x] 6.6 Walk every editor entry point and confirm the removed anime is gone from that view, then reload the page and confirm it is still gone

## 7. My list rank column and single sort control

- [x] 7.1 `MyListPage.css`: change `.my-list-row__rank` to a fixed `4ch` width, left-aligned, keeping `tabular-nums`, so `#1` and `#100` both start at the same x and neither reaches the poster (D8)
- [x] 7.2 `TopAnimePage.css`: give `.top-anime-row__rank` the same fixed `ch` width and left alignment (D8)
- [x] 7.3 `MyListPage.tsx`: in grouped view with the All filter, render `renderSortControls()` once above the groups instead of inside each group header; keep it on the status title's line for every single-status view and for ranked view (D10)
- [x] 7.4 Confirm the hoisted control did not change the gap between a group's `<h2>` and its first row, in both grouped and ranked view, per `library-views`' "Consistent list placement across my-list sort modes" (D10)
- [x] 7.5 Confirm changing the sort in the All view reorders every status group

## 8. Full-height posters in list rows

- [x] 8.1 `MyListPage.css`: zero `.my-list-row`'s vertical and leading padding, add `overflow: hidden` and a `min-height` equal to today's row height, and make `.my-list-row__picture` (and its placeholder) `align-self: stretch; height: auto` with `object-fit: cover` (D9)
- [x] 8.2 `MyListPage.css`: keep the rank column's leading padding so ranked rows still have the rank inset from the card edge while the poster fills the height (D9)
- [x] 8.3 `TopAnimePage.css`: same treatment for `.top-anime-row` / `.top-anime-row__picture`
- [x] 8.4 `ProfilePage.css`: same treatment for `.profile-list-row` / `.profile-list-row__picture`, covering Latest updates and both divergence lists
- [x] 8.5 `EditHistoryOverlay.css`: same treatment for `.edit-history__row` / `.edit-history__picture`
- [x] 8.6 Check each of the five row styles with and without a picture, and confirm the status stripe on `.my-list-row`'s left border and the hover/focus states still read correctly with `overflow: hidden`
- [x] 8.7 Confirm no list got taller: row heights match what they were before the change

## 9. Verification

- [x] 9.1 Build the frontend with nvm's Node v22 (`nvm use 22 && npm run build`) and run `npm run lint`
- [x] 9.2 Build the backend via the `sdk:10.0` Docker image
- [x] 9.3 Delete an entry with MAL reachable and confirm it disappears locally, the ActivityLog row is written, and the `PendingEntryDeletion` row is gone once the push succeeds
- [x] 9.4 Delete an entry with MAL unreachable, restart the backend, and confirm the removal is still pending and is pushed by the retry sweep once MAL is reachable again
- [x] 9.5 Re-add an anime while its removal is still pending and confirm the pending removal is discarded and no delete is pushed
- [x] 9.6 Run reconciliation with a removal still pending and confirm the anime is not proposed as an addition
- [x] 9.7 Run `openspec validate --changes improve-entry-editing-and-card-layout`
