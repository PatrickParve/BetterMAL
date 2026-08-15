## Context

Four independent fixes that share one page. Three are frontend-only and touch the same two files; the fourth is a backend eligibility rule.

**Scroll restoration as it stands.** `useScrollRestoration` (mounted once in `AppShell`, above the routes) does two things against the current history entry's `PageSnapshot`:

- *Records*: a `scroll` listener that schedules a `requestAnimationFrame`, and inside that frame reads `window.scrollY` and writes it to the snapshot captured by the effect's closure.
- *Restores*: a `useLayoutEffect` that, on a `POP` to a known entry, retries `window.scrollTo(0, target)` each frame for up to 500 ms until the page is tall enough; on anything else it scrolls to the top. A `scroll` listener cancels the retry loop, using a `programmatic` boolean cleared one frame after each `scrollTo` to tell its own scrolling apart from the user's.

Both halves have a race, and both produce exactly the reported symptom — back-navigation landing at the top.

1. **Recording is deferred by a frame, and the frame isn't cancelled.** A scroll event fired shortly before a link click (trackpad momentum, or just scrolling then clicking) leaves a pending frame. Navigation happens; the effect's cleanup removes the listener but leaves that frame scheduled; the new route's layout effect runs `window.scrollTo(0, 0)`; then the pending frame runs, reads `window.scrollY` — now `0` — and writes it into **the departing entry's** snapshot, which its closure still points at. Going back then restores to `0`.

2. **The restore cancels itself.** Scroll events are dispatched asynchronously, after the frame that cleared `programmatic`. When the page isn't tall enough yet, `scrollTo` clamps, the resulting scroll event arrives late, `programmatic` is already `false`, and the loop is cancelled as if the user had scrolled — leaving the page wherever the clamp put it.

A third, less likely contributor: `pageStateStore` caps snapshots at 30 and evicts in **insertion order**, so a long session can drop the entry sitting one step back in the history stack.

**The strips.** Three near-identical horizontal strips in `ProfilePage.tsx`, each `overflow-x: auto` with `padding: 8px`, `scrollbar-width: thin`, and tiles at `flex: 0 0 calc((100% - 9 * 10px) / 10)` — ten across the visible width by construction. Ten tiles plus nine gaps resolve to exactly the content-box width in exact arithmetic, but the division rarely lands on a whole pixel; the rounded-up total overflows by a fraction of a pixel, which is enough to make the strip scrollable and show a scrollbar. Each strip has a `useDragScroll()` instance holding the element ref and the drag handlers, and none of them record their scroll offset anywhere.

**Top series eligibility.** `SeriesRankingIndex.EligibleSeries()` walks every stored series' members (each projection carries `IsMainLine`, `AiringStatus`, `EntryStatus`, `MyScore`, `MalScore`), keeps a series when any member has an `EntryStatus`, and computes `MainLineAiredCount = mainLine.Count(m => m.AiringStatus != "not_yet_aired")` for the existing client-side "Multi-entry only" toggle. Everything the new rule needs is already in that loop.

## Goals / Non-Goals

**Goals:**

- Back-navigation to the profile page lands where it was left, every time — including the horizontal strips.
- A strip with ten or fewer entries neither scrolls nor pretends it can.
- No scrollbars under the posters, with scrolling otherwise unchanged.
- Top series stops ranking franchises I have barely watched, with no new control to learn.

**Non-Goals:**

- No change to *when* a page counts as a restore, to background refresh, or to view-control restoration — only to how position is recorded and applied.
- No persistence across reloads: strip offsets are session/history state like everything else in `PageSnapshot`.
- No new Top series control, no user-facing setting, and no change to the existing "Multi-entry only" toggle's meaning.
- No change to ordering, tile contents, the tooltip, or the section's empty-state wording.
- No restoration of scroll position inside overlays or vertical lists (the activity feed, the history overlay) — out of scope here.

## Decisions

### 1. Record scroll synchronously, against the entry being displayed

The deferred frame is the bug, and the closure over `snapshot` is what turns it into a cross-entry write. Both go:

- The `scroll` handler writes `window.scrollY` immediately. It is a property read the browser has already computed for the event plus a field write — the `requestAnimationFrame` throttle was guarding against a cost that isn't there.
- It writes through a `keyRef` holding the current history entry's key (`pageStateStore.putScroll(keyRef.current, window.scrollY)`) rather than a captured snapshot object. `keyRef` is assigned **during render** of the hook, so it already points at the new entry before any layout effect of that navigation runs — which is before the fresh-visit `scrollTo(0, 0)`, and therefore before any scroll event that scroll produces. A stray event can then only land on the entry actually on screen, which is the correct place for it.

*Alternatives considered.* Cancelling the pending frame in the effect's cleanup fixes the leak but not the ordering — the effect cleanup for a passive effect runs after the next page's layout effects, so a frame scheduled and fired inside that window still writes `0` to the wrong entry. Recording only at navigation time (a `beforeunload`-style hook, or in the cleanup) reintroduces the "was the value already stale?" question that recording continuously was meant to answer.

### 2. Cancel a restore on user *input*, not on scroll events

Distinguishing "my scroll" from "their scroll" by watching `scroll` events cannot be made reliable — the events are asynchronous and carry nothing identifying. So watch the input instead: `wheel`, `touchstart`, and the scrolling keys (`keydown` for arrows, page up/down, home/end, space). Any of those cancels the retry loop; scroll events are no longer listened to during a restore at all, and the `programmatic` flag disappears with them.

The retry budget goes from 500 ms to 1500 ms. With the self-cancellation gone, a longer budget costs nothing when the target is reached early (the loop stops on the first frame the page is tall enough) and buys the profile page room to settle when a section's data is not seeded — e.g. a media-type tab whose fetch was never completed before leaving.

*Alternative considered.* Comparing the observed scroll position against the last programmatic target to classify each event: still guesswork when the two coincide, and it fails exactly in the clamped case that matters.

### 3. Strip offsets live in the snapshot under an explicit key, applied on restore only

`PageSnapshot` gains `strips: Map<string, number>` beside `scrollY`, with a `putStripScroll(key, stripKey, offset)` accessor mirroring `putScroll`. Not the `view` map: that map is `useRestorableState`'s, its values feed React state, and a scroll offset that re-renders the page on every frame of a drag is not what it is for. Not a second module either — one history entry's restorable state stays in one place.

`useDragScroll()` grows a required `restoreKey` argument and becomes the one place a strip's scrolling is handled:

- a `scroll` listener on the element records `el.scrollLeft` under that key, synchronously, same reasoning as decision 1;
- on a restore, a layout effect sets `el.scrollLeft` to the recorded offset before paint. Because `usePageData` seeds restored data synchronously, the tiles are in the DOM on that first paint and the offset is reachable immediately. The effect re-applies while the strip's `scrollWidth` is still short of the target and stops for good once the offset is reached or the user touches the strip — a cheap analogue of the page-level retry loop, without a timer;
- on a fresh visit it does nothing, leaving the strip at its start.

The keys carry the view control where one exists: `top-anime:${mediaType}`, `rewatched:${rewatchedMediaType}`, `top-series` — the same keys those sections already use for `usePageData`, so a restored tab gets its own offset and a tab never inherits another's.

### 4. "Fits" is decided by entry count, not by measuring

Ten tiles across is the strip's defining constant (spec: *"ten tiles across the visible width"*), so `items.length > STRIP_VISIBLE_TILES` is exactly the question "does this overflow", answered without touching the DOM. Over that count the strip renders with its scrolling class; at or under it, a `--fits` modifier sets `overflow-x: hidden` and `cursor: default`. The drag handlers stay attached and become inert — `scrollLeft` on a non-scrolling element does nothing — rather than being conditionally wired up.

`STRIP_VISIBLE_TILES = 10` is declared in `ProfilePage.tsx` with a comment pointing at the `flex` basis in the CSS that it must agree with. The duplication is a real (if small) coupling; it is the cheapest honest option, and both ends are two files apart in the same feature.

*Alternatives considered.* Measuring `scrollWidth > clientWidth` in a `ResizeObserver` measures the very sub-pixel noise that is the bug. Shaving a rounding epsilon off the tile basis (`... / 10 - 0.5px`) is a one-line CSS fix, but it leaves the strip a scroll container that merely happens to have nowhere to go, so the grab cursor and the scrollbar would still need separate handling — and it trades a documented constant for a magic fudge factor.

### 5. Hidden scrollbars via `scrollbar-width: none` plus the WebKit pseudo-element

`scrollbar-width: none` covers Firefox and Chromium; `::-webkit-scrollbar { display: none }` covers Safari, which does not implement the standard property. Both go on all three strips, replacing today's `scrollbar-width: thin`. The strips keep `overflow-x: auto` when they overflow, so wheel and drag scrolling are untouched. The page's vertical lists keep `scrollbar-width: thin` — they are lists of rows where the bar is the only affordance, and the strips have drag-scrolling and posters that visibly continue past the edge.

### 6. The watched-coverage rule is server-side, in the existing eligibility check

The rule has no control, so the client never needs the excluded series — filtering them in `SeriesRankingIndex.EligibleSeries()` keeps the payload honest ("here is what Top series lists") and puts the rule next to `MainLineAiredCount`, which it shares its "has this actually aired" definition with:

```
mainLineAired   = mainLine.Where(m => m.AiringStatus != "not_yet_aired")
mainLineInList  = mainLineAired.Count(m => m.EntryStatus is not null)
if (mainLineAired.Count >= 2 && mainLineInList <= 1) skip
```

Client-side filtering was the alternative (it is where the single-entry toggle lives) and is rejected on the same grounds: it would need a new DTO field existing only to hide rows the user cannot unhide, and it would put two rules with different lifetimes — one permanent, one a view control — in the same place.

The DTO is unchanged. `MainLineAiredCount` stays for the "Multi-entry only" toggle; no `MainLineWatchedCount` is added, because nothing on the client would read it.

### 7. Any status counts as coverage; the threshold is "at most one"

*Any status* — plan-to-watch included — is the user's call, and it reads consistently: the rule asks whether a franchise is one I follow, and a season queued up is a season I have taken a position on. It also fails safe: a rule that ignored plan-to-watch would hide a franchise the moment its next season is queued but not started.

*At most one* rather than *exactly one* extends the literal ask by one case: two or more aired main-line entries with **none** in my list — a series known only through an extra. That series is in the section today, ranked on a main-line average computed from entries I have never touched; it is the same defect as the one-of-three case, only more so. Note this narrows an existing scenario in the `profile-stats` spec ("A series I only know through an extra"), which is why that requirement is modified rather than extended.

### 8. Nothing new is said when the rule empties the section

If the rule excluded every series, the section would fall to its existing "series are still being discovered … build all series from my list" message, which would be slightly misleading. No fourth empty state is added for it: the rule cannot be switched off, so there is no action to offer, and reaching that state needs a list where no franchise has two watched aired seasons — for which the existing message ("your series coverage is still filling in") is a reasonable thing to read anyway.

### 9. Entry eviction becomes least-recently-used

`pageStateStore` keeps insertion-ordered `Map` semantics but re-inserts a snapshot on access (`get`/`ensure` delete-then-set), so the first key is the least recently *used* rather than the oldest created, and the cap rises from 30 to 50. This is hardening, not the fix for the reported symptom: it removes the case where a long session silently drops the entry one step back in the stack, taking its scroll position and data with it. Cost is one map operation per lookup and a slightly larger ceiling on retained DTOs.

## Risks / Trade-offs

- **Recording on every scroll event instead of once per frame** → A field write and a `window.scrollY` read per event, on a listener already registered as `passive`. Cheaper than the `requestAnimationFrame` bookkeeping it replaces.
- **The input-based cancel could miss a way to scroll** (a scrollbar drag, a "scroll to element" from an extension) → The restore loop still stops when it reaches the target or the budget expires, so worst case a deliberate scroll during the first frames is overridden once; the retry is bounded and cannot fight the user indefinitely.
- **`STRIP_VISIBLE_TILES` duplicates the CSS tile count** (decision 4) → If the tile basis ever changes, a strip could scroll when it fits or fit when it scrolls. Mitigated by a comment on each end naming the other; a shared custom property would not help since the count is needed in JS.
- **Hidden scrollbars remove the "there is more here" hint** → The tenth tile is cut by the strip edge whenever there are more, which is the same hint the poster grids elsewhere rely on, and drag-scrolling remains discoverable through the grab cursor.
- **The coverage rule hides franchises whose later seasons are not built yet** → A partially built series has a short main line, so it reads as single-entry and is unaffected; the risk is the opposite direction (a series built with three seasons where only one is in my list is exactly what the rule targets). Series coverage grows in the background and the section is recomputed on every visit.
- **Series disappearing with no explanation and no way back** (decision 6/8) → Accepted deliberately: it is the point of the change. The set it removes is well defined and narrow — two-plus aired main-line entries, at most one of them in my list.
