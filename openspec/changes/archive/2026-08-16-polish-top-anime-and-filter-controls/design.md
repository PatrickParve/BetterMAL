## Context

Three unrelated pieces of visual polish land together because they all touch the same layer — page CSS plus two shared components — and would otherwise conflict with each other in the same files.

**Top Anime.** `TopAnimePage.tsx` renders three tiers today: `.top-anime-podium` (ranks 1–3), `.top-anime-cards` (ranks 4–10, a 7-up grid), and `.top-anime-page__list` (rank 11+, flat rows). The podium tier is the problem: each card sits on a solid coloured "stand" (84/60/44px blocks), `align-items: flex-end` keeps the stands flush, and CSS `order` paints rank 1 in the middle — so the visual order is 2‑1‑3 while the DOM is 1‑2‑3. The rank number lives on the stand, below the card. Ranks 4–10 carry a 12px translucent-black pill in the poster's top-left corner, whose legibility depends on the artwork behind it. Neither card tier has any hover state, though `navigation-and-search`'s hover requirement already demands one for every anime card and top-anime row (only `.top-anime-row` has it).

**Control heights.** Every filter control in the app is styled the same way — `font: inherit; font-size: 14px; padding: 6px 10px; border: 1px solid` — but renders at different heights depending on its element. The root sets `font: clamp(16px, 14px + 0.3125vw, 18px)/145%`, and a percentage line-height on `:root` computes to a *length* (~23px) that inherits as that length. A `<button>` or `<label>` therefore gets a ~23px line box (≈37px total), while a `<select>`'s inner box is normalized by the browser to `normal` (≈17px, ≈31px total). Same padding, ~6px apart. That is why Season's `Type` and `In my list` stand proud of its `Season`/`Year`/`Sort` selects, and Search's `Type` stands proud of its sort select.

**Search clear control.** Both search fields (`SearchBar`, and Settings' refresh picker) are `<input type="search">`, so the "×" is the UA's `::-webkit-search-cancel-button`, which defaults to the arrow cursor.

## Goals / Non-Goals

**Goals:**
- Ranks 1–3 and 4–10 read as deliberate, distinct tiers where the rank is unmistakable, without the literal podium furniture.
- Both card tiers gain the app's standard card hover/focus highlight, closing an existing spec gap.
- One shared height governs the controls in a filter/sort cluster, so a control added later inherits it instead of re-introducing the mismatch.
- The search field's clear control reads as clickable.

**Non-Goals:**
- No change to the flat rows for ranks 11+ — the ask is the top-3 and the rest of the top 10.
- No change to data, API, pagination, the Add/Edit action, or score hide/reveal semantics.
- No redesign of the Season/Search/My List headers beyond control height; the header layout (three-zone grid on Season, space-between on Search) stays.
- No new shared "Button"/"Control" React component — this is a CSS-level fix, not a component-library project.
- The Airing page's week stepper and its month/year selects are untouched: they are the same shape as Season's arrows-plus-selects, but nothing there is reported as mismatched and no shared control lands in that cluster.

## Decisions

### D1 — Ranks 1–3 become three full-width horizontal showcase cards, in rank order

`.top-anime-showcase` is a `repeat(3, minmax(0, 1fr))` grid spanning the content width, collapsing to one column under 1024px. Each card is horizontal: poster (fixed `104px`, `aspect-ratio: 2/3`) on the left, an info column on the right holding the rank badge, title, the two score chips, and the Add/Edit button. No `order` — DOM order is visual order, so card 1 is rank 1 and screen-reader order finally matches what the eye sees.

*Alternatives considered.* **Keep the podium, restyle the stands** — rejected: the stands are exactly the "unclean" part, and the `order` reshuffle is what makes the rank ambiguous in the first place. **Scale up the existing vertical poster cards to a 3-up full-width row** — rejected: at a content width of 1400–2400px each 2:3 poster becomes 400–750px wide, absurd for three cards; capping the row's width and centring it (what the podium does today at 150/180px) leaves the tier visually detached from the full-width tiers below it. The horizontal card spends the extra width on information instead of on poster area.

### D2 — Medal colours as three page-scoped token triplets

Declare on `.top-anime-page`, mirroring the `--status-*` triplet convention (`fg` / `-bg` / `-border`):

```
--medal-gold: #b8860b;   --medal-gold-bg:   rgba(184, 134, 11, 0.12);   --medal-gold-border:   rgba(184, 134, 11, 0.5);
--medal-silver: #71797e; --medal-silver-bg: rgba(113, 121, 126, 0.12);  --medal-silver-border: rgba(113, 121, 126, 0.5);
--medal-bronze: #8c5a2b; --medal-bronze-bg: rgba(140, 90, 43, 0.12);    --medal-bronze-border: rgba(140, 90, 43, 0.5);
```

The three base hexes are the ones the podium stands already use. They stay literal rather than theme-swapped, for the reason already recorded in `TopAnimePage.css`: a medal's colour *is* its meaning, and the theme's role colours don't survive being used as solid fills in both modes. The 12%/50% alpha wash reads on both `--bg` values. Scoped to the page, not `:root`, because nothing outside Top Anime has a medal concept.

### D3 — One rank-badge family, two sizes, two fills

A single `.top-anime-rank` pill (radius 999px, tabular figures, `#N` text) with two modifiers:

- `--lg` (showcase): `20px/800`, `padding: 2px 12px`, solid medal fill, `#fff` text.
- `--sm` (top-10 cards): `13px/700`, `padding: 2px 8px`, `background: var(--text-h); color: var(--bg)`, absolutely positioned at the poster's top-left, `pointer-events: none`.

The `--text-h`/`--bg` pair for the small badge auto-inverts with the theme and gives ~15:1 contrast against *itself*, so legibility never depends on the artwork underneath — which is precisely what today's translucent-black pill gets wrong over dark or busy posters. White-on-medal is only used at `20px/800`, where ~3.5:1 clears the WCAG large-text bar; it would not clear it at 13px, which is the second reason the small badge is neutral rather than medal-coloured.

*Alternatives considered.* Purple `--accent` fill for the small badge — rejected: purple is the "my score" role in this app's colour language (`score-presentation`), and a purple rank chip would read as a score. Moving the rank out of the poster into the card footer — rejected: it competes with the score line and makes the badge family inconsistent across the two tiers.

### D4 — Hover: card-surface change on the showcase tier, inset plate on the top-10 tier

The showcase card already *is* a bordered surface, so hover/`:focus-within` changes it directly: `background: var(--accent-bg)`, `border-color: var(--accent-border)`, `box-shadow: var(--shadow)` — the app's standard highlight, no layout shift (the border already occupies its space). The medal identity survives in the badge, which is the rank signal.

The top-10 cards are borderless posters, so they reuse `AnimeCard`'s technique verbatim: a `::before` plate at `inset: -6px`, `border-radius: 12px`, `z-index: -1`, transitioning background/border/shadow. The grid gap is 16px and two neighbouring plates extend 6px each, so plates never touch.

*Alternative considered.* Deepening each card's own medal tint on hover instead of switching to the accent tint — rejected: it needs a fourth token per medal, and it makes Top Anime the one page where hovering a card doesn't look like hovering a card anywhere else.

Rank 1's extra prominence is `box-shadow: 0 0 0 1px var(--medal-gold-border), var(--shadow)` — an outer ring plus elevation, chosen over a thicker border or a larger poster because it changes nothing about the box's size, so the three cards stay a clean 3-up row.

### D5 — Showcase scores use `ScoreChip`; the other tiers keep coloured values

The showcase card is a low-density surface where each score stands on its own as a figure, which is exactly the chip half of `score-presentation`'s density rule. Two default-size chips, labelled `MAL` and `My score`. The MAL chip wraps `<ScoreValue>` as `ScoreChip`'s contract requires, so hide/reveal and the completed-score setting keep working untouched; `ScoreChip` and `ScoreValue` are reused as-is, not modified.

`ScoreChip` has `min-width: 130px`, so a pair needs ~268px. At the narrowest 3-up width (≈1024px viewport) the info column is ~278px — it fits, but with little slack, so the chip row is `display: flex; gap: 8px; flex-wrap: wrap`: if a future font-size or padding change squeezes it, the chips stack instead of overflowing. Ranks 4–10 and the flat rows stay on `.score--mine` / `ScoreValue`: they are a dense grid and a dense list, where chips would crowd the card and break the rows' column alignment.

### D6 — One `--control-h` token, applied by setting height and zeroing block padding

`--control-h: 34px` on `:root` in `index.css`. Every control in a filter/sort cluster gets `height: var(--control-h); box-sizing: border-box;` and keeps only its inline padding; text-carrying non-`<select>` controls become `inline-flex` with `align-items: center` so their content is centred rather than pinned by padding.

Why 34px: it leaves a 32px content box, which holds the largest line box the fluid root ever produces (18px × 145% ≈ 26.1px) as well as a `<select>`'s ~17px normalized box — so both element families fit at every font size the app renders, which is what the requirement demands. It sits between today's ~31px selects and ~37px buttons, so nothing moves dramatically.

Applied to: `.season-page__sort` (season, year, and sort selects share this class), `.season-page__checkbox`, `.search-page__sort`, `.filter-multi-select__button`, and — because that button is the shared `Type`/`Airing` control — `.my-list-page__sort`, `.my-list-page__query`, `.my-list-page__clear-filters`, and `.my-list-page__tab`. Not applied to the round `.season-page__nav button` steppers, which are their own 32px circular family.

*Alternatives considered.* `align-items: stretch` on each cluster — rejected: it stretches a `<select>` unpredictably across browsers, does nothing once a cluster wraps to two lines, and leaves each new control free to be a different height. Forcing `line-height: 1` on every control — rejected: fragile against the fluid root size and prone to clipping descenders. A shared `.control` class applied in JSX — rejected as a larger refactor than the problem warrants; the per-page classes already exist and the token is the single source of truth this requirement asks for.

### D7 — The cursor rule is global, not `SearchBar`-scoped

`input[type='search']::-webkit-search-cancel-button { cursor: pointer; }` goes in `index.css`, so the navbar search bar and Settings' refresh picker are covered by one rule and any future search field inherits it. No `-webkit-appearance` override, so the native glyph, its position, and its behaviour are unchanged. Firefox renders no clear control at all, so the rule is inert there — which is why the requirement is written to constrain the cursor over the control rather than to mandate a control.

## Risks / Trade-offs

- **`<select>` ignoring `height` under Safari's `menulist` appearance** → verify Season/Search/My List in Safari as well as Chrome; the fallback, if it misbehaves, is `appearance: none` plus a custom arrow on those selects — a pattern the codebase already uses in `.my-list-row__score-select`. Not done pre-emptively, because dropping `menulist` also drops the native dropdown affordance.
- **`.filter-multi-select__button` is shared with My List** → My List's filter bar is normalized in the same change, so the fix cannot leave a new mismatch behind. This widens the diff to a page the user didn't report; it is bounded to height-only rules.
- **`--control-h` looks global but is applied selectively** → controls outside a filter cluster (nav arrows, editor overlay fields, pagination) keep their own sizing; the token's meaning is "the height of a control in a filter/sort cluster", recorded in a comment where it is declared so it isn't later assumed to be app-wide.
- **White-on-gold at 3.5:1** → confined to the `20px/800` showcase badge, which clears the large-text bar; the small badge uses the ~15:1 neutral pair instead.
- **The 3-up showcase row on a very wide display** → each card can reach ~780px, where the info column looks airy. Accepted: it is the same trade-off every full-width tier on the page already makes, and capping it would detach the tier from the grid below.
- **A 7-up grid of small cards directly under three large cards** → the size jump between tiers is the point (three tiers, three densities), but if rank 4 ends up looking orphaned next to rank 3, the top-10 grid can move to a 5-up/6-up layout later without touching the showcase tier.
- **Regression surface is visual, not behavioural** → nothing here changes state, fetching, or URL handling, so the checks are: pagination still swaps tiers correctly on page 2+, Add still flips to Edit in place, hidden MAL scores still reveal one at a time inside a chip, and the `Type` popover still anchors under its button.

## Migration Plan

Not applicable — frontend-only presentation change with no persisted state, no API surface, and no data migration. Rollback is reverting the commit.

## Open Questions

- Rank 1's emphasis is a ring plus elevation (D4). If that reads too subtle next to ranks 2 and 3 in the running app, the next lever is a slightly wider first grid track (`1.15fr 1fr 1fr`) rather than a bigger poster, which would unbalance the row's height.
- `In my list` stays a checkbox at the shared height. Turning it into a pill toggle matching `.my-list-page__tab` would tighten the cluster further, but that is a control-semantics change beyond this change's ask.
