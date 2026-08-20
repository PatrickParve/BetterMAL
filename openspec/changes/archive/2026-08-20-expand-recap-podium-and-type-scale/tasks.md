## 1. Podium of five

- [x] 1.1 In `RecapPage.tsx`, grow `MEDALS` to `['gold', 'silver', 'bronze', 'plain', 'plain'] as const` and split `renderTopTen` at five: `topTen.slice(0, 5)` to the podium, `topTen.slice(5)` to `<ol className="recap-top-ten-list" start={6}>`, with each row's printed rank becoming `#{index + 6}` (design decision 1, spec scenario "The ten split into a podium and rows").
- [x] 1.2 Leave `TOP_TEN_SIZE`, the narrowing, the sort, and `renderPodiumCard`'s key untouched, so the ranking and the remount-on-a-new-set behaviour are byte-identical (spec scenarios "Stable tie order", "Unscored entries rank last", "Re-animating on a new set").
- [x] 1.3 Update `renderPodiumCard`'s comment to say the podium is the top five and that ranks 4–5 take the neutral `--plain` variant, and update the section comment above `.recap-podium` in `RecapPage.css` the same way.
- [x] 1.4 In `RecapPage.css`, add `.recap-podium__card--plain { --medal: var(--text); --medal-bg: transparent; --medal-border: var(--border); }` beside the three medal variants — no new tokens in `index.css` (design decision 1, spec scenario "Rank is colour-coded").
- [x] 1.5 Replace `.recap-podium`'s three columns with `minmax(0, 1.2fr) minmax(0, 1.1fr) minmax(0, 1fr) minmax(0, 0.85fr) minmax(0, 0.85fr)` and **delete `max-width: 640px`**, keeping `align-items: end` and the 16px gap (design decision 2, revised — see design.md; spec scenario "The podium fills its section").
- [x] 1.6 Replace the single `@media (max-width: 720px)` rule with the two-step reflow from design decision 3: at ≤900px a six-column grid with `:nth-child(-n + 3) { grid-column: span 2 }` and `:nth-child(n + 4) { grid-column: span 3 }`; at ≤560px one column with `grid-column: auto` (spec scenarios "Narrow displays reflow the podium", "The narrowest display stacks the podium").
- [x] 1.7 Check a period with 1, 2, 4, 5, and 10 entries: no placeholder card ever appears, the row list is absent at five or fewer, and it starts at #6 at more than five (spec scenarios "A period with two entries", "Five or fewer entries").

## 2. One score box across the five cards

- [x] 2.1 In `RecapPage.css`, scope the podium's chip: `.recap-podium__link .score-chip { flex: 0 0 auto; width: 68px; justify-content: center; }` with the value at 15px and `.recap-podium__link .score-value { min-width: 5ch; }` (design decision 4).
- [x] 2.2 Comment why the width is a fixed px rather than `ch` or a flex share, and why it is scoped to the podium rather than changed on `.score-chip--compact`, whose other call sites (`SeriesTimeline`, `SeriesExtraTile`) depend on `flex: 1 1 0` in a *row*.
- [x] 2.3 Verify the box holds every value the two bases print without resizing or wrapping: `10` and a one-digit my-score, MAL's `10.00` and `7.65`, the `—` placeholder, and the hidden-score reveal button (spec scenarios "Every card's score box is the same size", "A MAL score fits the same box").
- [x] 2.4 Confirm the five boxes are identical on the narrow cards as on the wide ones, at the 5-across floor (~900px) as well as at a wide viewport, and that `SeriesTimeline`/`SeriesExtraTile` chips are visually unchanged.

## 3. #1's wave

- [x] 3.1 In `RecapPage.css`, delete `@keyframes recap-podium-sheen` and the `.recap-podium__card--gold::after` rule that uses it.
- [x] 3.2 Add `@keyframes recap-podium-wave` translating `translate3d(0,0,0) → translate3d(480px,0,0)` and the new `.recap-podium__card--gold::after` exactly as in design decision 5: `z-index: 0`, `inset: -20% -520px`, two `linear-gradient(90deg, …)` layers at `background-size: 160px 100%, 240px 62%`, `background-position: 0 0, 0 100%`, `repeat-x`, `20s linear infinite`, `pointer-events: none`.
- [x] 3.3 Comment the two invariants the numbers encode: 480px is LCM(160, 240) so the loop is seamless in both layers, and the 520px overhang exceeds the 480px travel so no uncovered edge can scroll into view (spec scenario "The motion never stops").
- [x] 3.4 Comment why `z-index: 0` is what excludes the poster — the layer sits below `.recap-podium__link`'s `z-index: 1`, whose poster `<img>` is opaque — and why the score chip is lit by the same pass: `--mine-bg`/`--mal-bg` are `rgba(…, 0.15)` tints, so the wave reads through the chip at the same instant as through the card (spec scenarios "The poster is left alone", "The score lights with the card").
- [x] 3.5 Watch the gold card through several full cycles in both themes: motion is present at every instant, crosses the whole card, never jumps at the loop point, and reads as a slow drift rather than a sweep (spec scenarios "The motion covers the card", "The motion never stops").
- [x] 3.6 Check the highlight's peak against the card's title and score in both themes; adjust only the `color-mix` percentages (22% / 14%) if contrast suffers, leaving the geometry alone.
- [x] 3.7 Confirm the `@media (prefers-reduced-motion: reduce)` block still switches the decorative motion off along with the arrival and the hover response — update the selector it names from the deleted sheen rule to the new one (spec scenario "Reduced motion").
- [x] 3.8 Confirm no other card carries the wave, and that the card with no poster art shows the wave through its placeholder without the layer escaping the card's `overflow: hidden`.

## 4. Zero-count stat tiles

- [x] 4.1 In `RecapPage.tsx`'s `renderStats`, drop the conditional class from the `<div>` branch so it always renders `className="recap-page__stat"`, leaving the `(tile.count ?? 0) > 0` test that chooses `<Link>` vs `<div>` unchanged (design decision 6).
- [x] 4.2 Rewrite the comment above `renderStats` that currently explains the old behaviour: a zero-count followable tile now takes the aggregate treatment, so the accent rail always means "this leads somewhere".
- [x] 4.3 In `RecapPage.css`, fold `--link`'s rules to anchors now that the class only ever lands on an `<a>`: `a.recap-page__stat--link::before`, `a.recap-page__stat--link:hover`, `a.recap-page__stat--link:focus-visible`, and the reduced-motion override that names it.
- [x] 4.4 Verify on a period with nothing dropped: **Dropped** shows the same rail and the same quiet hover as **Time spent**, shows no pointer cursor, is not a keyboard stop, and keeps its box and its place in the grid (spec scenario "A zero-count stat reads as an aggregate tile").
- [x] 4.5 Verify the non-zero tiles are unchanged — accent rail, accent hover with the lift, pointer cursor, keyboard focus and follow (spec scenarios "A followable tile reads as clickable", "Keyboard reaches the followable tiles").

## 5. Board poster sharpness

- [x] 5.1 In `ScoreBoardOverlay.css`, change `.score-board__grid` to `repeat(auto-fill, minmax(150px, 1fr))`, keeping `auto-fill` (not `auto-fit`) so a one-item slot's tile matches a full slot's (design decision 7).
- [x] 5.2 Change `.score-board__tile`'s `padding-top: 150%` to `calc(600 / 425 * 100%)` and extend the existing comment: MAL's `large` poster is 425×600, so this box matches the art exactly and `object-fit: cover` no longer crops ~6% off each side; the percentage-padding technique and its WebKit rationale stay.
- [x] 5.3 Leave `image-rendering: high-quality`, the `top`-based lift, the tier borders, and the hover card untouched, and note in the comment that resolution — not compositing — is what this pass fixes.
- [x] 5.4 Verify on a high-density display: posters read sharply, none is cropped at the sides, the grid still fills the overlay's width with no sideways scrolling, and a large slot still wraps (spec scenarios "A poster reads sharply on a high-density display", "Posters are not cropped to a taller box", "The board still fits its slots").
- [x] 5.5 Verify the board's behaviour is unchanged at the new size: lift, hover card at the board's edges, keyboard focus, following a poster, and each slot's count against its distribution row (spec scenario "The board's behaviour is unchanged").

## 6. The type scale

- [x] 6.1 Apply the `RecapPage.css` rows of design decision 8's table: top-10 row `font-size: calc(1em + 1px)`, its poster 40×56 and rank cell 32px, podium title 16px, badge 14px in a 30px circle, stat figure 28px, stat label 13px, hot-take scores 15px and direction 13px, "See all" 15px.
- [x] 6.2 Apply the `RankingSection.css` rows: section heading 18px, ranking row `font-size: calc(1em + 1px)`, meta 14px, `--ranking-poster-h` 44px with a 30px poster width, "See all" 15px.
- [x] 6.3 Apply the `ScoreDistribution.css` rows: `--compact` row 14px and bar track 9px, leaving the non-compact block (the profile page's) untouched.
- [x] 6.4 Widen the lead grid's aside from `minmax(14rem, 20rem)` to `minmax(15rem, 22rem)` in `RecapPage.css` so the larger figures and labels keep the stat grid's two columns.
- [x] 6.5 Comment why the two row containers use `calc(1em + 1px)` rather than a fixed px — they inherit the fluid root size, and nothing inside them re-applies the rule, so it cannot compound.
- [x] 6.6 Check for clipping and overflow at the widths where the aside sits beside the top 10 and where it goes full width (the 1024px collapse): no stat figure or label clipped, no distribution row's bar track out of line with the others, ranking rows still one shared height, hot-take score and direction columns still aligned (spec scenarios "Nothing overflows at the larger size", "Hierarchy survives the increase").
- [x] 6.7 Confirm the profile page's own distribution block and every other page using `ScoreChip` or `RankingSection` are visually unchanged.

## 7. Verification

- [x] 7.1 Run `npm run lint` and `npm run build` in `frontend/` under Node 22 (`nvm use 22` — the default Node 16 cannot run Vite).
- [x] 7.2 Walk a season recap, a yearly recap under both time filters, and a multi-year recap: the podium shows five, the rows run 6–10, and both bases render correctly with the hide switch on and off (spec scenario "Hidden MAL scores stay hidden" must still hold).
- [x] 7.3 Switch ranking basis and media type from mid-page and confirm the scroll still holds, the podium re-animates for the new set, and no layout shifts while it does (spec scenarios "Re-animating on a new set", "The podium arrives with motion").
- [x] 7.4 Step through the widths: wide, 1024px (lead collapse), 900px (podium reflow to 3+2), and 560px (single column) — checking the podium, the stat grid, the distribution, and the rankings at each.
- [x] 7.5 Run the whole page once with `prefers-reduced-motion: reduce` set: no arrival, no wave, no hover motion, colour feedback intact, everything followable.
- [x] 7.6 Tab through the page end to end: five podium cards in rank order, rows 6–10, only the non-zero followable stat tiles as stops, the distribution rows, the score board button, and the board's posters.

## 8. Score board scroll and motion follow-up (post-verification, user-requested)

- [x] 8.1 In `ScoreBoardOverlay.css`, remove `position: sticky` from `.score-board__slot-header` (base rule) and drop the apex tier's `position: relative` override that had accidentally opted the 10 slot out of it — the archived "stays identifiable while scrolling" requirement is superseded by the spec delta above; the board is now a plain, non-pinning scrolling list end to end, confirmed by tracking `top + scrollTop` as constant for all ten tiers across the full scroll range.
- [x] 8.2 Replace the 10 slot's inherited `score-board-sheen` band sweep with a `score-board-wave` animation using the same geometry/timing as the podium's `recap-podium-wave` (design decision 5 of this change), so the two "first place" marks share one motion vocabulary.
- [x] 8.3 Fix two regressions the wave introduced: the numeral going illegible when the highlight passed behind it (fixed with a dark `drop-shadow`/`text-shadow` on the numeral and count, plus lowering the wave's alpha) and the highlight reading as silver/grey where the two wave layers overlap (fixed by tinting the highlight toward `var(--tier-apex)` via `color-mix`, `--wave-hi`/`--wave-lo`, rather than plain white, so it cannot desaturate toward a neutral colour at any point in its cycle).
- [x] 8.4 Verify in a real browser (Chromium and WebKit, light and dark themes): no tier's header bar pins or travels independently of the scroll; the 10 slot's motion drifts continuously and reads as purple (hue ~258–262°, meaningful saturation) at every sampled frame of a full cycle; the numeral and count stay legible throughout.
- [x] 8.5 Update `design.md`'s "Fewer, larger board tiles means more scrolling" risk note and the spec delta above to no longer cite sticky slot headers as a mitigation, and add the "The score board is dismissible…" and "The 10 slot shares the podium's first-place motion" deltas reconciling the archived score-board spec with the shipped behaviour.
