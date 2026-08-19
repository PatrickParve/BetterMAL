## 1. Recap → my-list handoff vocabulary

- [x] 1.1 In `RecapPage.tsx`, extend `myListScopeSearch` (or add a thin wrapper beside it) to append the design's single `focus` parameter — `completed`, `dropped`, `watching`, `movies`, `score-1` … `score-10`, or omitted for the unfocused scope — leaving the existing `recap`-prefixed params byte-identical so the current "See all N in my list" link is unchanged.
- [x] 1.2 Make the **Currently watching** target build its scope with the aired-attributed time filter regardless of the recap's own filter (design decision 4), so the tile's count and the list's row count agree under **What I watched**.
- [x] 1.3 Document the `focus` token vocabulary in a short comment at the helper, matching the existing note that explains why the scope params are `recap`-prefixed.

## 2. Clickable recap stat tiles

- [x] 2.1 Rework `renderStats` so each tile carries whether it is followable and, when it is, its target: **In this period** (no focus), **Completed**, **Dropped**, **Movies watched**, **Currently watching**; **Mean score**, **Episodes watched**, and **Time spent** stay inert.
- [x] 2.2 Render followable tiles as `<Link>` and inert tiles as the current `<div>`, keeping the tile box (padding, border, size) identical between the two so the grid never reflows. **Revised:** a followable tile whose own count is 0 also renders as the plain `<div>` (no navigation), but keeps the `--link` class so it still takes the accent highlight rather than falling back to the aggregate tiles' quieter hover.
- [x] 2.3 In `RecapPage.css`, move the accent hover treatment (`--accent-bg`, `--accent-border`, `var(--shadow)`) onto the shared `--link` class (both the `<Link>` and the zero-count `<div>` variant) and give the inert (aggregate) tiles a quieter background-only hover; scope `cursor: pointer` and the `:focus-visible` outline to the `<a>` element only, so a zero-count tile doesn't claim clickability it doesn't have.
- [x] 2.4 Verify each tile's count equals the row count my list shows after following it, in a season, a yearly, and a multi-year recap, under both time filters. Also verify a zero-count tile (e.g. no dropped anime that period) shows the accent highlight on hover but has no cursor/target/keyboard stop.

## 3. Clickable rating distribution rows

- [x] 3.1 Add an optional `hrefForScore?: (score: number) => string` prop to `ScoreDistribution.tsx`; when set, wrap each row's four cells in a `<Link>` **only when the bucket's count is greater than 0**; a zero-count bucket renders as a `<div>` carrying the same `--link` class (highlighted, not navigable); when `hrefForScore` is unset, render exactly today's markup so the profile page is untouched.
- [x] 3.2 In `ScoreDistribution.css`, give the row its hover-ready resting box (padding plus a transparent 1px border) and the shared accent hover/focus treatment, scoped so it applies to both the `<Link>` and zero-count `<div>` renderings and changes no column width or bar-track alignment; scope `cursor: pointer` and the focus outline to the `<a>` element only.
- [x] 3.3 Pass `hrefForScore` from `RecapPage.renderDistribution`, building `/my-list?…&focus=score-N` from the page's current period, filter, and media type.
- [x] 3.4 Confirm a zero-count row still takes the accent highlight on hover but is not a link (no navigation, no cursor, no keyboard stop), and that the profile page's distribution renders and measures exactly as before.

## 4. My-list controls the drill-downs need

- [x] 4.1 Widen `ScoreFilter` in `MyListPage.tsx` to carry a specific score value alongside `any`/`rated`/`unrated`, and add the ten score options to the score `<select>` between Rated and Unrated.
- [x] 4.2 Apply the score-value case in the `derived` filter pipeline (`item.entry.myScore === value`).
- [x] 4.3 Add the `startedFilter` restorable boolean, its toggle button in the filter cluster (same `my-list-page__tab` style as "Group by status"), and its predicate (`item.entry.episodesWatched > 0`).
- [x] 4.4 Add both controls to `clearFilters` and `isOffDefault` so "Clear filters" appears for and resets them.

## 5. Applying an arriving focus on my list

- [x] 5.1 Parse `focus` from the URL in `MyListPage.tsx` into the design's control-value mapping (decision 3), treating an unknown or absent token as "no narrowing".
- [x] 5.2 Seed `statusFilter`, `typeFilter`, `scoreFilter`, and `startedFilter` from that mapping via each `useRestorableState` call's `initial` argument — no arrival effect, no URL rewrite — so a back/forward restore still wins over the seed.
- [x] 5.3 Delete `focus` alongside the `recap` keys in `dismissRecapScope`, so dismissing cannot re-seed the controls from a stale token.
- [x] 5.4 Check the arrival cases end to end: controls visibly set, adjustable afterwards without being reverted, "Clear filters" working within the scope, and back-navigation restoring what was left rather than what arrived.

## 6. Ranking column alignment

- [x] 6.1 In `RecapPage.tsx`, drop the `.recap-page__ranking-column` wrappers from the multi-year layout and render the four ranking sections as direct children of `.recap-page__rankings` with explicit grid placement (season score 1,1 · seasons-by-time 1,2 · year score 2,1 · years-by-time 2,2).
- [x] 6.2 In `RecapPage.css`, make `.recap-page__rankings` a two-column two-row grid whose rows size to their tallest item, keeping the single-column collapse when only one level has rankings and at the narrow breakpoint.
- [x] 6.3 Leave `RankingSection`'s "See all" threshold as it is — a ranking showing every row still offers no control.
- [x] 6.4 Verify with a 2020–2024 recap that seasons-by-time-watched and years-by-time-watched start on the same line although only the season ranking carries a "See all", and re-check the yearly mode's side-by-side pair and the narrow-viewport stacking.

## 7. Hot-take column alignment

- [x] 7.1 In `RecapPage.css`, give `.recap-hot-take__scores`' two score cells fixed `ch`-based widths with tabular numerals, and `.recap-hot-take__direction` a fixed min-width sized for "MAL liked it more", centred, leaving the title cell as the flexible one. **Also:** give `.recap-hot-take__scores` its own `align-items: center` — without it the two score cells defaulted to `stretch`, and since MAL's score renders through `ScoreValue` (itself a self-centering `inline-flex`) while my score is plain text with nothing centering it, my score sat visibly higher than MAL's score and the direction pill beside it.
- [x] 7.2 Confirm alignment holds across rows with long and short titles, one- and two-digit scores, both direction labels, and with scores hidden so a MAL score renders concealed — including that my score and MAL's score sit on the same vertical centre as each other and as the direction pill.

## 8. Search field hover

- [x] 8.1 In `SearchBar.css`, extend the `.search-bar:focus-within` rule to `.search-bar:hover` so hover draws the same inset ring and accent border, and confirm the magnifier's own hover state and the field's size are unaffected.

## 9. Verification and docs

- [x] 9.1 Run `npm run lint` and `npm run build` in `frontend/` under node 22 (`nvm use 22`), and fix anything they report.
- [x] 9.2 Walk the recap page at a wide and a narrow viewport in both colour themes, checking that no hover treatment reflows the stat grid, the distribution, the rankings, or the hot-take list.
- [x] 9.3 Update `CODE_GUIDE.md`'s `RecapPage` and `MyListPage` rows to mention the `focus` handoff, and its `MyListPage` row to mention the score-value and Started filters.
