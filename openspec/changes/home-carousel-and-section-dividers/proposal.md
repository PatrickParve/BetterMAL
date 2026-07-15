## Why

The "Currently watching" carousel on the home page currently renders every entry inline and only offers a nudge-scroll via arrows, so a large list sprawls across the page and there is no continuous way to browse it. Users want a compact, predictable row that shows at most a handful of shows at once and lets them scroll around the list endlessly in either direction. Separately, the dashboard section headings ("Currently watching", "Airing today", "Followed shows airing") read as floating labels; a thin rule under each title would visually anchor the heading to its content, matching the intended design.

## What Changes

- Cap the "Currently watching" carousel so at most **5** cards are visible at once; the row's width is bounded to five cards rather than growing with the list.
- When there are more than 5 entries, make the carousel **loop infinitely in both directions**: scrolling right past the last card continues to the first, and scrolling left past the first continues to the last, with no visible seam or dead-end.
- Keep the existing behavior when 5 or fewer entries exist: all cards fit, no looping/arrows are needed.
- Add a thin horizontal divider **line under each dashboard section title** — "Currently watching", "Airing today", and "Followed shows airing" — separating the heading from its content, as shown in the reference sketch.

## Capabilities

### New Capabilities
<!-- None: this refines existing dashboard behavior and presentation. -->

### Modified Capabilities
- `main-dashboard`: The "Currently watching horizontal carousel" requirement changes from an unbounded nudge-scroll row to a row that shows at most 5 cards and, when more exist, scrolls as an infinite loop in both directions. A new presentation requirement adds a divider rule beneath each dashboard section title.

## Impact

- Frontend only; no backend, API, or data changes.
- `frontend/src/components/CurrentlyWatchingCarousel.tsx` and `.css` — visible-count cap and infinite-loop scrolling logic.
- `frontend/src/pages/HomePage.css` — section-title divider styling shared by all three dashboard sections (`.dashboard-section h2`), which also covers the carousel and the "Followed shows airing"/"Airing today" sections.
