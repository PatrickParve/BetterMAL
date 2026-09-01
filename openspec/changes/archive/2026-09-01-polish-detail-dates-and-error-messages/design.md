## Context

Eight small defects across five surfaces, sharing one root in two places: the client throws the server's stated reason away (`fetchJson` raises `Error("<url> responded with 400")` and drops the `{ "error": … }` body every rejection carries), and the one place every episode increment funnels through swallows its failure on purpose (`CompletionPromptContext.setEpisodesWatched`'s `catch { return }`, commented "Leave the count as-is; the user can retry"). Everything else is presentation: a related-links row whose buttons come and go, an overlay titled in romaji while the rest of the app is in English, a date field that is whatever the browser feels like drawing, a filter that opens nothing, an overlay sized off `60vh`, and a chip whose value box carries `vertical-align: middle`.

Constraints worth naming up front:

- **The backend already gets the domain rules right.** `ApplyDates` compares post-edit values, allows either date alone, and allows same-day. Nothing about date validity changes server-side; only how the rejection is worded and how the client relays it.
- **The `{ "error": … }` body already exists** on every 400 from `EntriesController` and friends. This change reads it; it does not invent it.
- **`RelatedAnimeDto` has no English title** and cannot get one for free: `AnimeRelatedAnime.Title` is MAL's romaji from the relation node. `AnimeDetailService` already loads `AnimeMetadata` rows for exactly these anime ids to resolve media types, and `AnimeMetadata.EnglishTitle` is on those rows — so the title costs one more column on a query that already runs.
- **The `RankingOverlay` is shared** by the profile page and the recap page. Its sizing and dismissal change for both. The edit-history overlay sets the precedent for the sizing half — a height derived from row height rather than from the viewport — but not for the dismissal half: it keeps a close control where this one drops its own (D10).
- **`ScoreChip` is shared** by the top-anime showcase, the series page's averages, timeline cards and extra tiles. The alignment fix belongs there, not on one page.

## Goals / Non-Goals

**Goals:**

- No control in the app can fail silently: a rejected edit says why, wherever it was made.
- Date editing works the same in Safari as everywhere else, and a date can be cleared.
- The detail page's related-links row keeps one shape from anime to anime.
- The series page's "in my list" control shows my list when pressed.
- The two shared components (`RankingOverlay`, `ScoreChip`) are fixed once, for every caller.

**Non-Goals:**

- No change to what dates are valid, how they sync to MAL, or how the automatic start/finish rules fill them.
- No calendar-popover date picker (see D3) and no date library.
- No general-purpose toast/notification system for successes, progress, or informational messages — failure notices only.
- No change to which anime a prequel/sequel control targets, or to relation resolution.
- Nothing about the More overlay other than the title shown, and nothing about the See-all overlay's rows other than how many are visible and how it closes.

## Decisions

### D1. Absent prequel/sequel controls are `<button disabled>`, not styled spans

The dimmed control is a real `<button type="button" disabled>` carrying the same `anime-detail-page__related-link` class plus a `--disabled` modifier. `disabled` gives, for free and correctly, everything the spec asks for: not clickable, out of the tab order, reported as disabled to assistive technology.

*Alternatives:* a `<span aria-disabled="true">` (has to re-implement all three behaviours, and `aria-disabled` on a non-interactive element says nothing useful); rendering the `<Link>` with `pointer-events: none` (still focusable and still navigable by keyboard — the worst of both).

The row's wrapper condition (`hasSeriesRelation || prequel || sequel || …`) becomes unconditional, since two controls now always render. `Series`, `Main series` and `More` keep their existing come-and-go behaviour — the user asked for a stable prequel/sequel pair, and a dimmed `More` with nothing behind it would be a control that can never be enabled on that anime.

### D2. The English title rides along on the query that already runs

`AnimeDetailService.GetMediaTypesAsync` becomes `GetRelatedMetadataAsync`, projecting to a `RelatedMetadata(string? MediaType, string? EnglishTitle)` record keyed by anime id, and `RelatedAnimeDto` gains an `EnglishTitle` field filled from it. The client then uses the same `pickDisplayTitle(title, englishTitle)` helper every other surface uses, so the fallback rule is written once.

*Alternatives:* fetching each related anime to learn its English title (the `anime-detail` spec explicitly forbids extra requests for relation rows, and it would be dozens of requests per overlay); storing an English title on `AnimeRelatedAnime` at fetch time (MAL's `related_anime{node{…}}` selection can carry `alternative_titles`, but that widens the write path and leaves every already-stored row null until its owner is re-fetched — the metadata lookup covers those immediately).

A related anime we hold no `AnimeMetadata` row for keeps MAL's stored title, exactly as it keeps `"Unknown"` for a missing media type today.

### D3. A `DateField` of three selects, owning its own parts

New `frontend/src/components/DateField.tsx`:

```
value: string        // 'yyyy-MM-dd' or ''
onChange(next: string)
label / id wiring for the editor's <label>
```

It holds `{ year, month, day }` in local state rather than deriving all three from `value` on every render. That matters: a field being filled in passes through states that map to no date at all (year picked, month not), and a purely derived field would throw the user's first pick away the moment it emitted `''`. The parts re-seed from `value` when `value` changes from outside (a different entry, a clear).

- **Emit rule:** all three parts set → emit `yyyy-MM-dd`; otherwise emit `''`. So a partly filled field reads as empty, per the spec, without being an error state.
- **Day list:** `new Date(year, month, 0).getDate()` gives the days in the selected month, leap years included. With no year picked yet the list runs to 31 (an out-of-range day is corrected as soon as a year makes it wrong). Changing month or year clamps a now-invalid day down to the month's last day rather than silently emitting an invalid date.
- **Year range:** current year + 1 down to 1970, newest first, plus the stored value's own year if it falls outside — a date already on the entry must always be representable.
- **Clear:** the blank option in any part clears all three (per spec), and a trailing ✕ button clears the field in one action, disabled while the field is already empty.

*Alternatives considered and rejected:* keeping `<input type="date">` and adding a Clear button — the smallest change, but Safari/macOS's field is the actual complaint; a custom calendar popover — best feel, but it brings focus trapping, positioning, and keyboard grid navigation for a field that is opened rarely and behind a disclosure. The user chose the dropdown form.

### D4. The clear control sits inside the field's control row

`list-editing`'s "Entry editor controls share one size" requires the date field to match every other control's width and height. So the row is `[year][month][day][✕]` inside the standard control width: the three selects `flex: 1 1 0`, the ✕ `flex: 0 0 auto`. Putting Clear outside the row (or beneath it) would either overflow the shared width or add a second row height to the form.

### D5. `ApiError` in the client, thrown by the two existing helpers

```ts
export class ApiError extends Error {
  status: number
  reason: string | null   // the `{ "error": … }` body's message, when there was one
}
```

`fetchJson`/`fetchVoid` build it on `!res.ok`: read the body as text, `JSON.parse` inside a `try`, take `.error` when it is a non-empty string, `null` otherwise. `message` stays `reason ?? "<url> responded with <status>"`, so nothing that logs `err.message` gets worse, and every existing `catch {}` keeps working untouched — the class only adds fields to what was already an `Error`.

Body-reading is safe with the in-flight GET de-duplication: each caller already reads its own `res.clone()`, so consuming one clone's body affects no one else. Reachability reporting is unchanged — it happens in `performFetch`, before any of this.

*Alternative:* returning a result object instead of throwing. Rejected: every call site in the app is written around `try/catch`, and rewriting them all is a far larger change than adding two fields to the error.

### D6. Failure notices are a context + a mount, mirroring `ConnectionStatusNotice`

`ActionFailureProvider` (app-root) exposes `reportFailure({ title, reason })`; `ActionFailureNotice` renders the queue. Mounted in `AppShell` **outside** the `<Routes>` and **above** `CompletionPromptProvider`, so the increment path can reach it.

- Queue capped at 3, newest first, each auto-dismissing after ~8s, each individually dismissable.
- `role="status"` (not `alert`) — it must not interrupt what the user is doing.
- Cleared on `pathname` change, reusing the same "a route change is a page change" rule `Modal` already applies to overlays.
- Fixed **bottom-right** at `z-index: 95`. Bottom-centre is taken by `ConnectionStatusNotice` (z-index 90); 95 keeps notices under the modal backdrop's 100, which is correct because every action taken from inside an overlay reports inside that overlay (the entry editor and the completion prompt both do), so a notice never needs to be legible through a backdrop.

*Alternative:* raising the notice from each call site's own `catch`. Rejected — there are six increment call sites feeding one shared function, and the point of the capability is that a control added later is covered without asking.

### D7. The increment path reports, and stays otherwise unchanged

`CompletionPromptContext.setEpisodesWatched`'s `catch` becomes a `reportFailure` call naming the anime (`target.animeTitle`, already carried on `IncrementTarget`) and the reason (`err instanceof ApiError ? err.reason : null`), then returns as before. It still does not touch the count — the displayed value stays where it was and the notice is what accounts for it, which is exactly the spec's "the count does not move". The detail page's `handleAddToWatching` / `handleAddToList`, which today have a `try/finally` with no `catch` (so a rejection escapes as an unhandled promise rejection), gain the same treatment.

### D8. One rejection base class on the backend, one catch in the controller

Every entry-edit rejection becomes an `EntryEditRejectedException` (new base; the seven existing typed exceptions derive from it), and the five `ArgumentOutOfRangeException(nameof(request), …)` throws in `UserAnimeEntryEditService` become the same — which is what removes the `(Parameter 'request')` suffix the user pasted, since that suffix is `ArgumentException.Message`'s own doing, not ours. `EntriesController`'s nine `catch` blocks collapse to one, and its `catch (ArgumentOutOfRangeException)` stays as a backstop for anything not yet converted.

Message wording drops the `Anime {id}` prefix in favour of "This anime …", because these strings are now read by a person: the client knows which anime it acted on and says so itself. The typed exceptions keep their `animeId` parameter for future logging; only the message text changes.

*Alternative:* mapping status/message pairs in the controller rather than on the exception. Rejected — the reason belongs with the rule that raised it, and the service is where the rule lives.

### D9. "In my list" becomes apply/put-away, derived rather than stored

Two functions replace `toggleMineOnly`, over the same three pieces of state (`mineOnly`, `unfilteredGroups`, `collapsedGroups`) — nothing new is stored, so the restorable-view-state contract is unchanged:

- **pressed state** = `mineOnly && unfilteredGroups.size === 0 && at least one group open`. The third clause is the new part, and it is what makes the first press on a freshly opened page (all collapsed) do something visible instead of quietly turning a filter off.
- **activate** (not pressed): `mineOnly = true`, `unfilteredGroups = ∅`, and `collapsedGroups` set to `false` for every group holding at least one `entry != null` extra that `typeAdmits`, `true` for the rest.
- **activate** (pressed): every group collapsed, `mineOnly` left on.

This is deliberately the same shape as `toggleMediaType`'s "adding a type opens every group holding one", so the two section controls behave alike. Turning the filter *off* stays the job of Expand-all and of a group heading, which is what the existing spec already says those do.

### D10. The See-all overlay loses its close control entirely, and is sized by its rows

`RankingOverlay` loses its `.ranking-overlay__buttons` footer and gains nothing in its place: no ✕ corner control either. Dismissal is what `Modal` already provides to every overlay in the app — Esc, a click on the backdrop, and a route change — so the box holds the ranking and nothing else.

This deliberately does **not** follow `EditHistoryOverlay`, which keeps a ✕ because its own contents are interactive (a search field and two date bounds) and a click inside it is often a click on a control rather than a dismissal. The See-all overlay is a list of links: the whole area outside it is already a dismissal target, and a reader who opened it from a "See all" control is one Esc or one outside click from where they were.

*Trade-off accepted:* on a touch device there is no button to press — the dismissal is a tap outside the box. That is the same affordance the app's backdrop already offers everywhere, and the user asked for the control to go.

`max-height: 60vh` on the list becomes `calc(8 * var(--ranking-row-outer-h) + 7 * var(--ranking-row-gap))`, with the row's outer height derived from the same `--ranking-poster-h` the row itself is built from, so the two cannot drift.

The edit-history CSS documents the trap this hits — padding on a scroll container is not "spent" up front, so a padded scrolling list shows a sliver of the next row. The ranking list has no padding of its own today; if a frame is added it goes on a non-scrolling wrapper, per that precedent.

### D11. The chip's value row becomes a flex line

`.score-chip__value { display: flex; align-items: center; }`. That makes `.score-value` a flex item, and `vertical-align` does not apply to flex items — which is precisely the property (`vertical-align: middle`, set on `.score-value` so its button-only hidden branch does not sit high in a text line) that drops the MAL figure below the plain number beside it.

*Alternative:* removing `vertical-align: middle` from `.score-value`. Rejected — it is load-bearing everywhere `ScoreValue` sits inline in text (my list rows, top-anime rows, detail-page lines); the chip is the context that should stop depending on it.

### D12. Neither date may be set into the future, on both ends

`DateField`'s year/month/day lists never offer a choice that would produce a date after today, rather than offering the full range and rejecting the result: the year list stops at the current year (plus the incoming value's own year if it is already later, so an existing out-of-range value stays representable — D3's same rule, reused), the month list stops at the current month when the current year is selected, and the day list stops at today's day when the current year and month are both selected. Changing an earlier part (year, then month) re-clamps a later one that would now be in the future rather than leaving it stale.

`UserAnimeEntryEditService.ApplyDates` rejects a submitted `StartedAt`/`CompletedAt` later than today with the same `EntryEditRejectedException` the finish-before-start check already uses — checked only against a date the request actually touches (`HasStartedAt`/`HasCompletedAt`), so an entry with an untouched legacy date already in the future (there is no historical guarantee against one) is never rejected for a field the user didn't send.

*Alternative:* letting the dropdowns offer any date and rejecting a future one at save time, matching how finish-before-start works. Rejected for this rule specifically — finish-before-start depends on *two* fields' relationship, which can't be prevented by limiting either one's own option list, but a single field being in the future can be prevented outright by simply not offering the choice, which is less surprising than picking a valid-looking date and having it bounce.

### D13. A "Today" control alongside Clear

Each date field's control row gains a `Today` button beside `Clear`, setting all three parts to today's date in one action. Styled from the recap page's "Yearly" tab palette (`--family-year-*`) rather than the app's purple accent — that accent is reserved for hover/selection state elsewhere in this same form (D-below), and Today is an action, not a hovered or selected value — but at the tint/outline weight every other button in the popup uses, not the tab's own solid-gradient-plus-glow: that treatment is sized for a standalone mode switch, and reproduced at full strength here it read as oversized next to the form's other controls.

### D14. Vertical centering of a `<select>`'s own value has no flex/grid-based answer that works everywhere

Centering a fixed-height `<select>`'s displayed value on both axes was tried first as `display: flex; align-items: center; justify-content: center` directly on the select (Chromium and recent Firefox honor flex/grid layout on a select's own box once `appearance: none` is set). It rendered correctly in Chromium but broke in Safari/WebKit, which does not support styling a native select's internal box this way — confirmed by rendering both engines side by side.

Reverted to the classic, layout-mode-independent technique: `text-align`/`text-align-last: center` for the horizontal axis (unchanged from D3), and a `line-height` set equal to the control's own content-box height (`var(--entry-editor-control-height) - 2px` for the 1px top/bottom border) for the vertical one, with vertical `padding` zeroed so the line-height is what allocates the space. This depends on nothing but single-line text layout, which every engine handles identically. The rule needing to beat the shared `.entry-editor__field select { height; padding }` rule's specificity is why `.date-field__part`'s own selector is self-compounded (`.date-field__part.date-field__part`) rather than plain — the same technique D3/D4's width overrides already use.

### D15. Hover added across the entry editor, and where each new button borrows its palette

Every previously-bare control in the editor gains a hover state, not just the date fields: the status/score dropdowns and episodes/rewatch-count inputs pick up the app's standard `accent-bg`/`accent-border` tint (the same pair `.season-page__nav button`, `.pagination__page`, and others already use); Cancel gets the same tint since it carries no colour of its own; Rank (outline accent) and Delete (outline red) each get a light fill of their own colour on hover. Save and the confirmed-delete button, both solid-filled, keep their colour unchanged on hover — mirroring the recap page's own filled/active tabs, whose `--active:hover` rule is identical to `--active` — rather than picking up the plain-button tint, so a solid, "already-chosen" control doesn't visually change underneath the pointer.

The date-field parts (year/month/day) get the same `accent-bg`/`accent-border` tint on hover as every other dropdown in the form.

`Clear` is rendered as the word "Clear" rather than a `✕` glyph, styled to match Delete's red outline (`#e5484d` border and text, transparent background, a light red fill on hover) — both are "remove/undo a value" actions.

### D16. The Dates disclosure's field wrapper is a `<div>`, not a `<label>`

Each date field was wrapped in a `<label className="entry-editor__field">` — correct for every other field in the form, which each wrap exactly one control, but `DateField` renders five labelable elements (three selects, two buttons). A `<label>` with more than one labelable descendant implicitly associates with only the *first* of them (the year select): clicking anything inside the label that isn't itself a control — the "Start date"/"Finish date" text, or a gap between the boxes — silently moves focus to the year select, which then renders as if permanently hovered/focused no matter what the user actually clicked. Confirmed by comparing the two versions: clicking the field's own heading text focused the year select under the `<label>` and focused nothing under a plain `<div>`.

Switched to a plain `<div className="entry-editor__field">` for the Dates disclosure's two field wrappers only — every other field in the form keeps its `<label>`, since each of those genuinely wraps one control. Each of `DateField`'s five controls already carries its own `aria-label`, so nothing accessible is lost.

## Risks / Trade-offs

- **[The date field is more clicks than a native picker for a far-away date]** → The year list is newest-first and selects are type-to-jump, so a recent date is two or three keystrokes. The alternative the user rejected is the status quo.
- **[Three selects could break the editor's one-control-size rule]** → D4 fixes the row's total width and height to the shared control size; a scenario in the `list-editing` delta asserts it.
- **[Notices could pile up if many actions fail at once]** → Queue capped at 3, auto-dismiss, cleared on navigation.
- **[A notice could be hidden behind a modal backdrop]** → Accepted deliberately (D6): every in-overlay action reports inside its overlay, so nothing that raises a notice is reachable while a backdrop is up.
- **[Reworded backend messages could break a test asserting old text]** → No test asserts these strings today (checked); the exception *types* the tests use are unchanged, and the new base class is additive.
- **[The See-all overlay change reaches the recap page too]** → Intended, and specified there as well; both surfaces open the one component, and the two would otherwise drift.
- **[An overlay with no close control could strand a touch user]** → A tap anywhere outside the box closes it, which is `Modal`'s standard backdrop behaviour and is unchanged; the modal never fills the viewport, since the backdrop keeps 24px of padding around it and the box is capped at 640px wide and 85vh tall.
- **[Eight rows may exceed a short window]** → The modal's own `max-height: 85vh` clamps it, the same way it already clamps the five-row edit-history list. The row-derived height is what governs at ordinary window heights, which is what "no ninth row peeking" is about.
- **[`ApiError` changes what `fetchJson` throws]** → It is still an `Error` with the same message shape; every existing `catch` ignores the value entirely.
- **[A legacy entry could already hold a future date]** → D12's backend check only fires on a field the request actually touches (`HasStartedAt`/`HasCompletedAt`); an untouched stored date is never re-validated.
- **[A `<select>` centering fix that works in one engine but not another]** → D14 was caught exactly this way in review (Chromium fine, Safari broken) before it shipped, and the replacement was verified in both engines rather than one.

## Migration Plan

No database migration and no API-contract change beyond an added `englishTitle` field on related-anime rows (additive; an older client ignores it) and reworded rejection messages. Frontend and backend can ship independently in either order: the client's `pickDisplayTitle` falls back to `title` while `englishTitle` is absent, and `ApiError.reason` is simply `null` against a backend that has not been updated — the generic message is the documented fallback.

## Open Questions

None outstanding. The one branching decision — how to replace Safari's date field — was settled with the user in favour of the dropdown form (D3). Everything the user asked for afterward while using it — no future dates (D12), a Today control (D13), true centering (D14), hover feedback across the popup (D15), and the stray-focus bug in the Dates disclosure (D16) — is folded into this same change rather than a follow-up one, since it is the same surface.
