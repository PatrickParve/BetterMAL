## Context

Two clusters of work that only overlap in the files they touch.

**Entry editing.** `EntryEditorOverlay` is the app's single write surface, opened from every page through `EntryEditorContext`. It edits four fields (status, episodes watched, score, rewatch count) and sends only the ones that actually changed, comparing against values captured at mount. Everything it saves goes through `PATCH /api/anime/{id}/entry` → `UserAnimeEntryEditService`, which owns the app's business rules (date lifecycle, completion guard, activity logging) and then sets `pending_sync = true` and hands off to `DebouncedEntrySyncScheduler` (8s per-anime timer) → `EntryPushService` → `PATCH /v2/anime/{id}/my_list_status`. The durability contract is the `pending_sync` flag: the timer is only a coalescing mechanism, and `PendingSyncRetryBackgroundService` (every 2 min) plus manual "sync now" drain whatever the timer missed. `ReconciliationService` pulls the full MAL list weekly and holds a reviewable diff, skipping entries with `pending_sync = true` so an in-flight edit is never proposed for reversal.

Deletion has no representation anywhere in that chain, and the entry editor deliberately excludes the start/finish dates (`list-editing`'s "Editor excludes start/finish dates"), so a date the automatic rules got wrong can only be corrected on MAL's site — where it comes back as a reconciliation diff to accept. Note the existing spec text for `completed_at` ("cleared when status is changed away from Completed") does not match the shipped code, which never clears it; the deltas settle this in favour of the code's behaviour, which matches MAL and is now the only sane reading once dates are hand-editable.

**List-row layout.** Five row styles across three files (`.my-list-row`, `.top-anime-row`, `.profile-list-row`, `.edit-history__row`, and the divergence rows that reuse `.profile-list-row`) are all built the same way: a flex row with uniform padding, a fixed-size poster, and `align-items: center`. The my-list rank column is a 32px box, which a three-digit rank overflows into the poster.

## Goals / Non-Goals

**Goals**

- Delete an entry from inside the editor, with the same local-first durability guarantee every other edit gets.
- Hand-editable start/finish dates that the automatic rules respect.
- One consistent control size in the editor; a rewatch ceiling of 100.
- Rank and poster columns that hold their position at any rank length; one sort control in the All view.
- Full-height posters in every list-style row, at unchanged row heights.

**Non-Goals**

- No undo for a deletion — the confirmation step is the safeguard, and re-adding is a two-click path if the user changes their mind.
- No bulk delete, and no delete from list rows directly; the editor is the only entry point, matching how every other write works.
- No change to the debounce window, the retry interval, or the reconciliation schedule.
- No cleanup of a removed anime's `TopAnimeSelection` row — that table is an ordered preference over *scored* anime, so a row for an anime with no entry is inert, and keeping it preserves the user's ordering if the anime is ever re-added.
- No change to grid/poster cards (home carousel, season, airing) — this change is about list-*row* styles only.

## Decisions

### D1: A `PendingEntryDeletion` table, not a soft-deleted entry

A removal has to stay queued across restarts, but the flag that carries that role for edits (`pending_sync`) lives on the row being deleted. The options were a tombstone column on `UserAnimeEntry` (`deleted_at`, row kept until pushed) or a separate record.

Chosen: a separate `PendingEntryDeletion { AnimeId (PK), RequestedAt }` table, written in the same `SaveChangesAsync` as the entry's deletion. A tombstone column would leak into every read path in the app — my list, dashboard, profile, detail, reconciliation, stats all query `UserAnimeEntries` and would each need `WHERE deleted_at IS NULL`, with any missed one showing a deleted anime as still in the list. The separate table keeps "is it in my list?" answerable exactly as it is today (`the row exists`), and confines knowledge of pending removals to the sync layer plus reconciliation. No FK to `UserAnimeEntry` (the row it refers to is gone by definition); the FK is to `AnimeMetadata`, which is retained.

### D2: Removals push immediately, not through the debounce

`ScheduleSync` exists to coalesce a burst of edits to the same anime. A removed entry can receive no further edits, so there is nothing to coalesce and an 8-second window would only widen the gap in which MAL and local disagree. The DELETE endpoint therefore calls the push path directly (fire-and-forget, like the existing `airingRefreshTrigger.Enqueue`) and lets the response return without waiting on MAL.

An edit made moments before the delete may still have a timer running. It needs no cancellation: `PushIfPendingAsync` re-reads the entry from the database and returns early when it finds none, so a timer that fires after a deletion is a no-op and cannot re-create the entry on MAL.

### D3: `IEntryPushService` grows removal handling, so all three drain paths get it for free

`PushIfPendingAsync` / `DrainPendingAsync` are already the single place that knows how to talk to MAL for entry state, shared by the debounce timer, the 2-minute retry service, and manual "sync now". Adding `PushPendingDeletionAsync(animeId)` and draining pending deletions inside `DrainPendingAsync` means the retry sweep and "sync now" cover removals with no changes to their own code.

404 from MAL counts as success (`DELETE` on a list entry MAL doesn't have): the goal state is the anime being absent from the MAL list, and it already is. Any other failure leaves the `PendingEntryDeletion` row for the next sweep.

Ordering within `DrainPendingAsync`: deletions are drained *after* pending edits. An anime cannot be in both sets (the deletion removed its entry, and D4 discards the deletion on re-add), so the order is arbitrary for correctness — it is fixed only so log output reads predictably.

### D4: Re-adding an anime discards its pending removal

`UserAnimeEntryEditService.UpdateEntryAsync` creating a new entry for an anime deletes any `PendingEntryDeletion` for it in the same transaction. Otherwise a delete that failed while offline would fire on the next sweep and erase the entry the user has since re-created.

The narrow race — the DELETE already in flight to MAL when the re-add lands — is closed on the push side: after a successful delete push, if an entry now exists for that anime, the push service sets `pending_sync = true` on it and schedules a sync, so the re-added entry is pushed back to MAL rather than left as a local-only ghost.

### D5: Manual dates win by construction, not by a flag

Both automatic rules already read as "fill if empty" in code (`entry.StartedAt is null`, `entry.CompletedAt ??=`). Rather than tracking provenance ("was this date set by hand?"), the spec adopts that shape as the rule: automatic rules fill empty dates only, a value present is never overwritten, and clearing a date makes it eligible again. `ApplyStatus`'s `EpisodesWatched` auto-fill on completion is unaffected.

Alternative considered: a `dates_manually_set` flag so the auto rules could still correct a stale automatic date. Rejected — it adds a column and a concept to explain, to serve a case (user wants the automatic date back) already served by clearing the field.

### D6: The date fields are a `<details>`-style disclosure inside the form

The user asked for dates to appear only after clicking "Dates". A native `<details>/<summary>` gives keyboard and screen-reader behaviour for free and needs no state; the two `<input type="date">` fields inside participate in the same form and submit with it. Validation (finish ≥ start) is enforced on both sides: the overlay blocks the save and shows a message in the existing `entry-editor__error` slot, and `UserAnimeEntryEditService` throws `ArgumentOutOfRangeException` (already mapped to 400 by `EntriesController`) so the rule holds for any caller.

Wire format: `UserAnimeEntryEditRequest` gains `startedAt` / `completedAt` as `DateOnly?`. Since `null` already means "field not being edited" for every other field in that DTO, clearing a date cannot be expressed as `null` — the request carries the two dates as a nullable-of-nullable pair on the client (`startedAt?: string | null`, only present in the JSON when the user touched it) and the backend distinguishes "absent" from "explicit null" by binding to `Optional<DateOnly?>`-style wrappers or, more simply, by two extra booleans. The simplest workable version: send the date fields only when changed, and treat an explicitly present `null` as "clear", which `System.Text.Json` distinguishes from absent via a nullable property plus a `bool` set in a custom setter. Task 3.2 pins this down.

### D7: Uniform control sizing via one CSS rule, not per-control tweaks

`.entry-editor__field select, .entry-editor__field input` already share a rule; the size difference comes from selects sizing to their content and browsers giving them different intrinsic heights. Adding `width: 100%; box-sizing: border-box; height: 38px;` (one shared custom property for the height) to that existing selector covers current and future controls, satisfying the spec's "controls added later inherit the sizing" clause. `appearance` is left alone so the dropdowns keep their native affordance.

### D8: Rank column sized in `ch`, not pixels

`.my-list-row__rank` becomes a fixed `width: 4ch` (fits `#500`) with `text-align: left` so the `#` starts at the same x on every row and the poster after it starts at the same x, satisfying both halves of the user's request. `font-variant-numeric: tabular-nums` is already set, which is what makes `ch` sizing reliable. `.top-anime-row__rank` gets the same treatment for consistency (its ranks reach `#500` and are currently right-aligned in a 36px box, which shifts the `#` per row).

### D9: Full-height posters via fixed poster dimensions and zeroed row padding

For each row style: drop the row's vertical padding, add `overflow: hidden` so the poster's corners are clipped to the card radius, and give the poster explicit `width`/`height` (not `align-self: stretch; height: auto` with an `aspect-ratio` — tried first, but the combination produces a circular sizing bug in flex layout where the row inflates to the image's intrinsic size instead of clamping to the target height). The height matches the row's new content height (72px for my-list/top-anime, 56px for profile rows, 78px for edit history); the width is widened proportionally from the pre-change box (e.g. my-list/top-anime's old 40×56 becomes 52×72) so `object-fit: cover` crops by roughly the same amount as before instead of cropping much more aggressively into a taller, narrower box — the poster reads as a normal poster, just taller, rather than looking stretched. The row keeps its old height via `min-height` equal to today's computed height, so nothing on the page shifts — the user's choice of "keep current row height". Placeholder divs get the same treatment so pictureless rows stay aligned.

`align-items: center` stays on the row so the text and controls remain vertically centred. Ranked rows keep the row's leading padding on the rank column, so the poster sits after it rather than at the card edge.

My-list rows additionally keep a 10px gap between the status-colour stripe and an unranked row's poster, rather than the poster sitting flush against it — a follow-up user request once the fully-flush version shipped and read as the stripe and poster touching. This applies to my-list only: top-anime, profile, and edit-history rows keep the poster flush with the row's leading edge as originally specified, since none of them pair a coloured status border with an unranked leading poster the way my-list does.

### D10: One sort control in the All view, rendered outside the group loop

`MyListPage` renders `renderSortControls()` inside each group's `<h2>` header row. When `statusFilter === 'All'` and the view is grouped, the control is hoisted to a single header row above the groups instead. The existing `library-views` requirement "Consistent list placement across my-list sort modes" constrains the header-to-list offset, so the hoisted control must not change the spacing between a group's `<h2>` and its first row — the shared `.my-list-page__group-header` rule keeps that margin, and the hoisted control gets its own container above the first group.

### D11: Removals are logged as a new `ActivityChangeType.Removed`

The activity log's FK is to `AnimeMetadata` with cascade delete, not to `UserAnimeEntry`, so removal rows (and the anime's whole prior history) survive the entry's deletion with titles and pictures intact. `BuildActivityFeed`'s switch gains `Removed` as a plain pass-through case — it is not a progress event, so it never collapses with an episode run — and `CHANGE_TYPE_LABELS` plus the `ActivityChangeType` union on the client gain the matching member. Enum values are appended, never reordered, since they are persisted as ints.

## Risks / Trade-offs

- **A deletion is irreversible from inside the app, and the confirmation is the only gate** → The confirm names the anime, and the destructive action is styled distinctly and is not the default focus. Re-adding restores the anime with default status but not the old progress, so the copy says the entry will be removed from MAL too.
- **A pending removal that never succeeds leaves MAL and local permanently divergent, invisibly** → It is retried every 2 minutes forever and excluded from reconciliation, so it cannot be "resolved" by silently restoring the entry. Failures are logged the same way pending-sync failures are; a persistent one shows up as an anime that stays on MAL.
- **Reconciliation now has to consult a second table before proposing an addition** → One extra query per run (a set of anime ids), on a weekly job that already pulls the entire MAL list.
- **Hand-editable dates can produce combinations the automatic rules would never create** (finish date on a Plan-to-watch entry, start date in the future) → Only the start ≤ finish rule is enforced; everything else MAL itself allows, and inventing extra rules here would fight the reason the fields were added.
- **Zeroing row padding for full-bleed posters can shift adjacent content by a pixel or two** → Row heights are pinned with `min-height` to today's values, and each of the five row styles is checked with and without a picture.
- **`4ch` rank column assumes tabular figures** → `font-variant-numeric: tabular-nums` is already on both rank rules; the check is part of the layout verification task.

## Migration Plan

One EF migration adds `PendingEntryDeletions` (`AnimeId` PK/FK to `AnimeMetadata`, `RequestedAt`) and nothing else — `ActivityChangeType.Removed` is an appended enum value stored as an int, needing no schema change. The table starts empty, so there is no backfill and no data at risk. Rollback is the inverse migration plus reverting the code; a pending removal in flight at rollback time would be lost (the entry stays deleted locally, MAL keeps it), which the weekly reconciliation surfaces as an addition to review.

## Open Questions

- None blocking. The JSON encoding for "clear this date" (D6) has two workable shapes; task 3.2 picks whichever reads cleanest against the existing DTO, since both satisfy the spec.
