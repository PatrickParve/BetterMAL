## Context

Three defects on the profile page, two of them the same bug on different axes.

**The strips.** `.top-anime-strip__item`, `.top-series-strip__item`, and `.rewatched-strip__item` all declare `flex: 0 0 calc((100% - 9 * 10px) / 10)` and a `1px` border. There is no global `box-sizing: border-box` in this codebase (`index.css` sets it per element on `#root` and `.page-content` only), so those rules size the tile's *content box* and the border is added on top: ten tiles overflow the strip by 20px. `ProfilePage.tsx` marks a strip with ten or fewer entries `--fits`, which sets `overflow-x: hidden` — so the overflow isn't scrollable, it's just gone. `CurrentlyWatchingCarousel.css` already hit this exact bug and fixed it with `box-sizing: border-box` plus a comment explaining why.

**The feed.** `.activity-feed` is `flex: 1 1 auto` capped at `max-height: 340px`; rows are `min-height: 56px` with a `1px` border (58px outer) and an 8px gap. Five rows occupy 5 × 58 + 4 × 8 = 322px, leaving 18px of the sixth showing. The cap is also a fixed number against a variable box: the top row's height moves with `h2`'s `clamp(20px, 16px + 0.625vw, 24px)`, so any single pixel value is right at one window width and wrong at the next. A prior change already requires the feed to *fill* its box with no dead space below it, so the fix cannot simply be a smaller cap.

**The divergence lists.** `BuildOpinionDivergence` uses raw score differences: MAL − mine ≥ 3 one way, and MAL < 7 with mine − MAL ≥ 2 the other. Measured against the live database (458 anime with both scores): my scores average 7.22 with a population SD of 1.44, MAL's average 7.83 with an SD of 0.75 — half the spread, and only 12 of the 458 have a MAL average below 6.5 at all. The second rule therefore matches exactly one anime. Any rule built on raw differences inherits this: the mirrored-threshold variants measured 44 vs 9 and 30 vs 15.

## Goals / Non-Goals

**Goals:**
- Ten poster tiles fit across every strip, whole.
- The Latest updates feed shows five whole rows at any window width, still filling its box.
- Both divergence lists populate with comparable strength, ordered by how strong the disagreement is, with headings that stay honest.
- Thresholds live in named constants with the reasoning attached, so the next person doesn't have to re-derive them (the trigger for this change was not remembering the old ones).

**Non-Goals:**
- No scroll-snapping on the feed or the divergence lists — the requirement is about the resting state, and snap changes the feel of every scroll to fix something that isn't broken.
- No new API fields: the divergence score itself is not surfaced in the DTO or the row. The rows keep showing `Me X · MAL Y`.
- No user-configurable thresholds, no Settings surface.
- The full edit-history overlay keeps its current row size; only the feed's rows change.
- No change to strip scroll mechanics, drag-to-scroll, or scroll restoration.

## Decisions

### 1. Strips: `box-sizing: border-box` on the tile, not a smaller basis

Add `box-sizing: border-box` to the three `__item` rules so the declared basis *is* the rendered width. Ten tiles plus nine 10px gaps then equal the strip's content width exactly, and the strip's own 8px padding stays free — which is what the hover `transform: scale(1.08)` needs at either end (a ~111px tile grows ~4.4px per side).

*Alternative — subtract the borders in the calc* (`calc((100% - 9 * 10px - 20px) / 10)`): works, but encodes the border width in an arithmetic expression three times over, and silently breaks the moment a tile's border changes. *Alternative — collapse the three near-identical rules into one shared `.poster-strip` class*: the right long-term shape, but it touches three sections' markup and their scroll-restore keys for a one-line fix; left out deliberately.

The `STRIP_VISIBLE_TILES = 10` constant in `ProfilePage.tsx` and its comment stay correct — the comment's claim that the basis "lays out exactly this many tiles across the strip's visible width" only becomes true with this change.

### 2. Feed: rows sized from the box, via container query units

`.activity-feed` becomes a size container (`container-type: size`) and publishes a row height derived from its own height:

```
--activity-row-h: calc((100cqh - 4 * 8px - 5 * 2px) / 5)   /* gaps, then the rows' borders */
```

Rows in the feed take `height: var(--activity-row-h)` and the poster takes that height with its width scaled by the existing 41/56 poster ratio. Five rows then fill the feed exactly, whatever height the box hands it, and the sixth starts below the fold at every window width. At today's ~340px that puts rows at ~59.6px and posters at ~44 × 60 — the size bump the sliver was hiding.

Rows keep `content-box` sizing and the height excludes their 1px borders, hence the `5 * 2px` term. This is deliberate: switching the rows to `border-box` would shrink the content box to 2px less than the poster and clip the poster against the row's `overflow: hidden`, breaking the "poster fills the row flush" requirement.

Size containment means the feed contributes nothing to its box's intrinsic height, so it needs a floor or a short top row could collapse it: `min-height: calc(5 * 60px + 42px)` (342px, essentially today's cap) keeps the box at least as tall as it is now and keeps rows at ≥60px.

*Alternative — a fixed height of exactly five rows:* one line, but it contradicts the existing "the list fills the box to its bottom edge" requirement whenever another box in the row is taller, and it re-breaks at other window widths as the fluid `h2` moves the row height. *Alternative — a `ResizeObserver` that quantizes the height in JS:* works everywhere, but adds a measurement loop to a page that already runs two of them (scroll restoration) for something CSS can express. *Alternative — CSS `round(down, …)`:* no way to name "the space left in the box after the header" as a length inside the feed's own rule, which is exactly what `cqh` provides.

Browser support (Chrome 105+, Safari 16+, Firefox 110+) is well below anything this app targets, and the degradation is benign: an unsupported `cqh` makes the custom property invalid, `height` falls back to `auto`, and the rows land back on today's `min-height: 56px`.

### 3. Divergence: standardized scores, then a label gate

`BuildOpinionDivergence` computes, over the entries that have both a personal score and a MAL average (call it the rated population): the mean and population standard deviation of my scores (μ_mine, σ_mine) and of the MAL averages (μ_mal, σ_mal). For each anime:

```
divergence = (mal - μ_mal) / σ_mal  -  (mine - μ_mine) / σ_mine
```

- **They liked it, I didn't**: `divergence >= 1.0` and `mine <= 5` and `mal >= 7.5`, ordered by `divergence` descending.
- **I liked it, they didn't**: `-divergence >= 1.0` and `mine >= 8` and `mal <= 7.5`, ordered by `-divergence` descending.

Ties on either side break on title, case-insensitively, so the order is stable between builds.

Standardizing is what makes the two lists comparable: it measures each score against the spread of its *own* scale, so MAL's compressed averages and my wider personal range are put on equal footing. The label gates are then applied in raw score terms, because that's what the headings claim in raw score terms — a title I scored 6 does not belong under "I didn't", however far MAL's average sits above it, and a MAL average of 7.8 is not a community dislike. The gate boundaries are MAL's own score labels: 5 is "Average" (and below it, everything worse), 8 is "Very Good" (and above it, everything better), and 7.5 splits the community scale between "Good" and "Very Good". That leaves 6 ("Fine") and 7 ("Good") as a neutral band in neither list — a deliberate gap, not an oversight: those are the scores where I neither liked nor disliked something, so no heading can honestly claim me either way.

Measured on the current database:

| rule | they liked it | I liked it |
|---|---|---|
| current (raw ≥3 / MAL<7 and raw ≥2) | 16 | 1 |
| **standardized ≥1.0 SD + label gates (mine ≤5 / mine ≥8)** | **22** | **24** |
| same, with the dislike gate at 6 | 44 | 24 |
| mirrored raw thresholds (≥2 / ≥1.5, dislike gate at 6) | 44 | 9 |
| mean-offset only (shift MAL by −0.61, ≥2) | 31 | 13 |
| standardized ≥1.0 SD, no gates at all | 59 | 52 |

Every one of the 22 titles the gate at 5 drops relative to a gate at 6 is one I scored exactly 6 — and dropping them is what brings the two sides level (22 vs 24) rather than leaving one list twice the other.

The top of each list under the chosen rule: *The First Slam Dunk* (me 3, MAL 8.70), *Fumetsu no Anata e* (me 3, MAL 8.35), *Gintama* (me 5, MAL 8.93); and *Kanojo, Okarishimasu 5th Season* (me 9, MAL 6.31), *Kanojo, Okarishimasu 4th Season* (me 8, MAL 6.16), *Bakugan: Gundalian Invaders* (me 8, MAL 6.39).

**Population SD, not sample SD** — a whole-list statistic over a population that is the whole list, and at n = 458 the distinction is noise anyway; naming it removes the ambiguity for whoever writes the tests.

**Guard**: with fewer than 10 rated pairs, or a zero SD on either scale (every score identical), both lists come back empty. Zero SD is a division by zero; a handful of pairs is a mean and spread that mean nothing. The page already renders "Nothing here yet." for an empty list.

The statistics come from the entry list `GetProfileAsync` has already loaded — no extra queries, no new repository method, no caching concern.

### 4. Both lists uncapped, each box ten rows tall and scrollable

The service returns every qualifying anime, ordered. `.divergence-list` gets `max-height: calc(10 * 58px + 9 * 8px)` — ten rows at their 58px outer height plus the gaps between them, 652px — and the `scroll-y` class the feed and history list already use, so the scrollbar sits beside the rows instead of over them. `max-height` rather than `height`: a list of three renders three rows, not three rows and seven rows of white space. The eleventh row starts past the cap and shows nothing at all, so this list gets the same whole-rows treatment as the feed without needing the container-query machinery — its rows are a fixed 58px, so the arithmetic is exact.

## Risks / Trade-offs

- **The divergence rules are self-calibrating, so membership shifts as the list grows** → That is the point (fixed constants are what produced a one-item list), but it means an anime can leave a list without being re-scored. Both lists are read-only summaries with no persistence hanging off them, so nothing breaks; worth knowing when a title "disappears".
- **Standardized divergence is harder to explain than "3 points apart"** → The label gates are expressed in raw scores and every row shows both scores, so the lists stay readable without understanding the ranking. The constants carry their derivation in comments.
- **`container-type: size` collapses an element that has no other height source** → The `min-height` floor is the mitigation, and it's set to today's effective height so the box can't get shorter than it is now.
- **Container query units are a newer primitive than the rest of this stylesheet uses** → Fallback is today's behaviour (56px rows, no whole-row guarantee), not a broken layout.
- **The lists get much longer (16→44, 1→24 rows)** → Payload is a few kilobytes of small DTOs, and the boxes are height-capped, so the page doesn't grow.
- **`box-sizing` changes the tiles' rendered width by 2px** → Tiles get 2px narrower, not wider; nothing in the strips depends on an exact pixel width, and the aspect-ratio driven height follows.

## Migration Plan

No data migration, no API contract change, no stored state. The change is one backend method, one stylesheet, and one `className`. Rollback is reverting the commit; nothing persists that would outlive it.

## Open Questions

None blocking. Two were resolved before this design: the rule basis (standardized divergence with label gates, chosen over raw mirrored thresholds and mean-offset normalization) and the list length (ten rows visible, scrollable to the rest, chosen over a hard top-10 cap and over rendering everything).
