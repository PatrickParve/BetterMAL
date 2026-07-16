## 1. Cap the carousel to 5 visible cards

- [x] 1.1 In `CurrentlyWatchingCarousel.css`, cap `.carousel__track` with `max-width: calc(5 * 160px + 4 * 16px)` so at most 5 cards (plus gaps) are visible, and confirm `.dashboard-section--carousel` still centers the shrunk row.
- [x] 1.2 Verify with 6+ entries that only 5 cards show at once and the section stays centered; with ≤5 entries the row is narrower and unchanged.

## 2. Infinite-loop scrolling

- [x] 2.1 In `CurrentlyWatchingCarousel.tsx`, derive `looping = overflowing` (entries exceed what fits) and, when looping, render three concatenated copies of `items`, keying each card as `` `${copy}:${item.animeId}` `` so keys stay unique.
- [x] 2.2 On mount / when item count changes / on resize, compute one-copy width as `track.scrollWidth / 3` and set initial `scrollLeft` to the start of the middle copy.
- [x] 2.3 Add a scroll handler that keeps `scrollLeft` within `[oneCopy, 2*oneCopy)` by instantly re-centering (`scrollTo({ behavior: 'auto' })`) ± one-copy-width, guarded by a flag so the programmatic scroll does not re-trigger the handler.
- [x] 2.4 Keep the arrow buttons calling `scrollBy({ behavior: 'smooth' })` and confirm the scroll handler re-centers after the smooth animation crosses a boundary, so arrows loop in both directions.
- [x] 2.5 When not looping (≤5 entries / no overflow), render a single copy with no clones, no re-centering, and no arrows — matching current behavior.
- [x] 2.6 Verify incrementing a card still updates all copies (shared `items` entry) and that pending state keyed by `animeId` behaves correctly across copies.

## 3. Section-title dividers

- [x] 3.1 In `HomePage.css`, add a `border-bottom: 1px solid var(--border)` with matching `padding-bottom` to `.dashboard-section > h2` (covers "Currently watching" and "Airing today"); adjust existing `margin-bottom` so spacing stays balanced.
- [x] 3.2 In `CurrentSeasonSection.css`, add the same divider to `.current-season__header` so the rule spans the full width under both the "Followed shows airing" title and the sort `<select>`, and remove the nested `<h2>`'s own bottom margin to avoid a double gap.
- [x] 3.3 Verify all three sections show a thin divider under their title spanning the section content width, matching the reference sketch.

## 4. Validation

- [x] 4.1 Build the frontend (nvm Node v22 + Vite) and confirm no type/lint errors.
- [x] 4.2 Manually exercise the home page: 6+ currently-watching entries loop seamlessly both directions via trackpad/drag and via arrows; ≤5 entries fit with no arrows; dividers render under all three section titles.
