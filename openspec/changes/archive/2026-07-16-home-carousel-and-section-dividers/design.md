## Context

The "Currently watching" row is a client-only React component (`CurrentlyWatchingCarousel.tsx`) built on a native `overflow-x: auto` track. Arrows call `scrollBy({ behavior: 'smooth' })`, and overflow is detected with a `ResizeObserver` comparing `scrollWidth` to `clientWidth`. Cards are fixed-width (`.anime-card { width: 160px }`) with a 16px gap.

Two presentation changes are requested:
1. Bound the row to at most 5 visible cards and, when there are more, let it scroll as a seamless infinite loop both directions.
2. Add a thin divider rule under each dashboard section title.

The three dashboard sections share the `.dashboard-section` class but differ in heading markup: the carousel and "Airing today" render `<h2>` as a direct child of the section, while "Followed shows airing" (`CurrentSeasonSection`) wraps its `<h2>` and a sort `<select>` in a `.current-season__header` flex row.

## Goals / Non-Goals

**Goals:**
- Cap visible cards at 5 by bounding the track width to 5 cards + 4 gaps.
- Seamless infinite looping in both directions when entries > 5, preserving native trackpad/touch scrolling AND the existing arrow buttons.
- Keep current behavior unchanged when entries ≤ 5 (all fit, no arrows, no loop).
- A divider rule under all three section titles that spans the section content width.

**Non-Goals:**
- No backend/API/data changes.
- No autoplay/auto-advance; scrolling stays user-driven.
- No change to the increment, navigation, or next-episode countdown behavior.

## Decisions

### Bounding visible cards to 5
Cap the track with `max-width: calc(5 * 160px + 4 * 16px)` (= 864px) rather than counting DOM nodes. The card width and gap are the single source of truth, so the cap follows them. `.dashboard-section--carousel` already uses `width: fit-content; margin: 0 auto`, so the section shrinks to the capped track plus arrows and stays centered. When fewer than 5 cards exist, the track is naturally narrower and the cap is inert.

_Alternative considered:_ measuring card width in JS and setting a pixel width. Rejected — CSS `calc` keeps it declarative and avoids a measure/reflow cycle.

### Infinite loop via triple-copy native scroll with silent reposition
When `items.length > 5`, render **three concatenated copies** of the items inside the track and start `scrollLeft` at the beginning of the middle copy. On every scroll event, if the position drifts out of the middle band, instantly re-center by ± one-copy-width via direct `scrollLeft` assignment. Because the three copies are pixel-identical, the reposition is invisible; the user perceives an endless row that wraps in both directions.

- One-copy width = the real pixel gap between the first card of copy 0 and the first card of copy 1 (`getBoundingClientRect().left` difference), not `track.scrollWidth / 3` — the track's own padding is applied once across all three copies, so dividing scrollWidth by 3 drifts the target off true card-boundary spacing by a couple of pixels. Recomputed on resize and when the item count changes.
- Reposition rule: keep `scrollLeft` within `[oneCopy, 2*oneCopy)`. If it falls below `oneCopy`, add `oneCopy`; if it reaches `2*oneCopy`, subtract `oneCopy`. The guard tracks the exact target of the last reposition and only suppresses a scroll event that matches it; any other event (including a stray one from an interrupted arrow animation) gets a fresh bounds check.
- Arrows keep calling `scrollBy({ behavior: 'smooth' })`; the same scroll handler re-centers after the smooth animation crosses a boundary, so arrows loop too.
- React keys must be unique across copies: prefix with the copy index (e.g. `` `${copy}:${item.animeId}` ``). The three renderings of one anime map from the same `items` entry, so an increment updates all copies together and pending state (keyed by `animeId`) stays correct.

_Alternatives considered:_
- **Array rotation / windowed render** (rotate a slice on arrow click, translate with `transform`). Rejected — loses continuous native trackpad/touch momentum scrolling, which is central to the "easy to scroll" request.
- **Head/tail clone padding only** (one clone each side). Rejected — a fast fling can overshoot a single clone; three full copies give ample buffer with trivial extra DOM for a ≤ small list.

### Section-title dividers
Add a `border-bottom` (1px, `var(--border)`) with matching `padding-bottom` to the heading element of each section:
- `.dashboard-section > h2` — the direct-child heading, covering "Currently watching" and "Airing today".
- `.current-season__header` — the flex wrapper for "Followed shows airing", so the rule spans the full width beneath both the title and the sort `<select>`, matching the sketch. The nested `<h2>` there drops its own bottom margin to avoid a double gap.

Using the `>` child combinator keeps the `CurrentSeasonSection` `<h2>` (which is nested, not a direct child) from getting its own short underline; only its header wrapper carries the divider.

## Risks / Trade-offs

- **Reposition seam / flicker during a fast fling** → Use direct `scrollLeft` assignment (instant) for the re-center and identical content across copies; re-center as soon as the boundary is crossed. Three copies provide enough runway that a normal fling settles before exhausting a copy.
- **`scroll-snap` fighting the reposition** → Originally assumed re-centering by exactly one-copy-width would preserve snap alignment. Verified in-browser that it doesn't: reposition targets computed mid-animation aren't themselves snap-aligned, so `scroll-snap-type: x proximity` silently "corrects" the just-set position to the nearest card edge — sometimes by enough to flip which side of the `[oneCopy, 2*oneCopy)` band it lands in, breaking the loop in one direction. Fixed by removing `scroll-snap-type`/`scroll-snap-align` from the carousel entirely; the reposition math is fully deterministic once the browser no longer adjusts positions on its own.
- **Programmatic re-center retriggering the scroll handler** → A single-shot "ignore the next event" boolean guard isn't sufficient: an in-flight `scrollBy({ behavior: 'smooth' })` animation (from an arrow click) keeps emitting its own trailing scroll events after our instant reposition, and a naive guard can swallow one of those and skip a bounds check it needed. Fixed by tracking the exact target we last set and only suppressing an event that matches it (within a small epsilon); anything else falls through to a fresh bounds check.
- **Divider under a wrapped/narrow heading** → Anchoring the "Followed shows airing" rule to `.current-season__header` (full-width flex row) rather than the `<h2>` keeps it spanning the section width regardless of title length.

## Open Questions

- Divider weight/color: assume 1px `var(--border)` to match the section border tone; adjust if the sketch intends a heavier rule.
