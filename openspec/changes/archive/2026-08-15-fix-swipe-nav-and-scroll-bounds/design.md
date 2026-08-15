## Context

All three problems live in the same two lines of shell CSS plus one missing declaration on one strip.

**Where things stand today** (`frontend/src/index.css:142-149`):

```css
/* Stops the document's own horizontal rubber-band and the browser's
   swipe-to-navigate gesture. Declared on html explicitly rather than
   relying on body-to-viewport propagation, which only applies when html
   says nothing. */
html,
body {
  overscroll-behavior-x: none;
}
```

Three consequences of that block, matching the three reports one-for-one:

1. `overscroll-behavior-x: none` on the document is *defined* to disable the browser's history swipe — the comment names it as intended. That is problem 1.
2. The vertical axis is left at `auto`, so the document still rubber-bands. Pulling up at scroll position zero drags the whole shell down and exposes the page background above the navbar. That is problem 3.
3. Nothing here concerns the carousel. Problem 2 is `.carousel__track` in `CurrentlyWatchingCarousel.css`: it sets `overflow-x: auto` and says nothing about `overflow-y`. Per CSS overflow, when one axis is not `visible` the other computes from `visible` to `auto`, so the track is silently a **vertical** scroll container too. It has a few pixels of vertical scrollable overflow — `.anime-card::before` is an absolutely positioned hover plate at `inset: -6px`, and the card heights are fractional because the root font size is a `clamp()` of viewport width — so the track drifts by a couple of pixels. The four sibling strips (`.series-timeline__scroll`, `.top-anime-strip`, `.rewatched-strip`, `.top-series-strip`) all already pin `overflow-y: hidden`; the carousel is the one that was missed.

**The constraint that makes this safe.** Removing the document's horizontal containment only re-opens the history gesture — it does not re-open the horizontal *drift* the original requirement was written against — because every horizontal region already sets `overscroll-behavior-x: contain` on itself. `contain` prevents scroll chaining to ancestors *and* prevents the browser navigation gesture from being triggered by that element's overscroll. The five regions carry their own guarantee; the document-level `none` was belt-and-braces that cost the swipe.

The document has no horizontal overflow to rubber-band in the first place: `#root` is `width: 100%; max-width: 2400px` with `overflow-x: clip`, and `.dashboard-section--carousel` was moved to `box-sizing: border-box` precisely so it stops pushing past the viewport.

## Goals / Non-Goals

**Goals:**

- Restore the browser's two-finger back/forward gesture on every page.
- Keep page content horizontally fixed, and keep a flick past a strip's end from navigating away.
- Make the currently-watching carousel move on the horizontal axis only.
- Stop the document rubber-banding above the navbar.
- Keep the change to CSS declarations plus their comments — no TypeScript, no markup.

**Non-Goals:**

- Making the navbar sticky. "Nothing above the navbar" here means "no rubber-band above the top of the page", not "the navbar is pinned while scrolling". The navbar scrolls away with content today and continues to.
- Changing how the strips scroll, drag-scroll, snap, or restore their offsets.
- Touching `useScrollRestoration`. The document stays the scrolling element, so `window.scrollTo` keeps working unchanged.
- Any touch/mobile-specific tuning. This is a desktop trackpad fix; `touch-action` is not introduced.

## Decisions

### Decision 1: Drop `overscroll-behavior-x` from the document rather than scoping it away

The block becomes:

```css
/* Stops the document's own vertical rubber-band, so a scroll-up at the top
   of a page can't drag the shell down and expose empty space above the
   navbar. Declared on html explicitly rather than relying on
   body-to-viewport propagation, which only applies when html says nothing.
   The x axis is deliberately left at its default: setting it to `none` here
   also switches off the browser's two-finger back/forward gesture. Sideways
   drift is prevented at the source instead — every horizontally scrolling
   region sets `overscroll-behavior-x: contain` on itself, which stops both
   chaining to the page and an accidental history navigation. */
html,
body {
  overscroll-behavior-y: none;
}
```

*Alternatives considered.* `overscroll-behavior-x: contain` on the document instead of removing it: rejected — `contain` on the root element disables navigation gestures just as `none` does; the two differ only in whether the local bounce is kept. Keeping `none` and re-implementing back/forward from a JS wheel-gesture listener: rejected outright — it would reimplement a native gesture, including its live rubber-band preview and its inertia, and would still be reachable only through synthetic history calls.

### Decision 2: `overflow-y: hidden` on `.carousel__track`, matching the four sibling strips

One declaration, the same one already used by every other horizontal strip in the app, placed next to the existing `overflow-x: auto`.

The track's `padding: 6px` is what keeps this from clipping the hover plate: `overflow` clips at the **padding** edge, and `.anime-card::before` extends exactly 6px past the card on every side — the padding the track already reserves for that purpose. The four other strips prove the combination out; they all pair `overflow-y: hidden` with their own 8px/20px padding and show unclipped hover treatments.

*Alternatives considered.* `touch-action: pan-x`: rejected — it constrains touch input, not the trackpad's synthesised wheel events, so it would not fix the report, and on a touchscreen it would swallow the vertical component that should scroll the page. Removing the hover plate's negative inset so nothing overflows: rejected — the plate's geometry is deliberate (`AnimeCard.css` documents it), and it would not address the fractional-height component.

### Decision 3: Suppress the vertical bounce at both ends, not just the top

`overscroll-behavior-y` has no "top edge only" value. The options were both ends via one declaration, or a JS scroll clamp to kill only the top bounce. Both ends it is — the bottom bounce is the same effect at the other edge, and suppressing it is consistent rather than surprising. Called out in the proposal so it isn't a silent extra.

### Decision 4: Fix the four stale comments in the same pass

`CurrentlyWatchingCarousel.css`, `SeriesTimeline.css`, and `ProfilePage.css` (×3, two of which point back at `.top-anime-strip`) each explain their `overscroll-behavior-x: contain` with "index.css's `overscroll-behavior-x: none` handles the document side". After this change there is no document side — `contain` *is* the whole mechanism, and it is now load-bearing for the no-accidental-navigation guarantee. Leaving the comments would point a future reader at a declaration that no longer exists and invite them to re-add it, which would break the swipe again.

## Risks / Trade-offs

- **The browser's own gesture preview still moves the page during a swipe.** Confirmed in manual verification: with `overscroll-behavior-x` left at its default on ordinary page content, a two-finger swipe shows the browser's native slide/rubber-band preview while the fingers are still down, before it resolves into a navigation. → Accepted. That preview is the browser's own gesture chrome, not page content, and there is no web-platform way to keep the gesture recognized while hiding its native visual feedback — the JS-reimplementation alternative that could suppress it was already rejected above for the same reason (it would have to fully replace the native gesture, including its inertia and release behaviour, just to keep the page visually still). Written into the `navigation-and-search` requirement so it reads as intended behaviour rather than unaddressed drift.
- **Swipe-back over a strip stops feeling available.** With `contain` on all five regions, a two-finger swipe that begins over the carousel or a poster strip never navigates, even once the strip is at its end. → Intended, and now specified: it is what stops a browsing gesture from throwing the user off the page. Back/forward remains available everywhere else on the page, plus the buttons and keyboard shortcuts.
- **`overscroll-behavior` on the document is not honoured identically by every engine.** Chromium honours it on `html`/`body`; Safari has been less consistent about the document-level rubber-band specifically. → Verify in the browser actually used before calling problem 3 done. If it is ignored there, the fallback is to make `#root` the scroll container (`html, body { height: 100%; overflow: hidden }` + `#root { overflow-y: auto; overscroll-behavior-y: none }`) — but that changes the scrolling element out from under `useScrollRestoration`'s `window.scrollTo`/`window.scrollY` and would pull that hook into scope. Not worth it unless the one-line version demonstrably fails.
- **Something else could still make the page horizontally scrollable.** The document-level `none` was also masking any future horizontal overflow. → `#root`'s `overflow-x: clip` remains the backstop, and the "no horizontal scrollbar" scenario in the spec stays in force; the verification step checks it explicitly on the widest and narrowest supported widths.
- **The carousel's `overflow-y: hidden` clips a hover plate that later grows past 6px.** → The plate's inset and the track's padding are already documented as one budget in both files; the spec's "Hover treatment still fits" scenario is the guard.

## Migration Plan

No data, no API, no build change. Three CSS edits ship together; reverting is a `git revert` of the single commit. Nothing persists across the change, so there is no rollback state to consider.

## Open Questions

None. All three fixes are determined by the existing CSS and the sibling strips' established pattern.
