## Why

The entry editor is the app's one write surface, and it is missing the two things MAL itself offers — removing an anime from the list, and setting the start/finish dates — so those edits can only be made on MAL's own site, which then shows up here as a reconciliation diff. Its controls also don't line up: the dropdowns render shorter and narrower than the number inputs beside them. Alongside that, the list rows the app is built out of have three layout faults that are visible on every visit: a rank number reaching three digits slides under the poster next to it, the sort control repeats once per status group when "All" is selected, and every poster floats in a padded box instead of filling its card.

## What Changes

**Entry editor — removing an entry**

- The editor gains a Delete action that removes the anime from my list and, from there, from MAL. It appears only for an anime that is already in my list, and asks for confirmation before removing.
- Removal follows the same local-first, durable-sync model as every other edit: the entry disappears locally at once, and the MAL removal is retried in the background until it succeeds, so it works offline.
- A removal is recorded in the activity log and appears in the full edit history, so a deletion is not a silent hole in the record.
- A pending removal is never undone by reconciliation: an entry awaiting its MAL delete is excluded from the reconciliation diff, so a not-yet-pushed deletion cannot be resurrected by MAL still listing it.

**Entry editor — dates**

- The editor gains a collapsed "Dates" disclosure. Expanding it reveals editable start-date and finish-date fields showing the entry's current dates; either can be set or cleared.
- Manually set dates are authoritative: the automatic started-on-first-progress and finished-on-completion rules only fill a date that is empty, and never overwrite one.
- A finish date earlier than the start date is rejected.
- Dates are pushed to MAL with the rest of the entry, which the write path already sends.

**Entry editor — validation and layout**

- Rewatch count accepts 0 to 100 and is rejected outside that range.
- Every control in the editor — dropdowns and number inputs alike — is the same width and the same height, so the form reads as one column.

**My list**

- The rank column is wide enough for a three-digit rank, with the `#` at a fixed left edge, so rank numbers align with each other and posters align with each other on every row regardless of rank length.
- With the "All" filter selected, the sort control appears once above the list rather than repeating in every status group's header. Under a single-status filter it stays on that status's header line.

**List-row posters (my list, top anime, latest updates, full edit history, both opinion-divergence lists)**

- The poster fills its row from top edge to bottom edge, flush with the card's left edge, instead of sitting inset with padding around it. Rows keep their present height.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `list-editing`: adds removal of an entry from my list (confirmation, local-first with durable MAL removal, activity logged); replaces the "editor excludes start/finish dates" requirement with an expandable dates section, manual-date precedence over the automatic date rules, and start/finish ordering validation; bounds rewatch count at 100; requires uniform control sizing in the editor.
- `mal-write-sync`: adds the entry-removal push (`DELETE /v2/anime/{id}/my_list_status`) with the same retry-until-success and manual "sync now" handling as edits, and extends reconciliation's in-flight exclusion to entries with a pending removal.
- `data-persistence`: adds the pending-removal record that makes a removal durable across restarts, and adds removal to the change types the ActivityLog records.
- `library-views`: rank numbers keep a fixed column so three-digit ranks don't overlap the poster; the quick-filter control appears once above the list in the "All" grouped view instead of once per status group; my-list and top-anime rows render their poster full-bleed.
- `profile-stats`: latest-updates, full-edit-history, and opinion-divergence rows render their poster full-bleed; the latest-updates feed and full history report removals.

## Impact

- **Backend — new**: `PendingEntryDeletion` model + EF migration; `ActivityChangeType.Removed`; `IMalClient.DeleteMyListStatusAsync`; a removal path in the sync services (`EntryPushService`, `PendingSyncRetryBackgroundService`, `DebouncedEntrySyncScheduler` or a direct push).
- **Backend — modified**: `EntriesController` gains `DELETE /api/anime/{animeId}/entry`; `UserAnimeEntryEditService` gains removal plus date handling, the 0–100 rewatch bound, and manual-date precedence; `UserAnimeEntryEditRequest` gains `startedAt`/`completedAt`; `ReconciliationService` skips anime with a pending removal; `ProfileService` surfaces removals in the feed and history.
- **Frontend — API**: `deleteEntry(animeId)`; `UserAnimeEntryEditRequest` gains the two date fields; `CHANGE_TYPE_LABELS` gains the removal label.
- **Frontend — editor**: `EntryEditorOverlay.tsx` / `.css` (delete + confirm, dates disclosure, uniform control sizing); every caller of the editor needs an `onDeleted` path to drop the removed row (`MyListPage`, `TopAnimePage`, `AnimeDetailPage`, via `EntryEditorContext` — `SeasonPage` and `HomePage` don't open the editor today, so there's nothing to wire there).
- **Frontend — layout only**: `MyListPage.tsx` / `.css` (rank column, single sort control, full-bleed poster), `TopAnimePage.css`, `ProfilePage.css`, `EditHistoryOverlay.css`.
- **No breaking API change**: the PATCH contract only gains optional fields; the DELETE endpoint is new.
