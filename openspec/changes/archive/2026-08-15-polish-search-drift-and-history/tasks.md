## 1. Uniform type-ahead dropdown rows

- [x] 1.1 In `frontend/src/components/SearchBar.css`, give `.search-bar__result` a fixed `height: 52px` with `box-sizing: border-box` (40px thumbnail + its 6px vertical padding, design decision 1), and comment why the height is fixed rather than left to the content.
- [x] 1.2 In the same file, pin the two line boxes the series stack is built from: `.search-bar__result-title { line-height: 20px }` and a dropdown-scoped `.search-bar__result .series-badge { line-height: 16px }` (design decision 2), noting in a comment that the root's `145%` line-height computes to ~23px and inherits as a length, which is what made the series row taller.
- [x] 1.3 Confirm `SeriesBadge.css` is left untouched, and check the search results page to be sure a series card still matches the anime cards beside it — the scoped rule must not reach the grid.
- [x] 1.4 Verify in the running app: type a query that matches both series and anime, confirm every dropdown row is the same height, the dropdown's height changes only with row count, and rows do not shift as further characters change which rows are series.
- [x] 1.5 Verify the same at a wide window (2400px+, where the root font hits its 18px cap) that no title is clipped by the fixed row height.

## 2. Search field restyled as one control

- [x] 2.1 In `frontend/src/components/SearchBar.css`, move the border, background, and 8px radius onto `.search-bar` itself, and make `.search-bar__input` borderless and transparent with its own outline suppressed (design decision 3).
- [x] 2.2 Restyle `.search-bar__submit` as an inset icon button: no border, no seam, no left radius, sitting inside the field's right edge, keeping its existing hover treatment and its `outline-offset: -2px` focus ring.
- [x] 2.3 Add the field-level focus indication on `.search-bar:focus-within` — accent border colour plus an inset ring — in the same rule block as the input's suppressed outline, with a comment tying the two together.
- [x] 2.4 Check `frontend/src/components/SearchBar.tsx` and add wrapper markup only if the CSS needs it; keep the input, the button, the dropdown, and every handler as they are.
- [x] 2.5 Verify by keyboard and pointer: focus the input and confirm nothing is drawn over the magnifier; tab to the magnifier and confirm its ring stays inside the field; confirm hover on the magnifier still reads; confirm the field's size and position in the navbar are unchanged, including the ≤900px wrapped layout.
- [x] 2.6 Verify Enter and the magnifier still submit to `/search?q=…`, the query still restores on the search page, and the dropdown still anchors flush under the field.

## 3. No horizontal page drift

- [x] 3.1 Before changing anything, confirm no page currently shows a document-level horizontal scrollbar (home, my list, top, season, airing, profile, series, detail, search) so the backstop in 3.3 is not masking a real overflow.
- [x] 3.2 In `frontend/src/index.css`, add `overscroll-behavior-x: none` to `html, body` (declared on `html` explicitly, design decision 4) with a comment naming both effects: no document rubber-band, no swipe-to-navigate.
- [x] 3.3 In the same file, add `overflow-x: clip` to `#root` as a backstop, noting why `clip` and not `hidden`.
- [x] 3.4 Add `overscroll-behavior-x: contain` to each horizontally scrolling region: `.carousel__track` (`CurrentlyWatchingCarousel.css`), `.series-timeline__scroll` (`SeriesTimeline.css`), and `.top-anime-strip`, `.rewatched-strip`, `.top-series-strip` (`ProfilePage.css`).
- [x] 3.5 Verify with a trackpad: a two-finger sideways flick on an ordinary page moves nothing; the same flick past the end of a poster strip, the carousel, and the series timeline leaves the page still; and no gesture triggers a back/forward navigation.
- [x] 3.6 Verify the strips still scroll and still drag-scroll, and that their scroll offsets are still restored on back-navigation (the `page-state-restoration` behaviour).

## 4. History overlay closes from a corner ✕

- [x] 4.1 In `frontend/src/components/EditHistoryOverlay.tsx`, wrap the `<h2>` and a new close button in an `.edit-history__header` row, with the button as `<button type="button" aria-label="Close history">` holding an inline ✕ SVG in the same shape as `SearchBar`'s `SearchIcon` (design decision 5).
- [x] 4.2 Delete the `.edit-history__buttons` footer and its Close button from the component, and remove the `.edit-history__buttons` rules from `EditHistoryOverlay.css`.
- [x] 4.3 Style `.edit-history__header` as a space-between flex row and `.edit-history__close` as an icon control with the hover and focus-visible treatment of the app's other icon controls (`.navbar__settings`).
- [x] 4.4 Verify: clicking the ✕ closes the overlay; tabbing to it and pressing Enter or Space closes it with a visible focus ring; Esc and click-outside still close it; and no Close button remains below the list.

## 5. History list opens on five whole rows

- [x] 5.1 In `frontend/src/components/EditHistoryOverlay.css`, declare `--history-row-h: 78px` on `.edit-history` and use it for `.edit-history__row`'s `min-height` (design decision 6).
- [x] 5.2 Replace `.edit-history__list`'s `max-height: 55vh` with `max-height: calc(5 * (var(--history-row-h) + 2px) + 4 * 6px)`, and comment the arithmetic — five outer row heights (content plus the row's 1px top and bottom borders) plus the four gaps between them.
- [x] 5.3 Verify with a long history: five rows are fully visible on open, no sliver of a sixth, no row cut through the middle, and the rest scrolls; the ✕ stays put while the list scrolls.
- [x] 5.4 Verify at several window heights (including ~700px, where the modal's own `85vh` starts to bite) that five whole rows are still shown, and that filtering to two rows shows two rows without reserved space.

## 6. Detail page scores as coloured numbers

- [x] 6.1 In `frontend/src/pages/AnimeDetailPage.tsx`, replace the `ScoreChip role="mal" label="MAL score"` with a `<p>` line in the same form as the `Rank:` and `Popularity:` lines beside it, wrapping the existing `<ScoreValue>` in a `<span className="score--mal">` (design decision 7) — the `ScoreValue` element and its props stay exactly as they are.
- [x] 6.2 Replace the `ScoreChip role="mine" label="My score"` with the same labelled-line form, the score in a `<span className="score--mine">`, leaving the rewatch-count and completed-date lines beneath it untouched.
- [x] 6.3 Drop the now-unused `ScoreChip` import from `AnimeDetailPage.tsx` and confirm `ScoreChip` itself and its five other callers (series page averages, series timeline, series extra tiles, profile top-series tiles) are untouched.
- [x] 6.4 In `frontend/src/pages/AnimeDetailPage.css`, delete `.anime-detail-page__score-boxes .score-chip { min-width: 0 }`, which existed only to defeat the chip's 130px floor on this page, and adjust the panels' spacing only if the lines need it.
- [x] 6.5 Verify on a scored anime in both light and dark themes: the MAL score is a blue number and my score a purple number, each on a labelled line in the same rhythm as the lines around it, with no tint or border around either, and the labels in ordinary text.
- [x] 6.6 Verify the panels still read as a compact pair beside the title in all three states — a scored entry, an entry with no score (no second panel), and an entry with neither rewatch count nor completion date — and that they still stack under 1024px.
- [x] 6.7 Verify score visibility is unchanged: with the hide toggle on the MAL line shows its reveal control in the MAL colour and no value in the DOM; revealing shifts nothing; "always show MAL scores for completed shows" still applies; and a revealed score re-hides after navigating away and back.

## 7. Wrap-up

- [x] 7.1 Run the frontend build (Vite, under Node 22) and confirm it is clean.
- [x] 7.2 Re-read all four delta specs against the running app, confirm each scenario holds, then run `openspec validate polish-search-drift-and-history --strict`.
