## 1. Page-scoped airing colour

- [x] 1.1 In `frontend/src/pages/SeriesPage.css`, add `--airing` / `--airing-bg` / `--airing-border` aliases to `--status-watching*` inside the `.series-page` block, next to the existing `--mal*` / `--mine*` aliases
- [x] 1.2 Extend that block's comment to say green means "on air now" on this page and is aliased so it can diverge from the Watching status colour

## 2. Airing pill on the card

- [x] 2.1 In `frontend/src/components/SeriesTimeline.tsx`, change `formatAirRange`'s `currently_airing` branch to return the start month/year alone instead of `"… – ongoing"`, and update its comment to say the airing label completes that line
- [x] 2.2 In `TimelineCard`, render `{airRange} · <span className="series-timeline__airing-tag">Airing</span>` in the second meta line for a currently-airing entry, leaving the undated and finished branches as they are
- [x] 2.3 Set that line's `title` to the long form (`"<range> · Currently airing"`) so an ellipsised line still reads in full on hover
- [x] 2.4 Add `.series-timeline__airing-tag` to `SeriesTimeline.css`, mirroring `.series-timeline__no-date-tag`'s geometry with `color: var(--airing)`, `background: var(--airing-bg)`, `border: 1px solid var(--airing-border)`

## 3. Remove the blue ring

- [x] 3.1 Replace `.series-timeline__card--airing`'s `box-shadow` + `animation` with `border-color: var(--airing)`, and rewrite its comment to explain the border-over-ring choice and that hover/focus intentionally recolours it like any other card
- [x] 3.2 Delete `@keyframes series-timeline-airing-pulse` and the `prefers-reduced-motion` block that suppressed it
- [x] 3.3 Delete the `{airing && <span className="series-timeline__sr-only">Currently airing</span>}` span from `TimelineCard` and the now-unused `.series-timeline__sr-only` rule
- [x] 3.4 Rewrite `.series-timeline__scroll`'s padding comment: the padding stays, but it is now justified by row breathing room and edge spacing, not by clearance for a glow

## 4. Verify

- [x] 4.1 Run the frontend typecheck/build (`npm run build` under nvm's Node 22) and confirm no unused-symbol or type errors from the removals
- [x] 4.2 Open a series with a currently-airing season and confirm: green `Airing` pill in the air-range line, green card border, no glow, no pulse, no badge over the poster, and the card the same size as its neighbours
- [x] 4.3 Confirm the airing card's aired fill is still blue and its score chips still blue/purple
- [x] 4.4 Check both light and dark themes, and check a series whose airing entry is the first and the last card in the row for clipping
- [x] 4.5 Confirm a finished-airing card still shows its full range and an undated card still shows its no-date tag, both unchanged
