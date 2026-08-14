## 1. Detail page broadcast progress

- [x] 1.1 Add an optional `aired?: number | null` prop to `ProgressBar` (`frontend/src/components/ProgressBar.tsx`); when it is absent or `null`, render exactly as today
- [x] 1.2 Compute the aired fill width in `ProgressBar`: `aired/total` with a known total, a fixed half track when the total is unknown but `aired` is known, nothing when neither is known; clamp to the track
- [x] 1.3 Measure the watched fill within the aired extent against `aired` when the total is unknown and an aired fill is drawn, so being caught up covers the aired extent exactly and never overshoots it
- [x] 1.4 Add the aired fill element and its blue styling to `ProgressBar.css`, layering the purple watched fill on top within the same track (mirror `AiringProgressBar.css`'s track/fill layering)
- [x] 1.5 Give the track a title/aria description naming aired and watched counts when an aired fill is drawn
- [x] 1.6 Pass `aired={detail.airingStatus === 'currently_airing' ? detail.episodesAired : null}` from `AnimeDetailPage.tsx`, leaving the existing `max`, increment, and in-place edit wiring untouched
- [x] 1.7 Check no other `ProgressBar` call site (My List, carousel, season, search, top anime) renders differently

## 2. Timeline time display

- [x] 2.1 Delete the year ruler from `SeriesTimeline.tsx`: the `.series-timeline__axis` markup, `assignYearMarks`, `YearMark`, `MIN_LABEL_GAP_PX`, and the `CARD_WIDTH`/`CARD_MARGIN`/`CARD_STRIDE` constants that only fed its math; delete the matching axis rules from `SeriesTimeline.css`
- [x] 2.2 Add an air-range formatter: `Apr – Jun 2013` within one year, `Oct 2013 – Mar 2014` across years, a single date for a one-day entry, and `Apr 2026 – ongoing` while still airing
- [x] 2.3 Rework the card meta block into two fixed single lines — media type and episode count on one, the air range (or the existing no-date indicator) on the other — each reserving the same space on every card so row alignment holds
- [x] 2.4 ~~Add a gap formatter over day numbers...~~ Done, then removed at the user's request after review — see 5.1.
- [x] 2.5 ~~Render a fixed-width connector between each pair of adjacent cards...~~ Done, then removed at the user's request after review — see 5.1.
- [x] 2.6 Verify a series with a multi-year gap presents no year labels anywhere — still true with no connector: no year appears anywhere on the timeline, dated or not

## 3. Airing ring and card chrome

- [x] 3.1 Remove the `.series-timeline__airing-badge` element from `TimelineCard` and its CSS rule
- [x] 3.2 Add a `--airing` modifier class for a currently-airing card, styled as a `box-shadow` ring in `--mal` plus a soft glow, so no layout shifts and no card resizes
- [x] 3.3 Keep the ring visible under `:hover`/`:focus-within`, ensuring the existing accent border does not replace or hide it
- [x] 3.4 Add visually-hidden "Currently airing" text to the airing card so the graphical ring has an accessible equivalent
- [x] 3.5 Wrap any ring animation in `@media (prefers-reduced-motion: reduce)` so it is suppressed when asked for
- [x] 3.6 Remove the status-coloured left border: the `border-left` on `.series-timeline__card`, the five `--watching`/`--completed`/`--plantowatch`/`--onhold`/`--dropped` rules, and the `statusClass` computation in `TimelineCard`; leave the footer status text and the dashed no-date border in place

## 4. Verification

- [x] 4.1 Build the frontend (`npm run build` under node 22) and confirm no TypeScript or lint errors, including no unused imports left by the deleted ruler and status-class code
- [x] 4.2 Check a detail page for a currently airing anime, a finished one, and one with an unknown total: blue fill only while airing, purple layered on top, label and increment unchanged
- [x] 4.3 Check a series page with a currently-airing season: the ring is obvious against a light poster and a dark one, no badge remains, and no status colours on cards

## 5. Follow-up fixes from user review

- [x] 5.1 Remove the gap-connector feature entirely (`formatGap`, `monthsBetween`, `connectorLabel`, `TimelineConnector`, `entryEndDay`, `dayNumber`, `DAY_MS`, the `Segment`/`buildSegments` plumbing, and the connector CSS rules); restore plain fixed spacing between cards (`gap: 12px` on `.series-timeline__row`) and derive a card's `undated` flag inline from `entry.airedFrom === null`
- [x] 5.2 Fix the airing ring being clipped on top, bottom, and whichever edge (left/right) a card sits at the end of the row: give `.series-timeline__scroll` padding on every side so the ring's box-shadow has room before the container's own `overflow-y: hidden` (and `overflow-x: auto`'s matching ink-overflow clip) cuts it off, and trim the pulse animation's peak size to comfortably fit that padding
- [x] 5.3 Hide `.series-timeline__scroll`'s horizontal scrollbar chrome (`scrollbar-width: none`, `-ms-overflow-style: none`, `::-webkit-scrollbar { display: none }`) while keeping the row scrollable, so a franchise with many seasons doesn't show a scrollbar track under the cards
- [x] 5.4 Update `proposal.md`, `design.md`, and `specs/series-page/spec.md` to drop the gap-connector requirement/scenarios and document the ring-clipping and scrollbar-hiding behaviour
- [x] 5.5 Rebuild the frontend (`npm run build` under node 22, confirm no TS/lint errors) and re-verify visually: ring renders in full on all sides in both light and dark mode, no connector labels/lines remain, and an overflowing timeline scrolls without a visible scrollbar
