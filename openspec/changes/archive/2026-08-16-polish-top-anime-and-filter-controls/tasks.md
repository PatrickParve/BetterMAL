## 1. Shared tokens and the search-field cursor

- [x] 1.1 In `frontend/src/index.css`, add `--control-h: 34px` to `:root` with a comment stating it is the height of a control in a filter/sort cluster (not an app-wide control size), per design D6.
- [x] 1.2 In `frontend/src/index.css`, add `input[type='search']::-webkit-search-cancel-button { cursor: pointer; }` with a comment noting it covers both search fields (navbar + Settings picker) and is inert in Firefox, per design D7.
- [x] 1.3 Verify by hand: typing in the navbar search field and hovering its "×" shows the pointer cursor, clicking it still clears the field, and the dropdown/focus ring/magnifier are unchanged.

## 2. Normalize filter/sort control heights

- [x] 2.1 In `frontend/src/components/FilterMultiSelect.css`, give `.filter-multi-select__button` `height: var(--control-h)`, `box-sizing: border-box`, `display: inline-flex`, `align-items: center`, and inline-only padding (`0 10px`); leave the panel's `top: calc(100% + 4px)` anchoring alone.
- [x] 2.2 In `frontend/src/pages/SeasonPage.css`, apply the same height treatment to `.season-page__sort` (the season, year, and sort selects all share it) and `.season-page__checkbox`; leave `.season-page__nav button` at its 32px circular size.
- [x] 2.3 In `frontend/src/pages/SearchPage.css`, apply the same height treatment to `.search-page__sort`.
- [x] 2.4 In `frontend/src/pages/MyListPage.css`, apply it to `.my-list-page__sort`, `.my-list-page__query`, `.my-list-page__clear-filters`, and `.my-list-page__tab` so the shared `Type`/`Airing` button doesn't create a new mismatch there.
- [x] 2.5 Verify in Chrome and Safari that Season's five controls, Search's two, and My List's filter bar are each flush; if a `<select>` ignores `height` under Safari's `menulist` appearance, apply the `appearance: none` + custom-arrow fallback noted in design D6 (the pattern already used by `.my-list-row__score-select`).
- [x] 2.6 Verify the heights still match at both ends of the fluid root font size (narrow and wide viewport), and that opening the `Type` popover, selecting, clearing, and closing it all behave as before.

## 3. Top Anime — ranks 1–3 showcase cards

- [x] 3.1 In `frontend/src/pages/TopAnimePage.css`, declare the three medal token triplets (`--medal-gold`/`-bg`/`-border`, silver, bronze) on `.top-anime-page`, reusing the existing hexes and keeping the comment explaining why they are literal rather than theme-swapped (design D2).
- [x] 3.2 Add the shared `.top-anime-rank` badge family with its `--lg` modifier (20px/800, medal fill, `#fff` text, `#N`, tabular figures), per design D3.
- [x] 3.3 Replace `.top-anime-podium*` in `TopAnimePage.css` with `.top-anime-showcase*`: a `repeat(3, minmax(0, 1fr))` grid collapsing to one column under 1024px; each card a bordered surface (12px radius, medal border + tint) laying out a fixed 104px `2/3` poster beside an info column.
- [x] 3.4 Give the showcase card its hover/`:focus-within` state — `background: var(--accent-bg)`, `border-color: var(--accent-border)`, `box-shadow: var(--shadow)` — and give rank 1 its `0 0 0 1px var(--medal-gold-border), var(--shadow)` ring-plus-elevation emphasis (design D4).
- [x] 3.5 In `frontend/src/pages/TopAnimePage.tsx`, rewrite the ranks 1–3 block to the new markup: rank badge, `Link`-wrapped poster and title, the score row, and the existing `renderActionButton` output. Drop the `--rank-N` `order` overrides so DOM order is visual order (1, 2, 3 left to right).
- [x] 3.6 Swap the showcase scores to `ScoreChip`: `<ScoreChip role="mine" label="My score">` and `<ScoreChip role="mal" label="MAL">` wrapping the existing `<ScoreValue value={item.malScore} completed={…} />`; put the pair in a `display: flex; gap: 8px; flex-wrap: wrap` row (design D5). Do not modify `ScoreChip` or `ScoreValue`.
- [x] 3.7 Verify: the three cards read 1, 2, 3 left to right, each states its own rank, the medal colours survive a light/dark switch, hover highlights the whole card without shifting neighbours, and Add still flips to Edit in place.

## 4. Top Anime — ranks 4–10 card row

- [x] 4.1 Add the `.top-anime-rank--sm` badge modifier (13px/700, `background: var(--text-h)`, `color: var(--bg)`, `pointer-events: none`) and use it for `.top-anime-card__rank`, replacing the translucent-black pill, per design D3.
- [x] 4.2 Give `.top-anime-card` the `AnimeCard`-style hover plate: a `::before` at `inset: -6px`, `border-radius: 12px`, `z-index: -1`, transitioning to `var(--accent-bg)` / `var(--accent-border)` / `var(--shadow)` on `:hover` and `:focus-within` (design D4). Keep the 7-up grid and its ≤1024px auto-fill fallback.
- [x] 4.3 Update `TopAnimePage.tsx`'s ranks 4–10 block to use the new badge class; leave its scores as `.score--mine` / `ScoreValue`.
- [x] 4.4 Verify: `#4`–`#10` stay fully legible over bright, pale, and dark poster art; hover plates never touch a neighbour or shift layout.

## 5. Regression checks and close-out

- [x] 5.1 Confirm the flat rows for rank 11+ are visually and structurally unchanged, and that page 2 and beyond render only flat rows (no showcase or card tier).
- [x] 5.2 Confirm score hiding still works: with MAL scores hidden, each showcase card's MAL chip shows the reveal control and revealing one reveals that score alone; the always-show-completed setting still applies.
- [x] 5.3 Run `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` and `npm run lint` in `frontend/` — the default `node` is v16 and fails the Vite build.
- [x] 5.4 Remove any now-dead `.top-anime-podium*` rules and unused medal/stand CSS left behind by the rewrite.

## 6. Follow-up refinements (post-implementation feedback)

- [x] 6.1 Remove the Add/Edit action button from the ranks 4–10 top-ten cards; drop the now-dead `.top-anime-card__action*` CSS.
- [x] 6.2 Give `.top-anime-card` (ranks 4–10) a permanent bordered, accent-tinted box instead of no visible boundary; simplify its hover from the offset `::before` plate (needed only for a borderless card) to a direct border/shadow change on the now-bordered box; move the rank badge inside `.top-anime-card__link` so it still anchors to the poster's corner despite the card's new padding.
- [x] 6.3 Shrink the showcase tier's `ScoreChip` pair (`.top-anime-showcase__scores .score-chip*`) so "My score" and "MAL" stay smaller and on one line without crowding the 140px poster.
- [x] 6.4 Remove the Add/Edit action button from the ranks 1–3 showcase cards; drop the now-dead `.top-anime-showcase__action*` CSS.
- [x] 6.5 Enlarge the showcase poster from 104px to 140px wide.
- [x] 6.6 Fix: changing the page from the bottom pagination controls (page numbers or arrows) left the reader scrolled past the new page's top entries, because `page` is local `useRestorableState`, not a navigation — it never triggers `useScrollRestoration`'s location-keyed scroll-to-top. Added a `goToPage` wrapper in `TopAnimePage.tsx` that calls `window.scrollTo(0, 0)` after `setPage`, wired to both the top and bottom `Pagination` instances.
- [x] 6.7 Update `specs/library-views/spec.md`'s "Top anime ranked list" requirement: scope the list-action button to the flat-row tier only (ranks 1–10 no longer carry one), note the ranks 4–10 tier's new bordered/tinted box, and add the scroll-to-top-on-page-change requirement and scenario.
- [x] 6.8 Fix: going back (browser back button, keyboard shortcut, or trackpad swipe) after changing the Top anime page left the ranking entirely, landing on whatever page preceded Top anime, instead of stepping to the previously-viewed page of the ranking — because `page` was local React state, invisible to browser history. Moved `page` into the URL (`?page=N`) via `useSearchParams` in `TopAnimePage.tsx`, so each page change pushes a real, swipe/back/forward-navigable history entry; `useScrollRestoration`'s existing location-keyed logic now handles the page-1↔page-N scroll behaviour (task 6.6's manual `goToPage`/`window.scrollTo` call was removed as redundant).
- [x] 6.9 Decoupled the ranking fetch from `page` to avoid a regression from 6.8: `usePageData`'s cache is keyed per history entry, so once `page` lived in the URL it would have treated every page's own URL as a distinct resource needing its own network fetch — flashing "Loading…" on every page change even though the full ranking (identical regardless of page) was already in memory. Replaced `usePageData('top-anime', getTopAnime)` with a module-scoped cache (`cachedItems`/`loadTopAnimeOnce` in `TopAnimePage.tsx`) fetched at most once per app session; verified via a network-request count across a page 1→2→3→back→back→forward sequence that only one `/api/top-anime` request fires.
- [x] 6.10 Make the showcase tier's two score chips equal width: `.top-anime-showcase__scores .score-chip` gained `flex: 1 1 0`, and `.top-anime-showcase__scores` itself gained `align-self: stretch` — its parent `.top-anime-showcase__info` uses `align-items: flex-start`, which was leaving the scores row hugging its own content width instead of filling the column, so the `flex: 1 1 0` split had almost no room to work with.
- [x] 6.11 Update `specs/library-views/spec.md`: add the page-change-creates-history-entry requirement and its three back/forward scenarios, and note the showcase tier's two score chips are equal-sized with a scenario for it.
