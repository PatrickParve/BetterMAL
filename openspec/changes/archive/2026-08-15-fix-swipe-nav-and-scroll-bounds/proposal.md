## Why

Three scroll/gesture problems on the trackpad, all in the app shell:

- **Two-finger swipe back/forward no longer works anywhere in the app.** The "pages do not drift horizontally" work put `overscroll-behavior-x: none` on `html, body` — that declaration stops the document's sideways rubber-band, but it also switches off the browser's own history gesture as a documented side effect (its own comment in `index.css` says so). Back/forward by trackpad is the primary way to move around this app; the horizontal-fixity fix took it out along with the drift.
- **The home page's "Currently watching" carousel drifts vertically.** A two-finger gesture over the row moves it a few pixels up and down as well as sideways. The row is a horizontal strip and should only ever move horizontally. The four sibling strips (series timeline, and the profile page's top-anime, rewatched, and top-series strips) all already carry `overflow-y: hidden` for exactly this; the carousel track is the one that was missed.
- **The page rubber-bands above the navbar.** Scrolling up at the top of any page pulls the whole shell down and reveals empty space above the navbar. `overscroll-behavior-x: none` was set on the document but the vertical axis was left at its default, so the vertical bounce is still there.

## What Changes

- **The browser's two-finger back/forward gesture works again**, on every page. The document-level horizontal containment that killed it is removed.
- **Pages still do not drift horizontally.** The protection moves entirely to the scrolling regions themselves: each horizontal strip already sets `overscroll-behavior-x: contain`, which is what stops a flick past a strip's end from reaching the page — and it equally stops that flick from being read as a back/forward navigation. So a sideways gesture that starts over a strip never navigates; a sideways gesture on ordinary page content does.
  - This is a **deliberate narrowing of an existing requirement**: "a sideways gesture never moves the page" now means "never moves the page's *content*", and no longer means "never triggers the browser's history gesture".
- **The currently-watching carousel scrolls horizontally only** — the vertical play in the track is removed, matching the other four strips.
- **The document no longer rubber-bands vertically.** Scrolling up at the top of a page cannot pull the shell below the top of the window, so nothing is ever revealed above the navbar. The same declaration also removes the bounce at the *bottom* of a page; that is accepted as the consistent behaviour rather than suppressing only the top edge, which CSS cannot express on its own.

## Capabilities

### New Capabilities

None — both capabilities involved already exist.

### Modified Capabilities

- `navigation-and-search`: modifies the "Pages do not scroll or drift horizontally" requirement so it constrains page *content* only and explicitly preserves the browser's back/forward swipe gesture, with the no-navigation guarantee kept for gestures that start over a horizontal strip; adds a requirement that the horizontal regions scroll on one axis only; adds a requirement that pages do not scroll or bounce past their vertical bounds.

`main-dashboard` is deliberately **not** modified: the one-axis rule is stated once in `navigation-and-search`, which already owns the cross-cutting gesture behaviour of these regions and already names all five of them by name. A carousel-only copy of the rule in `main-dashboard` would be the second place to keep in step.

## Impact

- `frontend/src/index.css` — `html, body` drops `overscroll-behavior-x: none` and gains `overscroll-behavior-y: none`; the comment above it is rewritten, since it currently documents the swipe-suppression as intended behaviour.
- `frontend/src/components/CurrentlyWatchingCarousel.css` — `.carousel__track` gains `overflow-y: hidden`.
- `frontend/src/components/CurrentlyWatchingCarousel.css`, `frontend/src/components/SeriesTimeline.css`, `frontend/src/pages/ProfilePage.css` — the four "index.css's `overscroll-behavior-x: none` handles the document side" comments become stale and are corrected; the `overscroll-behavior-x: contain` declarations they annotate are unchanged and now carry the guarantee on their own.
- No TypeScript, no component markup, no API, no backend, and no database change. `useScrollRestoration`'s `window.scrollTo` path is untouched — the document remains the scrolling element.
