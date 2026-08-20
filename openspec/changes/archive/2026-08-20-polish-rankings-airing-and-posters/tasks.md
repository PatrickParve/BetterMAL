## 1. Schedule: mark today

- [x] 1.1 In `AiringPage.tsx`, compute today's local ISO date once per render with the existing `todayIso()` helper and compare it to each `day.localDate` while mapping the seven columns (design D1).
- [x] 1.2 Add `airing-day__header--today` to the matching column's header and `aria-current="date"` to that column, leaving the header's text content and position exactly as they are for every other column.
- [x] 1.3 In `AiringPage.css`, style `--today` as a filled accent chip: `--accent-bg` background, `--accent` text, the existing 1px bottom border recoloured to `--accent`, font-weight 700, and a small radius on the fill — no change to the header's height or padding.
- [x] 1.4 Verify in the browser: today's column is marked on the current week; navigating one week back or forward marks nothing; all seven headers stay the same height and the slots beneath them start on one line.

## 2. Top anime: one medal palette

- [x] 2.1 Delete the `--medal-gold/silver/bronze` (+ `-bg`, `-border`) block and its now-superseded comment from `.top-anime-page` in `TopAnimePage.css`, so the page inherits `index.css`'s `:root` medal tokens (design D2).
- [x] 2.2 Rewrite `.top-anime-showcase__item--rank-1/2/3` to set the podium's three local aliases (`--medal`, `--medal-bg`, `--medal-border`) instead of naming a medal colour per property.
- [x] 2.3 Rewrite the shared `.top-anime-showcase__item` rule to read only those aliases: medal border, `linear-gradient(var(--medal-bg), var(--medal-bg)), var(--code-bg)` surface, plus `position: relative` and `overflow: hidden`.
- [x] 2.4 Add the podium's medal band as `.top-anime-showcase__item::before` — 44px tall, `linear-gradient(180deg, var(--medal-bg), transparent)`, `pointer-events: none` — and give `.top-anime-showcase__info` / `__poster-link` a stacking context above it if needed.
- [x] 2.5 Keep rank 1's existing ring-plus-elevation and its hover/focus variants, now expressed with `var(--medal-border)`, so rank 1 still reads as the most prominent of the three.
- [x] 2.6 Confirm no podium motion follows: no `@keyframes`, `animation`, transform-lift, or sheen is added to this page.
- [x] 2.7 Verify side by side with the recap podium in light and dark mode that rank 1, 2, and 3 carry the same medal colours on both pages.

## 3. Top anime: rank badge and score chips

- [x] 3.1 Change `.top-anime-rank--lg` to the podium's badge form: 28px circle, `2px solid var(--medal)` border, `var(--bg)` fill, `var(--medal)` text, tabular numerals (design D4).
- [x] 3.2 Drop the `#` from the showcase rank in `TopAnimePage.tsx` so the badge carries the bare number, leaving the flat rows' `#N` text untouched.
- [x] 3.3 Leave `.top-anime-rank--sm` (ranks 4–10, drawn over poster art) exactly as it is, per design D4.
- [x] 3.4 Tighten `.top-anime-showcase__scores .score-chip` padding and add a `max-width` to `.top-anime-showcase__scores` so the pair no longer spans the full info column, keeping `flex: 1 1 0` and `align-self: stretch` so the two chips stay equal width.
- [x] 3.5 Raise the chip label to 12px and the value to 14px within the showcase scope only.
- [x] 3.6 Verify the two chips are still identical in width and height with "My score" beside "MAL", and that the label reads as a label rather than as fine print.

## 4. Top anime: uncropped posters

- [x] 4.1 Change `.top-anime-showcase__picture` from `aspect-ratio: 2 / 3` to `260 / 368` (its `width: 140px` is definite, so `aspect-ratio` is safe here — design D6).
- [x] 4.2 In `TopAnimePage.tsx`, wrap the ranks 4–10 card image (and its placeholder) in a `.top-anime-card__picture-frame`, leaving the `--sm` rank badge a direct child of `.top-anime-card__link` so its absolute placement is unchanged.
- [x] 4.3 In `TopAnimePage.css`, size the frame with `padding-top: calc(368 / 260 * 100%)` and absolutely fill it with `.top-anime-card__picture`, mirroring `.recap-podium__picture-frame`; carry over the comment explaining why the padding technique is used inside an `fr` track.
- [x] 4.4 Verify against an anime's own detail page that the showcase and top-ten posters show the same extent of artwork, and that the 7-up card row still lines up at desktop and at the `max-width: 1024px` breakpoint.

## 5. Reveal-control alignment

- [x] 5.1 Add `.top-anime-showcase__scores .score-value { justify-content: flex-start }` so a hidden MAL score's control starts where the score — and the "My score" chip's `—` — starts (design D7).
- [x] 5.2 Add `.top-anime-card__scores .score-value { justify-content: flex-end }` so the ranks 4–10 control ends at the card's trailing edge, mirroring my score at its leading edge.
- [x] 5.3 Add `.top-anime-row__mal-score .score-value { justify-content: flex-end }` for the flat rows' right-aligned MAL column, which has the same mismatch.
- [x] 5.4 Leave `ScoreValue.tsx` and `ScoreValue.css`'s `min-width: 4ch` default untouched, so every other surface keeps its centred control and no slot changes width.
- [x] 5.5 Verify with the hide toggle on and off: revealing a score in each of the three layouts shifts nothing, and the control lines up with the neighbouring `—` or with the score's own edge.

## 6. Landscape detection

- [x] 6.1 Add `frontend/src/hooks/useLandscapePicture.ts` exporting a hook that returns a callback ref and an `isLandscape` flag, reading `naturalWidth > naturalHeight` immediately when the element is already `complete` and otherwise on a one-shot `load` listener (design D8).
- [x] 6.2 Reset the flag when the `src` it was given changes, so navigating between anime cannot carry one picture's orientation onto the next.
- [x] 6.3 Comment the hook with why a callback ref is used rather than `onLoad` (a cached image can complete before React attaches the handler).

## 7. Landscape: hero images

- [x] 7.1 Use the hook in `AnimeDetailPage.tsx` for `.anime-detail-page__picture`, adding an `--landscape` modifier class when the flag is set; leave the placeholder branch alone.
- [x] 7.2 In `AnimeDetailPage.css`, give `--landscape` `height: auto` at the existing 260px width, so the picture column, the controls beneath it, and the boxes beside it keep their positions.
- [x] 7.3 Do the same for the series page header picture in `SeriesPage.tsx` / `SeriesPage.css`, with `aspect-ratio: auto; height: auto` at its existing clamped width.
- [x] 7.4 Verify on an anime with landscape art that the whole image is visible on both pages, and that a portrait anime and an anime with no picture render exactly as before.

## 8. Landscape: card tiers

- [x] 8.1 In `SeriesTimeline.css`, introduce `--card-w: 168px` on `.series-timeline__card`, drive its `width` from it, and replace the picture's `aspect-ratio: 2 / 3` with `height: calc(var(--card-w) * 3 / 2)` — an identical result for portrait cards (design D9).
- [x] 8.2 Add `.series-timeline__card--landscape { --card-w: 336px }` and pin the picture's height to the portrait value (`252px` via the same expression against the portrait width) with `object-fit: contain`, so pictures, titles, chips, and footers stay on one line across the row.
- [x] 8.3 Wire the hook into `SeriesTimeline.tsx`, putting the modifier on the card element (not just the image) so the card itself can widen.
- [x] 8.4 In `SeriesPage.css`, lift the extras grid's `12px` gap into a custom property on `.series-page__extras-grid` so the tile sizer below and the grid cannot drift apart.
- [x] 8.5 In `SeriesExtraTile.tsx`, wrap the picture in a `.series-extra-tile__picture-frame` and wire the hook so a landscape tile gets `--landscape` on the `<li>`.
- [x] 8.6 In `SeriesExtraTile.css`, keep portrait tiles rendering exactly as today, and for `--landscape`: `grid-column: span 2`, frame `width: calc((100% - var(--extras-gap)) / 2)` with `padding-top: calc((100% - var(--extras-gap)) * 0.75)`, and the image `height: 100%; width: auto; max-width: 200%` centred inside it.
- [x] 8.7 Verify a landscape timeline card and a landscape More tile: each shows its whole picture, is wider than its portrait neighbours, and keeps its picture's bottom edge, title, and footer aligned with them at several viewport widths.
- [x] 8.8 Verify that two landscape cards of very different durations are the same width as each other, so card width still carries no duration meaning.

## 9. Wrap-up

- [x] 9.1 Run the frontend type-check and build (Node 22 via nvm, per the repo's build note) and fix anything the change introduced.
- [x] 9.2 Re-read the change's spec deltas against the implementation and confirm each scenario is actually satisfied, particularly the no-reflow, equal-chip, badge-legibility, medal-theme, and card-width scenarios.
- [x] 9.3 Run `openspec validate polish-rankings-airing-and-posters` and confirm it passes.
