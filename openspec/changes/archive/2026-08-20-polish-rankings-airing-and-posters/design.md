## Context

Three unrelated surfaces, one change, because each is a small presentation fix with no data behind it:

- **`AiringPage.tsx`** renders seven `.airing-day` columns from `week.days`, each headed by `dayOfWeek` + `<day>.<month>`. Nothing in the page compares a column to today, even though `todayIso()` already exists in the file (the `current` button uses it).
- **`TopAnimePage.css`** predates the recap podium. It declares its own `--medal-gold/silver/bronze` triples on `.top-anime-page`, which *shadow* the identically-named tokens `index.css` publishes on `:root` — so the same three medals have two different values in one app, and only the recap page's follow the theme's tuned dark-mode variants. Its showcase rank badge is a filled `#N` pill; the podium's is a medal-outlined circle. Both of its card tiers lock posters to `aspect-ratio: 2 / 3`, while `AnimeDetailPage.css` renders a poster at `260 × 368` (≈ `2 / 2.83`) and `RecapPage.css` deliberately matches that, so Top anime crops art the anime pages show in full.
- **`ScoreValue`** renders its reveal control inside `.score-value { min-width: 4ch; justify-content: center }`. Centring is correct in a compact chip whose value is centred, but wrong wherever the value is start- or end-aligned: the control then floats ~2ch away from where the number sits.
- **Poster boxes app-wide assume portrait art.** `AnimeDetailPage` (`260×368`), `SeriesPage` header (`aspect-ratio: 140/198`), `SeriesTimeline` cards (`2/3`) and `SeriesExtraTile` (`2/3`) all use `object-fit: cover`, which centre-crops a landscape picture down to a portrait sliver. `pictureUrl` is already MAL's `large` asset (`MalMappingExtensions.cs`) and carries no dimensions, so orientation can only be learned in the browser.

Constraints: frontend-only; no new DTO fields, requests, or dependencies; existing spec guarantees about slot widths, card alignment, and "card size says nothing about duration" must survive.

## Goals / Non-Goals

**Goals:**

- One medal palette in the app, and a Top anime showcase that reads as the same design language as the recap podium.
- Posters on Top anime's card tiers show the same extent of artwork the anime's own page shows.
- A reveal control that lands where the number it replaces would land, in every alignment context, without changing any slot's width.
- Landscape artwork shown whole on the anime detail page and the three series-page surfaces, with row alignment preserved.
- Today findable on the schedule at a glance, and announced as the current date.

**Non-Goals:**

- Porting the podium's *motion* (entrance, hover lift, gold sheen) or its sizing/proportional layout to Top anime — colour and badge form only.
- Landscape handling anywhere else (My List rows, season/search grids, Top anime tiers, recap podium, airing thumbnails). Those are dense thumbnail contexts where a crop is acceptable and a variable-size cell would break the grid.
- Any backend work to store image dimensions.
- Changing what a hidden score renders — only where the control sits.

## Decisions

### D1 — Today is marked on the day header, not down the column

`AiringPage` compares each `day.localDate` to `todayIso()` (the same helper the `current` button uses, so the mark and that control can never disagree) and puts `airing-day__header--today` plus `aria-current="date"` on the matching header.

The treatment is a filled accent chip: `--accent-bg` background, `--accent` text, the existing 1px bottom border recoloured to `--accent`, and weight 600 → 700. Fill-plus-weight means the mark is not carried by hue alone, and recolouring the *existing* border rather than thickening it keeps the header's height identical, so the seven columns' slots still start on one line (a 2px border would push one column's slots down by 1px).

*Alternative rejected:* tinting the whole day column. The user asked for the top of the page, and a column-wide tint would sit underneath the slot cards' own `--accent-bg` hover state, making a hovered slot in today's column indistinguishable from an unhovered one.

### D2 — Delete Top anime's local medal palette rather than reconcile it

`TopAnimePage.css`'s `--medal-*` block is removed outright so the page inherits `:root`'s. The block's comment (arguing for literal medal colours over theme tokens) is superseded: `index.css` publishes literal medal colours *with* per-theme values, which is what that comment actually wanted. This is the whole of the "same colours as the recap top 3" ask — no colour values are copied anywhere, because after the deletion there is only one set.

### D3 — The showcase card mirrors the podium's colour structure, aliased the same way

`.top-anime-showcase__item--rank-N` sets the three local aliases the podium uses (`--medal`, `--medal-bg`, `--medal-border`), and the card's shared rule reads only those — so the surface (`linear-gradient(var(--medal-bg), var(--medal-bg)), var(--code-bg)`), the border, and the top medal band (`::before`, 44px, `linear-gradient(180deg, var(--medal-bg), transparent)`) are each written once instead of three times. The card gains `position: relative; overflow: hidden` so the band cannot escape the card's rounded corners.

Rank 1 keeps its existing ring-plus-elevation (`box-shadow: 0 0 0 1px var(--medal-border), var(--shadow)`) rather than the podium's larger-card treatment: the spec requires rank 1 to read as most prominent, and the three-up grid here is equal-width by design.

### D4 — The `--lg` rank badge becomes the podium's circle; `--sm` is left alone

`.top-anime-rank--lg` changes from a medal-filled pill carrying `#N` to the podium's 28px circle — 2px `--medal` border, `--bg` fill, `--medal` number, no `#`. `.top-anime-rank--sm` (ranks 4–10) is deliberately unchanged: it sits *over* poster art, where the spec requires legibility independent of the artwork beneath it, and an outlined badge with a translucent-adjacent fill is exactly the failure mode that rule was written against. So the two tiers keep different badge forms, as they do today.

### D5 — Chips shrink by capping the row, not by unstretching it

The two chips must stay equal-width to each other (existing spec scenario), which is what `flex: 1 1 0` on `align-self: stretch` gives. Rather than dropping the stretch — which would size each chip to its own text and break that guarantee — the row gets a `max-width` so the pair stops spanning the full info column, and the chips' padding tightens. Label rises 10px → 12px and value 13px → 14px, keeping the value dominant while making the label read as a label. Chip labels stay "My score" and "MAL", unchanged.

### D6 — Poster proportions: `aspect-ratio` where the width is definite, a padding frame where it is a grid track

Both card tiers move from `2 / 3` to the anime pages' `260 / 368`.

- `.top-anime-showcase__picture` has a definite `width: 140px`, so plain `aspect-ratio: 260 / 368` is safe.
- `.top-anime-card__picture` is `width: 100%` of a `repeat(7, 1fr)` grid track — the exact case `RecapPage.css` documents WebKit mis-sizing when `aspect-ratio` is applied to a replaced element inside an `fr` track. So the card's image is wrapped in a `.top-anime-card__picture-frame` sized by `padding-top: calc(368 / 260 * 100%)` with the image absolutely filling it, the same technique and for the same reason as `.recap-podium__picture-frame`. This is the change's only markup edit on this page; the `--sm` rank badge stays a sibling of the frame inside the (already `position: relative`) card link, so its absolute placement is unaffected.

`object-fit: cover` is kept at the new ratio: portrait MAL art (≈ 0.705) now loses essentially nothing, and cover still guards against an unexpected ratio rather than letterboxing it.

### D7 — Reveal-control alignment is a page-scoped CSS override, not a component prop

`.score-value` keeps `justify-content: center` as its default. The three Top anime layouts that align their values otherwise override it in `TopAnimePage.css`:

- `.top-anime-showcase__scores .score-value { justify-content: flex-start }` — the chip's label and value both start at its leading edge.
- `.top-anime-card__scores .score-value { justify-content: flex-end }` — the card's MAL score is pinned to the trailing edge opposite my score.
- `.top-anime-row__mal-score .score-value { justify-content: flex-end }` — the flat rows' right-aligned MAL column, included because it has the identical mismatch and the spec rule this change writes would otherwise be violated by code in the same file.

*Alternative rejected:* an `align` prop on `ScoreValue`. Alignment is a fact about the surrounding layout, not about the score, and this page already reaches into `.score-chip` from its own stylesheet — a prop would thread presentation through the component API for no gain. The slot's `min-width: 4ch` is untouched in every case, so no width changes and nothing reflows.

### D8 — Orientation is learned from the loaded image, via a callback-ref hook

A new `frontend/src/hooks/useLandscapePicture.ts` exports a hook returning `[refCallback, isLandscape]`. The callback ref reads `naturalWidth > naturalHeight` immediately when the element is already `complete` (a cached image, which is the common case on a revisit) and otherwise attaches a one-shot `load` listener. It resets when `src` changes.

*Why not `onLoad`:* a cached image can finish decoding before React attaches the handler, so `onLoad` may never fire and those anime would silently keep the cropped box — the bug would appear only on second visits, which is the hardest kind to notice.

*Why a hook and not a component:* the four call sites differ (bare `<img>` on the detail page and the series header, framed image inside a card on the timeline and More tiles) and two of them need the flag on an *ancestor* — the card — to widen it. A hook hands the flag to whatever needs it; a wrapper component would have to grow props for each.

Square art counts as portrait (`>`, not `>=`); it is vanishingly rare and the existing box crops it symmetrically.

### D9 — Landscape rendering: natural height for hero images, fixed height plus a wider card for card tiers

Two different treatments because the two contexts have different failure modes.

**Hero images** (`anime-detail-page__picture`, `series-page__picture`) keep their width and take `height: auto` (`aspect-ratio: auto` on the series header). Nothing sits beside them horizontally that depends on their height, so the page simply gets a shorter picture with no dead space.

**Card tiers** (`series-timeline__card-picture`, `series-extra-tile__picture`) keep their picture area's *height* and switch to `object-fit: contain`, because a row of cards must keep its pictures', titles', and footers' edges on one line — a natural-height picture would drop one card's body above or below its neighbours'. The card then widens so the contained picture is shown at a useful size rather than as a thin band:

- **Timeline** — the card is a fixed `width: 168px` in a flex row, so this is direct: `--card-w` is introduced, the picture's height becomes `calc(var(--card-w) * 3 / 2)` (identical to today's `aspect-ratio: 2/3` result), and a landscape card sets `--card-w: 336px` while that height stays pinned to the portrait value.
- **More tiles** — the grid is `repeat(auto-fill, minmax(140px, 1fr))` with a `12px` gap, so no track width is known to CSS. A landscape tile takes `grid-column: span 2`, and its picture frame is sized to exactly *one* track — `width: calc((100% - 12px) / 2)`, `padding-top: calc((100% - 12px) * 0.75)` (1.5× that width, both percentages resolving against the tile's own width) — so its height equals a portrait tile's picture height exactly, at any viewport. The image inside is `height: 100%; width: auto; max-width: 200%`, centred, so it grows sideways into the tile's second track instead of shrinking.

A landscape card takes one alternative width, never a per-image computed one, so card width still carries no magnitude meaning (`series-page` spec: "Card width still says nothing about duration").

*Alternative rejected:* leaving card widths alone and only switching to `contain`. It is one line and keeps perfect alignment, but a 16:9 image inside a `2 / 3` box is ~62% empty — the artwork ends up smaller than the crop it replaced, which is not what "show it whole" should feel like.

## Risks / Trade-offs

- **Deleting the local medal palette shifts Top anime's medal colours slightly** (e.g. gold `#b8860b` → `#a16207` in light mode) → intended: that is the "same colour as the recap" ask, and the theme-aware dark values are strictly better than the single literal set. Verified against the "Medal colours survive a theme switch" scenario in both themes.
- **A landscape picture's box changes shape after the image loads**, since orientation is unknown until then → mitigated by it being a cached, instant path on revisits and by affecting only the handful of anime with landscape art; the alternative (blocking layout on a preflight image load) would delay every poster on every page to serve a rare case.
- **The More-tile sizer hard-codes the grid's `12px` gap** in a `calc` → mitigated by lifting the gap into a custom property on the grid and reading it in both places, so the two cannot drift.
- **A landscape tile spanning two tracks can leave a ragged trailing gap** in a group whose count no longer divides evenly → accepted; `auto-fill` already leaves trailing space on the last row and the groups are ordinary galleries, not a fixed layout.
- **`--medal-bg` is aliased locally on the showcase card** and would collide with a same-named token if one were ever added to `:root` → same pattern the podium already uses, so the two pages fail or work together rather than diverging.
- **Marking today by accent risks colliding with the accent-tinted slot hover** → avoided by D1's decision to mark only the header, which no slot hover touches.

## Open Questions

None. The one wording ambiguity — whether the MAL chip's label should be relabelled "MAL score" to match "My score" — is resolved as *no*: the ask was about label size, the chips must stay equal width regardless, and renaming would be a spec-visible change to a label two other scenarios reference by name.
