## Context

Five presentation changes, all on the recap page, all on machinery that already exists:

- **The podium** is `RecapPage.renderTopTen`: it narrows `recap.items` by media type, sorts by the selected basis, slices ten, then splits `topTen.slice(0, 3)` into `<ol class="recap-podium">` (rendered by `renderPodiumCard`) and `topTen.slice(3)` into `<ol class="recap-top-ten-list" start={4}>`. `.recap-podium` is a three-column grid, `minmax(0, 1.2fr) minmax(0, 1.05fr) minmax(0, 0.95fr)`, `align-items: end`, **`max-width: 640px`** — the cap that leaves the empty band, since the lead grid's first column is the page width less a `minmax(14rem, 20rem)` aside. Cards read their rank colour through one local alias set (`--medal`, `--medal-bg`, `--medal-border`) assigned by `--gold|--silver|--bronze`; `MEDALS` in `RecapPage.tsx` is a 3-tuple that indexes straight off the card's array position.
- **#1's motion** is `.recap-podium__card--gold::after`: a 40%-wide, 44px-tall white band at `z-index: 2` (above `.recap-podium__link`'s `z-index: 1`), `translateX(-120% → 220%)` over 5s. Off-card for roughly half of every cycle, and confined to the medal band across the card's top edge.
- **The podium score** is `<ScoreChip role size="compact">`. `.score-chip--compact` is `flex: 1 1 0` — but the podium's link is `flex-direction: column`, so that flexes the chip's *height*, not its width, and `align-items: center` leaves its width content-sized. Rank 1 showing `10` and rank 4 showing `8` therefore get different boxes, at an 11px value.
- **Stat tiles** are `renderStats`: a tile with a target and a non-zero count renders as `<Link class="recap-page__stat recap-page__stat--link">`; a tile with a target and a **zero** count renders as a `<div>` carrying **the same `--link` class**; an aggregate tile as a bare `<div>`. `--link` drives both the accent rail (`.recap-page__stat--link::before`) and the accent hover, so a zero-count tile advertises a destination it does not have.
- **The score board's tiles** are `.score-board__grid { repeat(auto-fill, minmax(100px, 1fr)) }` with `.score-board__tile { padding-top: 150% }`. At the board's own `min(1080px, 100%)` width that resolves to 6–9 tracks of ~105px. MAL's `large` poster — the one `PictureUrl` stores, per `MalMappingExtensions` (`MainPicture?.Large ?? MainPicture?.Medium`) — is **425×600**, i.e. a 1.412 ratio, so the 1.5 box also crops ~6% off each poster's sides under `object-fit: cover`. A previous pass already ruled out compositor-layer staleness here by moving the hover lift from `transform` to `top`; what is left is plain resolution.
- **The type scale**: the root font is fluid, `clamp(16px, 14px + 0.3125vw, 18px)`, and the recap's rows (top 10, rankings, hot takes) inherit it. What reads as fine print is everything that overrides it downward — stat label 12px, distribution rows 13px, ranking meta 13px, podium title 14px, hot-take direction 12px — beside a 26px stat figure and 200px-plus poster art.

## Goals / Non-Goals

**Goals:**

- Five anime on the podium, filling the section's width, with ranks 1–3 larger than they are today and 4–5 clearly subordinate to them.
- One score box size across all five cards, sized for the widest figure either ranking basis prints.
- #1's motion becomes an ambient, seamless, whole-card drift that carries the score with it and leaves the poster alone.
- A tile that carries the accent marker always leads somewhere.
- Board posters drawn near their source resolution, in the source's own proportion.
- One raised text scale across the recap's supporting text, with nothing clipped or reflowed by it.

**Non-Goals:**

- Changing *which* anime the top 10 holds, what it ranks on, how ties break, or how many entries it shows. `TOP_TEN_SIZE` stays 10; the podium/row split is a slice boundary.
- Any backend, DTO, or request change.
- New global colour tokens. Ranks 4–5 and the wave both compose from tokens `index.css` already defines.
- Restyling the recap's controls, the overlays, or `ScoreChip`'s other two call sites (`SeriesTimeline`, `SeriesExtraTile`) — the podium's chip sizing is scoped to the podium.
- A podium anywhere else on the app.

## Decisions

### 1. Five cards from one slice; ranks 4–5 are a neutral variant of the same card

`renderTopTen` splits at 5 instead of 3: `topTen.slice(0, 5)` to the podium, `topTen.slice(5)` to `<ol class="recap-top-ten-list" start={6}>`, with the rows' printed rank becoming `#{index + 6}`. Nothing else about the ranking changes, so the two lists cannot disagree about rank — the same already-sorted array is sliced twice.

`renderPodiumCard` keeps its shape; `MEDALS` grows to `['gold', 'silver', 'bronze', 'plain', 'plain'] as const`, and `.recap-podium__card--plain` assigns the same three local aliases from neutral tokens:

```css
.recap-podium__card--plain { --medal: var(--text); --medal-bg: transparent; --medal-border: var(--border); }
```

Every rank-coloured surface in the card — border, medal band, badge, hover glow — is already written once against those aliases, so ranks 4 and 5 come out as quieter versions of the same card with no new rules and no new tokens. The card's key, which is what makes React remount and re-run the entrance animation on a genuinely new set, is unchanged.

*Alternatives considered.* Rendering 4–5 from a separate component would duplicate the card's markup for a purely tonal difference. Giving them their own medal-like colour (a fourth and fifth hue) would break the "gold/silver/bronze means the podium" reading that the score board also borrows.

### 2. One descending row, sized by `fr`, with the cap removed

```css
.recap-podium {
  grid-template-columns:
    minmax(0, 1.2fr) minmax(0, 1.1fr) minmax(0, 1fr) minmax(0, 0.85fr) minmax(0, 0.85fr);
  align-items: end;
  gap: 16px;
  /* max-width: 640px — deleted */
}
```

`fr` rather than fixed widths because the section's width varies with the viewport *and* with the lead aside's `rem`-based track under the fluid root font; ratios keep the descent proportional at every width. Ranks 4 and 5 are equal to each other — the row reads as "three medals, then two runners-up" rather than as five arbitrary sizes, and the spec only requires each card be no larger than the one before.

Worked example at a 1440px viewport: the lead column resolves to ≈980px, less four 16px gaps = 916px over 5fr → #1 ≈ 220, #2 ≈ 202, #3 ≈ 183, #4/#5 ≈ 156. Against today's capped row (#1 = 228px, #3 = 180px) ranks 1–2 land close to where they sit today rather than growing past it, 4–5 land between rank 3 and the row list's 40px thumbnail, and the band of empty space is gone. `align-items: end` continues to bottom-align the row, so the descent shows as tops trailing down and away from #1.

*Revised from an initial 1.45/1.25/1.05/0.75/0.75 split*, which read #1 at ≈253px — bigger than "artwork, not a thumbnail" called for and disproportionate against 4–5. The flatter 1.2/1.1/1/0.85/0.85 split still fills the section edge to edge (the "no empty band" requirement doesn't depend on the specific ratios, only on `fr` doing the filling) and still descends strictly, but gives ranks 1–3 less of the row's width and 4–5 more of it.

### 3. Reflow in two steps, by explicit spans rather than by auto-fit

Five cards side by side stop working around 900px, where #4/#5 fall under ~115px. Two breakpoints:

```css
@media (max-width: 900px) {
  .recap-podium { grid-template-columns: repeat(6, minmax(0, 1fr)); }
  .recap-podium__card:nth-child(-n + 3) { grid-column: span 2; }  /* 1–3 across line 1 */
  .recap-podium__card:nth-child(n + 4)  { grid-column: span 3; }  /* 4–5 across line 2 */
}
@media (max-width: 560px) {
  .recap-podium { grid-template-columns: 1fr; }
  .recap-podium__card { grid-column: auto; }
}
```

A six-column base is what lets 3-up and 2-up rows share one grid: 2+2+2 then 3+3. DOM order is rank order throughout, so reading and keyboard order never diverge from the numerals, and `align-items: end` bottom-aligns within each line. Below 560px every card is full width in one column, the shape the spec's "narrowest display" scenario describes.

*Alternative considered.* `repeat(auto-fit, minmax(150px, 1fr))` would reflow without breakpoints but would also equalise all five cards at every width, losing the descending silhouette that is the point of a podium.

### 4. The podium's score box is fixed, and scoped to the podium

```css
.recap-podium__link .score-chip {
  flex: 0 0 auto;
  width: 68px;
  justify-content: center;
}
.recap-podium__link .score-chip__value { font-size: 15px; }
.recap-podium__link .score-value { min-width: 5ch; }
```

68px is fixed rather than `ch`-derived so the box is identical on all five cards regardless of which basis is showing and of the fluid root font. The widest content is MAL's `toFixed(2)` (`ScoreValue`), i.e. `10.00` — five tabular characters, ≈45px at 15px — inside a 68px box with 4px of padding a side; `10` on the my-score basis and the hidden-score reveal button (a 13px eye) sit in the same box. 68px also fits the narrowest card the 5-across layout ever produces (≈115px wide, ≈91px of content box).

Scoped by descendant selector rather than by a new `size` on `ScoreChip`: the podium is the only place that needs a fixed box, and `ScoreChip`'s other call sites (`SeriesTimeline`, `SeriesExtraTile`) rely on `--compact`'s `flex: 1 1 0` to share a row. This also fixes the latent oddity that `flex: 1 1 0` in the podium's *column* flex container was stretching the chip vertically and never sizing it horizontally.

### 5. The wave: one oversized layer, two beating periods, one transform

Replaces `.recap-podium__card--gold::after` entirely.

```css
@keyframes recap-podium-wave {
  from { transform: translate3d(0, 0, 0); }
  to   { transform: translate3d(480px, 0, 0); }
}

.recap-podium__card--gold::after {
  content: '';
  position: absolute;
  z-index: 0;                       /* above the card's background, below .recap-podium__link's z-index: 1 */
  inset: -20% -520px;
  background-image:
    linear-gradient(90deg, transparent, color-mix(in srgb, var(--medal) 22%, transparent), transparent),
    linear-gradient(90deg, transparent, color-mix(in srgb, var(--medal) 14%, transparent), transparent);
  background-size: 160px 100%, 240px 62%;
  background-position: 0 0, 0 100%;
  background-repeat: repeat-x, repeat-x;
  animation: recap-podium-wave 20s linear infinite;
  pointer-events: none;
}
```

Four properties of the ask, each met by one part of that:

- **Never disappears.** Both layers tile horizontally, and the animation translates by 480px = LCM(160, 240), i.e. a whole number of periods of *both*. The end frame is pixel-identical to the start, so `infinite` loops with no seam and no gap — light is on the card at every instant, rather than sweeping off and leaving it inert.
- **Whole card, not a band.** `inset: -20% -520px` covers the card's full height (and overhangs 520px on each side, comfortably more than the 480px travel, so no uncovered edge ever scrolls into view). The card's existing `overflow: hidden` clips it.
- **Except the picture.** `z-index: 0` puts the layer *below* `.recap-podium__link`, whose poster `<img>` is opaque — so the wave passes behind the artwork rather than over it, with no extra masking. (A missing poster renders as a bordered placeholder with no background; the wave shows through it, which is correct — there is no artwork there to protect.)
- **Into the score.** For the same reason, the score chip is painted over the wave rather than instead of it, and `--mine-bg`/`--mal-bg` are `rgba(…, 0.15)` tints — translucent — so the wave reads through the chip at the same instant and the same intensity as through the card surface beside it. No second animation, no synchronisation to get wrong.

Two layers at different periods and different heights (full height, and a 62% band anchored to the bottom) beat against one another over the 480px cycle, so the motion reads as an irregular drift rather than as a metronome. 20s over 480px is ≈24px/s — an order slower than the old 5s sweep, and slower than any pointer response on the page.

`translate3d` only, so the effect is compositor-only and reflows nothing — the guarantee the existing "The podium is animated" requirement already makes. The highlight colour is `color-mix` on `var(--medal)`, which the codebase already uses (`.recap-page__tab--active`), so the wave is amber-on-cream in the light theme and yellow-on-charcoal in the dark one with no new token and no theme block. 22%/14% keeps the title's contrast well clear of the tint at its peak.

*Alternatives considered.* A diagonal `repeating-linear-gradient` would look more like a wave, but its period is a percentage of the gradient line, whose length depends on the box's own size and angle — no fixed translate distance is then a whole number of periods at every card width, so the loop would visibly jump. Animating `background-position` instead of `transform` gives seamlessness for free but repaints every frame. Masking the poster out with a `mask-image` or a clip path would let the layer sit above the link, but it would have to track the poster frame's exact box; painting underneath gets the same result from the stacking order the card already has.

### 6. A zero-count tile stops claiming to be followable

In `renderStats`, the `<div>` branch drops its conditional class: it renders `className="recap-page__stat"` whether or not `tile.to` is set. `--link` then only ever lands on an `<a>`, so its rules in `RecapPage.css` fold from `.recap-page__stat--link:hover` to `a.recap-page__stat--link:hover` and the rail rule to `a.recap-page__stat--link::before`. The tile keeps its box, its figure, and its place in the grid — only the rail colour and the hover treatment change — so the "no reflow" guarantee is untouched, and the `(tile.count ?? 0) > 0` test that decides `<Link>` vs `<div>` is unchanged.

This reverses a decision made in `drill-into-recap-stats` (its design decision 7, which argued a zero-count tile should still read as "this kind of tile"). The new reading: the rail's job is to say *this leads somewhere*, and a tile that leads nowhere wearing it makes the marker unreliable. The count is right there on the tile, so the zero is not hidden by the change.

### 7. Board tiles: bigger, and cut to the poster's own proportion

```css
.score-board__grid { grid-template-columns: repeat(auto-fill, minmax(150px, 1fr)); }
.score-board__tile { padding-top: calc(600 / 425 * 100%); }   /* was 150% */
```

At the board's 1032px content width `auto-fill` resolves to 6 tracks of ≈164px (was 9 of ≈105px), so on a 2× display a poster is drawn at ≈328×463 device px from a 425×600 source — a 0.77 reduction, against the ≈0.53 it was drawn at before, which is the softness. `auto-fill` (not `auto-fit`) keeps a one-item slot's tile the same size as a full slot's.

The ratio change is the same fix from the other side: at 1.412 the tile matches the source exactly, so `object-fit: cover` scales without cropping — the ~6% slice the 1.5 box took off each poster's sides is gone, and the box is the same 260:368 the podium's poster frame already uses. Both keep the `padding-top` percentage technique and its comment: the WebKit `aspect-ratio`-in-an-`fr`-track mis-sizing that comment documents is exactly what produced the "cropped in Safari" report, and percentage padding is immune to it.

`image-rendering: high-quality` stays as it is — it is a hint the engines currently treat as their default smooth downscale, so it is neither the cause nor the cure here, and removing it would be an unrelated change.

### 8. The type scale is a table of small explicit bumps, not a token

Every size on the recap is already an explicit `px` in one of four stylesheets; the change is one step up on each, applied where it is declared rather than introduced as a new custom property. A `--recap-fs-*` token set would add indirection for values used once each.

| Element | File | Now | After |
| --- | --- | --- | --- |
| Top-10 row (whole row) | `RecapPage.css` | inherits root | `font-size: calc(1em + 1px)` |
| Top-10 row poster / rank cell | `RecapPage.css` | 36×50 / 28px | 40×56 / 32px |
| Podium card title | `RecapPage.css` | 14px | 16px |
| Podium badge | `RecapPage.css` | 13px in 28px | 14px in 30px |
| Podium score value | `RecapPage.css` | 11px | 15px (decision 4) |
| Stat figure / label | `RecapPage.css` | 26px / 12px | 28px / 13px |
| Hot-take scores / direction | `RecapPage.css` | 14px / 12px | 15px / 13px |
| "See all" links | `RecapPage.css`, `RankingSection.css` | 14px | 15px |
| Section heading | `RankingSection.css` | 17px | 18px |
| Ranking row / meta / poster | `RankingSection.css` | inherits / 13px / 28×40 | `calc(1em + 1px)` / 14px / 30×44 |
| Distribution compact row | `ScoreDistribution.css` | 13px | 14px |
| Distribution compact bar | `ScoreDistribution.css` | 8px | 9px |

`calc(1em + 1px)` on the two row containers rather than a hard `px`: those rows inherit the fluid root size today, and a fixed px would freeze them while every heading around them kept scaling. Nothing inside those rows re-applies the rule, so the +1px cannot compound.

The lead grid's aside widens with the figures it holds — `minmax(14rem, 20rem) → minmax(15rem, 22rem)` — so the two-column stat grid keeps its columns and the uppercase labels keep wrapping where they already do rather than starting to clip. `ScoreDistribution`'s `--compact` variant is recap-only (the profile page passes no `compact`), so nothing outside the recap moves.

## Risks / Trade-offs

- **[Ranks 4–5 too small at the 5-across floor]** At ~900px they are ~115px wide, with a two-line title at 16px in a ~91px content box → roughly 6 characters a line before the ellipsis. → The 900px breakpoint is chosen at that floor; verify the two narrow cards at 900–1024px specifically, and raise the breakpoint if the title reads as unusable.
- **[The wave's contrast over the title]** `color-mix(…, 22%)` of a dark amber over a near-white card in the light theme sits behind `--text-h` body text. → Check the gold card's title and score against the highlight at its peak in both themes; the mix percentage is the single knob, and lowering it costs only visibility of the effect.
- **[A 1200px-wide animated layer per gold card]** `inset: -20% -520px` makes the layer far wider than the card. → It is one clipped element carrying two gradients and a `translate3d`; it is composited once and never repaints. Only one card on the page has it.
- **[The zero-count tile change is a reversal]** A tile that used to highlight now does not, which a user who learned the old behaviour may read as the tile having "gone dead". → The tile still shows its figure and its label unchanged, and the new behaviour is the one the rail's meaning implies; the archived decision it reverses is named in decision 6 so the history is not lost.
- **[Fewer, larger board tiles means more scrolling]** A slot with 60 anime goes from ~7 rows to ~10. → The board scrolls within the modal, and posters below the fold are `loading="lazy"`. (Slot headers were briefly made sticky during this change to keep a long slot's tier identifiable, then reverted at explicit user request in favour of a plain, non-pinning list — see the "score board scroll and motion follow-up" section of tasks.md and the corresponding spec delta below.)
- **[Spec/implementation drift being reconciled, not just extended]** The archived requirement said the first card "sits between the other two"; the shipped CSS has laid the cards out left-to-right in descending size since a later pass. The delta spec now states the shipped shape. The archived "the slot being scrolled through stays identifiable" score-board requirement is a second instance of the same kind of correction, reconciled by the same delta. → Called out here so the change is read as a correction rather than as a silent divergence.

## Open Questions

- "Same size to fit number 10 in" is read as *one shared chip size across the five cards, sized for the widest figure either basis prints* (decision 4). If it meant matching the row list's score cell instead, only the 68px/15px pair changes.
