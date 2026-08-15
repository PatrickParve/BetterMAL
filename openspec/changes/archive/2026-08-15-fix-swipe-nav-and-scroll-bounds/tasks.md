## 1. Restore the browser's back/forward swipe

- [x] 1.1 In `frontend/src/index.css`, change the `html, body` block (lines 142-149) from `overscroll-behavior-x: none` to `overscroll-behavior-y: none` — the x axis goes back to its default so the two-finger history gesture works again, and the y axis picks up the vertical bound from task 3.1.
- [x] 1.2 Rewrite the comment above that block: it currently documents suppressing "the browser's swipe-to-navigate gesture" as the intent. It must now say the y axis stops the document's vertical rubber-band, that the x axis is deliberately left at default *because* `none`/`contain` there kills the history gesture, and that sideways drift is prevented per-region by `overscroll-behavior-x: contain` instead (design.md decision 1 has the wording).

## 2. Confine the currently-watching carousel to one axis

- [x] 2.1 In `frontend/src/components/CurrentlyWatchingCarousel.css`, add `overflow-y: hidden` to `.carousel__track` next to its existing `overflow-x: auto`, matching `.series-timeline__scroll` and the three profile strips.
- [x] 2.2 Confirm the track's existing `padding: 6px` still lets `.anime-card::before` (the `inset: -6px` hover plate) render unclipped at the top and bottom, since `overflow` clips at the padding edge — no padding change should be needed.

## 3. Bound the page vertically

- [x] 3.1 Confirm task 1.1's `overscroll-behavior-y: none` is what removes the rubber-band above the navbar, and that no other rule on `html`, `body`, or `#root` re-enables it. (No separate edit — 1.1 covers both the x and the y change in the one block.)
- [x] 3.2 Verify `useScrollRestoration` is untouched and the document is still the scrolling element, so `window.scrollTo` / `window.scrollY` keep working.

## 4. Correct the now-stale comments

- [x] 4.1 `frontend/src/components/CurrentlyWatchingCarousel.css` — the `.carousel__track` comment cites "index.css's `overscroll-behavior-x: none` handles the document side"; that declaration no longer exists. Reword so `overscroll-behavior-x: contain` reads as the whole mechanism, covering both no-chaining-to-the-page and no-accidental-history-navigation.
- [x] 4.2 `frontend/src/components/SeriesTimeline.css` — same correction on `.series-timeline__scroll`.
- [x] 4.3 `frontend/src/pages/ProfilePage.css` — same correction on `.top-anime-strip`, and check the `.rewatched-strip` and `.top-series-strip` comments that defer to it ("See .top-anime-strip") still read correctly afterwards.

## 5. Verify in the browser

- [x] 5.1 Build the frontend (`nvm use 22` first — the default node is v16 and Vite needs 22).
- [x] 5.2 Swipe back and forward with two fingers over ordinary content on the home, my-list, and anime detail pages; confirm history navigation works in both directions.
- [x] 5.3 Swipe sideways over the currently-watching carousel, the series timeline, and each of the three profile strips, past both ends: the region stops at its end, the page does not move sideways, and no back/forward navigation fires.
- [x] 5.4 Make a diagonal two-finger gesture over the currently-watching row: the cards move horizontally only, with no vertical play, and the page scrolls for the vertical component.
- [x] 5.5 Hover the first and last carousel cards with the row scrolled fully left and fully right; the hover plate is unclipped at every edge.
- [x] 5.6 Scroll up at the top of a page: nothing appears above the navbar. Scroll down past the bottom of a long page: no bounce. Check on the browser actually in use — if the document-level rule is ignored there, stop and reconsider against design.md's `#root`-as-scroll-container fallback rather than working around it.
- [x] 5.7 Check no page shows a document-level horizontal scrollbar at the narrowest and widest supported window widths, now that the document-level containment is gone.
- [x] 5.8 Navigate away from and back to the profile page and confirm vertical scroll position and strip offsets still restore (`page-state-restoration` is unaffected but shares the scroll surface).
