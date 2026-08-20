## 1. Medal colour tokens

- [x] 1.1 In `frontend/src/index.css`, add `--medal-gold`, `--medal-gold-bg`, `--medal-gold-border` and the matching `--medal-silver-*` / `--medal-bronze-*` triples to `:root`, placed after the `--status-*` block and before the `--mal`/`--mine` aliases, following those tokens' `colour / rgba(…, 0.12) / rgba(…, 0.45)` shape (design decision 3).
- [x] 1.2 Add dark-theme values for all nine tokens in the existing `@media (prefers-color-scheme: dark)` block, in the same position.
- [x] 1.3 Comment the silver choice: a literal silver sits on top of `--border` in the light theme and disappears, so the token is a cool slate that reads as silver through the gold/silver/bronze family rather than through the literal hue.
- [x] 1.4 Check each of the six tint/border pairs against `--bg` and `--code-bg` in both themes — the badge numeral must be legible on its own tint, and the three ranks must be tellable apart at badge size.

## 2. Podium markup

- [x] 2.1 In `RecapPage.tsx`, split `renderTopTen`'s `topTen` into `podium = topTen.slice(0, 3)` and `rows = topTen.slice(3)`, leaving the narrowing, sorting, and slicing above it untouched (design decision 1).
- [x] 2.2 Extract a `renderPodiumCard(item, index, effectiveBasis)` helper rendering an `<li class="recap-podium__card recap-podium__card--gold|silver|bronze">` wrapping a single `<Link to={/anime/:id}>`: a rank badge, the poster (or the placeholder `<div>` used by the rows) inside an aspect-ratio frame, the title with a `title` attribute via `pickDisplayTitle`, and the score.
- [x] 2.3 Render the score with the existing `ScoreChip` — `role="mine"` with the raw `myScore` (or `—`) under the mine basis, `role="mal"` wrapping `<ScoreValue value={item.malScore} completed={item.malRevealed} />` under the MAL basis — so hide/reveal keeps working untouched (proposal: reuses existing components).
- [x] 2.4 Render `<ol class="recap-podium">` with the three cards in rank order, then, only when `rows.length > 0`, the existing `<ol class="recap-top-ten-list" start={4}>` with the row markup unchanged (design decision 1 — `start` is what keeps the two lists one ranking for assistive technology).
- [x] 2.5 Give each card a `key` that includes the period, filter, basis, and type narrowing alongside `item.animeId`, so React remounts the cards when the set genuinely changes and reuses them otherwise — this is what drives the entrance animation's re-run (design decision 4, spec scenario "Re-animating on a new set").
- [x] 2.6 Keep `<h2>Top {Math.min(TOP_TEN_SIZE, narrowed.length)}</h2>`, the controls header, the empty-state note, and the "See all N in my list" link exactly as they are.

## 3. Podium styles

- [x] 3.1 In `RecapPage.css`, add `.recap-podium` as a three-column grid — `minmax(0, 1fr) minmax(0, 1.24fr) minmax(0, 1fr)`, `align-items: end`, `gap: 12px`, list reset — and place the cards by rank with `grid-column: 2` on the first, `1` on the second, `3` on the third (design decision 2).
- [x] 3.2 Style `.recap-podium__card`: `--medal*` aliases set per rank modifier, a `--medal-border` border over a `--medal-bg`-tinted `--code-bg` ground, `border-radius: 14px`, `overflow: hidden`, and a medal band across the top that the badge sits in.
- [x] 3.3 Give the first-ranked card its raise as `+28px` of fixed card height (padding/poster height, not a percentage), so `align-items: end` lifts it above the other two and the podium's total height stays bounded (design risk: podium height).
- [x] 3.4 Style the rank badge — a `--medal`-coloured ring with the numeral in tabular figures — and the poster frame with `aspect-ratio: 2 / 3`, `object-fit: cover`, and the placeholder variant borrowing `.recap-top-ten-row__picture--placeholder`'s treatment.
- [x] 3.5 Clamp the title to two lines with `-webkit-line-clamp` and reserve both lines' height unconditionally, so a one-line and a two-line title produce cards of identical height (spec scenario "A long title does not change the card").
- [x] 3.6 Add the `@media (max-width: 720px)` collapse: one column, every card at the #1 size and height, `grid-column: auto` on all three, so rank order and visual order agree (spec scenario "Narrow displays stack the podium").
- [x] 3.7 Verify the two- and one-entry cases place correctly — two cards leave column 3 empty with #1 centred and #2 left; one card sits alone in the centre column; no placeholder card is rendered (spec scenario "A period with two entries").
- [x] 3.8 Verify the podium at the lead grid's left-column width and again below the 1024px lead collapse where it goes full-width.

## 4. Podium motion

- [x] 4.1 Add a `recap-podium-rise` keyframe (`translateY(12px)` + `opacity: 0` → none + `1`) and apply it to `.recap-podium__card` at `420ms cubic-bezier(0.22, 1, 0.36, 1) both`, with `animation-delay` staggered `0ms / 80ms / 160ms` by rank modifier (design decision 4).
- [x] 4.2 Add the hover response: `transform: translateY(-4px) scale(1.015)` plus a `--medal`-tinted glow, `transition` 160ms, on `:hover` and `:focus-within` — `transform`/`opacity` only, so nothing reflows (spec: "The podium is animated").
- [x] 4.3 Add a `recap-podium-sheen` keyframe translating a narrow `linear-gradient` highlight across the first-ranked card's medal band on a ~5s loop, drawn as a `::after` inside the card's `overflow: hidden`, `pointer-events: none`, `aria-hidden` by construction.
- [x] 4.4 Add one `@media (prefers-reduced-motion: reduce)` block covering all three: `animation: none`, `transition: none`, and no hover `transform` — leaving the colour part of the hover treatment intact (spec scenario "Reduced motion").
- [x] 4.5 Verify the entrance re-runs when the period, time filter, ranking basis, or media-type narrowing changes, and does not re-run when an unrelated re-render happens (e.g. opening and closing a ranking overlay).
- [x] 4.6 Verify with reduced motion switched on at the OS level that the podium renders in place immediately, hover still tints, and every card is still followable.

## 5. Stat tiles

- [x] 5.1 In `RecapPage.css`, make `.recap-page__stat` `position: relative` and add the leading rail as a `::before` — 3px, full height, inline-start edge, inside the padding box — so no tile's box changes (design decision 5).
- [x] 5.2 Colour the rail `--accent` on `.recap-page__stat--link` and `--border` on the plain aggregate tile, so followable and aggregate are distinguishable at rest (spec scenario "Followable and aggregate tiles differ at rest").
- [x] 5.3 Raise `.recap-page__stat-value` to 26px with `font-variant-numeric: tabular-nums`, and set `.recap-page__stat-label` in uppercase with letter-spacing at its current size, keeping it subordinate to the figure.
- [x] 5.4 Add `transform: translateY(-2px)` to `.recap-page__stat--link:hover, :focus-visible` on top of the existing accent tint/border/shadow, extend the tile's `transition` to cover `transform`, and leave the aggregate tiles' quieter background-only hover as it is.
- [x] 5.5 Add the reduced-motion guard for the tile lift alongside the podium's block in task 4.4.
- [x] 5.6 Verify all eight tiles still fit the two-column grid at the aside's `minmax(14rem, 20rem)` width without a label wrapping mid-word, that hovering never moves a neighbouring tile, and that a zero-count followable tile still shows the accent rail and highlight without a pointer cursor.

## 6. Segmented control states

- [x] 6.1 In `RecapPage.css`, replace the `.recap-page__tab--active, .recap-page__tab--active:hover` pair with the five-state table from design decision 6, keeping the resting and unselected-hover rules as they are.
- [x] 6.2 Give `--active` its accent fill plus a `box-shadow: 0 0 0 3px var(--accent-bg)` halo — `box-shadow`, so the control's box and the cluster's shared `--control-h` height are untouched.
- [x] 6.3 Add the distinct selected-hover state: fill lightened via `color-mix(in srgb, var(--accent) 84%, white)`, halo widened to 5px, `translateY(-1px)`, so hovering the already-chosen option is no longer inert (spec scenario "Hovering the already-selected option").
- [x] 6.4 Add `.recap-page__tab:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px }` — there is none today — and confirm it is visually distinct from the hover treatment.
- [x] 6.5 Confirm `:disabled` takes neither the hover treatment nor the lift, keeping its current `opacity: 0.4` and `not-allowed` cursor, and that the disabled-option `title` tooltips still appear.
- [x] 6.6 Add the tab lift to the reduced-motion block from task 4.4.
- [x] 6.7 Verify all three groups — mode tabs, time filter, basis toggle — pick the rules up identically, that no state changes any control's height, and that the clusters stay flush with the `--control-h` selects and the round stepper arrows beside them.

## 7. Season-page button

- [x] 7.1 In `RecapPage.tsx`, keep `renderSeasonPageLink`'s `<Link>` element (it must stay a real anchor — design decision 7) and swap its class to `recap-page__season-button`, shortening the label to "Browse the season" with a trailing `›` and a leading glyph.
- [x] 7.2 In `RecapPage.css`, replace `.recap-page__season-link` with `.recap-page__season-button`: `height: var(--control-h)`, `box-sizing: border-box`, pill radius, `--border` border, `--bg` ground, inline-flex centring, and the app's standard control hover and `:focus-visible` treatment.
- [x] 7.3 Verify in season mode that the button's top and bottom align with the season and year selects and the round stepper arrows beside it, and that ⌘-click still opens the season page in a new tab (spec scenario "The control is still a link").

## 8. Navbar score-switch knob

- [x] 8.1 In `Navbar.css`, shrink `.navbar__score-switch-knob` to 26px square, centre it with `top: 50%; transform: translate(0, -50%)`, and set `left: 3px` so it clears the pill's border on every side (design decision 8).
- [x] 8.2 Replace the knob's `box-shadow: var(--shadow)` — whose primary layer is offset 10px downward — with a tight `0 1px 2px rgb(0 0 0 / 0.18)` that cannot reach the border from 3px inside it.
- [x] 8.3 Change the checked transform to `translate(calc(100% - 26px - 6px), -50%)` — expressed against the track width so both end gaps stay equal — replacing the hard-coded `translateX(66px)`, and update the comment above the knob, which currently explains the now-removed flush-to-the-cap sizing.
- [x] 8.4 Change `.navbar__score-switch-track`'s reserved width from `calc(100% - 34px)` to `calc(100% - 32px)` to match the knob's new footprint (26px + two 3px gaps), so the "Scores" label stays centred in the space the knob leaves.
- [x] 8.5 Verify in both themes and both states that the pill's border is unbroken all the way round with no shadow falling across it, that the eye is centred in the knob, and that the gaps at the two ends are equal.
- [x] 8.6 Verify the switch is still exactly 100px wide and `--control-h` tall, that toggling reflows nothing, and that the existing reduced-motion block still covers the knob's transition.

## 9. Whole-page verification

- [x] 9.1 Run `npm run lint` and `npm run build` in `frontend/` under node 22 (`nvm use 22` — the default node is v16 and Vite will fail on it).
- [x] 9.2 Walk all three recap modes, both time filters, both ranking bases, and a media-type narrowing, confirming the top 10 still holds the same ten anime in the same order as before this change, with unscored last and titles breaking ties.
- [x] 9.3 Confirm hidden MAL scores stay hidden on the podium under the MAL basis, and that a per-score reveal there behaves exactly as it does in a row.
- [x] 9.4 Confirm the rows below the podium are numbered 4–10 and keep their existing hover treatment unchanged, and that the hot takes, rankings, and rating distribution are untouched.
- [x] 9.5 Tab through the whole page: podium cards in rank order, then rows, then stat tiles, then controls — every stop showing a visible focus indicator.
- [x] 9.6 Check the page at a narrow width (below 720px), at the 1024px lead-grid collapse, and at full width, confirming nothing overflows horizontally and the podium reads correctly at each.
