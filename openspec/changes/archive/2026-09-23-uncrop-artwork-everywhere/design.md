## Context

`uncrop-row-artwork-and-highlight-updates` taught every fixed-height row and thumbnail to draw a picture whole (`RowPicture`, `useWidePicture`, the `artwork-presentation` capability). It excluded the poster grids and strips on purpose, because their design is a uniform tile, and its spec says so, so that a later change "has to argue its way in rather than drift in". This is that change. The argument comes from the user: a picture chosen for an anime should look like itself everywhere, and the grids may be restyled to allow that as long as each surface keeps its structure, colours and information.

Every surface that still crops, by the family its layout puts it in:

| Family | Surfaces | Today |
|---|---|---|
| Column grid (fixed tracks, sorted) | Season, Year, Search, Series browser, Home "Followed shows airing", series More tiles | `AnimeCard`: `aspect-ratio: 2/3; object-fit: cover`. More tiles already span two tracks for `naturalWidth > naturalHeight` |
| Fixed box (box shape *is* the design) | Home "Currently watching" carousel, Top anime rank 4–10 cards, Recap podium, Recap score board, series timeline cards | `cover` in a 2/3, 260/368 or 425/600 box. The timeline widens to one fixed alternative width for `>` |
| Height-bound tile (width is free) | Profile "My top anime", "Top series", "Most rewatched" (both scopes), "Most time spent"; Top anime rank 1–3 showcase poster | `cover` at a fixed tile size; strips "fit" by `items.length <= 10` |
| Own-proportion header | Series page header | `cover` in 140/198 unless `>` |

The detail page (`height: auto; aspect-ratio: auto 260/368`), update cards and picture picker already draw every shape whole and are not touched.

Two product decisions were put to the user and answered:

1. **Grids:** a wide card spans two columns and the cards keep their exact sorted order, even though a row then sometimes ends one cell short. The user chose this over "slide the next card in" because packing would put a sorted list in the wrong order.
2. **Leftover space:** where a picture drawn whole doesn't fill its box, the gap is filled with a blurred copy of the same art, not a neutral background.

## Goals / Non-Goals

**Goals:**

- One shape classification, computed in one place and used by rows, cards, tiles and boxes alike.
- Poster art renders **identically to today** on every surface: same box, same crop, no extra element. That keeps a change touching a dozen surfaces checkable by eye, as it did for the rows.
- Each surface keeps its structure. Grids keep their columns and order, fixed boxes keep their box, strips keep their height and drag-scroll, the carousel keeps its exact five-card bound.
- No backend, DTO or storage change. Shape is read from the loaded image.

**Non-Goals:**

- Changing which picture is displayed (`artwork-selection`).
- The detail page, update cards, and picture picker, which are already whole.
- Card colours, borders, badges, chips, text or information. Only the picture area's geometry and fill change.
- Packing grids densely (the user declined it).
- Changing any host's `loading` policy. Each keeps whatever it does today.
- `SeriesEntryRow.css`, whose picture rule is dead (the component isn't rendered, only its label helpers are imported).

## Decisions

### D1 — Three shapes, split at 3:4 and 1:1

`useLandscapePicture.ts` gains `usePictureShape(src) → [ref, shape]` on the existing `useOrientationPicture` core (callback ref, `complete` check, reset on `src` change):

- `poster`: `w / h ≤ 3/4`
- `upright`: `3/4 < w / h < 1`
- `wide`: `w / h ≥ 1`

`3/4` sits just above every real poster and every poster box in the app. The boxes run from 2:3 (0.667, cards and strips) to about 0.73 (profile rows). MAL's own posters run from about 0.64 to 0.72. So at worst a poster-classified picture loses about 11% of its width, in the narrowest 2:3 box, and a typical 0.708 MAL poster loses the ~6% it loses today. Anything wider used to lose up to a third of itself, and now it is drawn whole.

`wide` is where layout changes (span, widened card), because that is where a picture's own width at the box's height first exceeds a whole poster box. `upright` is drawn whole but doesn't move anything except in the row and strip families, where width is already free.

`useWidePicture` (≥1, rows only) is removed. `RowPicture` switches to `usePictureShape` and applies `row-picture--wide` whenever the shape isn't `poster`. Its CSS already handles any ratio: `width: auto` at the slot's height, capped at 16/9 of it. `useLandscapePicture` (`>`) stays for the detail page's and the series header's landscape layouts, whose specs define landscape as strictly wider.

*Alternative rejected:* a threshold relative to each slot's own ratio. It is marginally more precise, but it needs every slot's ratio in JavaScript, and row slots declare theirs as CSS `calc()` expressions over other custom properties. That buys a difference of a few percent over one constant that already clears every box in the app.

*Alternative rejected:* keeping ≥1 as the only split. An upright 4:5 picture would still lose a sixth of its width in a 2:3 card and a fifth in a my-list row. That is exactly the "square gets cut off" complaint, one notch narrower.

### D2 — `PosterPicture`: one leaf component; hosts react with `:has()`

Every card, tile and box surface renders its picture through a new `PosterPicture`, the counterpart to `RowPicture`:

```tsx
<PosterPicture src={item.pictureUrl} className="anime-card__picture" />
```

It renders a wrapper `<span class="poster-picture poster-picture--{shape} {className}">` holding the art `<img>`. Only when the shape isn't `poster`, it also holds the fill copy (D3). With no `src` it renders the host's existing placeholder, `{className} {className}--placeholder` (or `placeholderClassName`), unchanged. It passes through `alt`, `draggable` and `loading` for the hosts that set them today (profile strips, score board).

The shape state lives in this leaf, for the same reason `RowPicture`'s does (row design D1). Several hosts render many pictures from one component: `ProfilePage`'s five strips, `TopAnimePage`, `RecapPage`, `ScoreBoardOverlay`. A hook per picture is impossible there. A picture's load should also re-render one wrapper, not a page.

Hosts whose *own* geometry depends on the shape (a grid card spanning, a timeline card widening, a strip tile widening) read it from the wrapper's class with `:has()`, e.g. `.season-page__grid > .anime-card:has(.poster-picture--wide)`. `:has()` is already in production in `Navbar.css`. Selectors stay anchored to the host's direct child, so an invalidation touches one card, not the grid. This also retires the `useLandscapePicture` calls in `SeriesExtraTile` and `SeriesTimeline` in favour of the same mechanism.

*Alternative rejected:* lifting shape into each host through an `onShape` callback. It needs state per item in page-level components, and in `ProfilePage` that means extracting five tile components just to own a `useState`. `:has()` gets the same result without moving state.

### D3 — The fill is a second, decorative copy of the same image

For `upright` and `wide`, `PosterPicture` renders, behind the art:

```tsx
<img className="poster-picture__fill" src={src} alt="" aria-hidden="true" draggable={false} />
```

It is absolutely positioned to cover the wrapper, with `object-fit: cover`, blurred and slightly scaled (so the blur's soft edge sits outside the box), and dimmed over `var(--code-bg)` through theme tokens (`--poster-fill-opacity`, set per theme). The art sits above it with `object-fit: contain`. The wrapper clips with `overflow: hidden` and the host's own radius.

- It exists only after the art has loaded and been classified, so it is served from the same cache entry: no second request.
- Poster art never gets one, so the default path is byte-identical to today and a grid of posters adds nothing.
- Where a height-bound tile shows the art at its full width, the copy is fully hidden behind it. It shows only where there really is spare space (a box the art can't fill, or art past a tile's bound).

*Alternative rejected:* a CSS `background-image` from an inline `--picture-url` custom property on a pseudo-element. There is no extra element, but it puts a URL into a CSS string that has to be escaped. Loading is decoupled from the `<img>`'s own lazy/eager behaviour. And it can't be dropped for posters without the same conditional render anyway.

*Alternative rejected:* a flat neutral band. The user chose the blurred copy.

### D4 — Column grids: span two tracks, strict order

A card whose picture is `wide` takes `grid-column: span 2`. Grids keep the default `grid-auto-flow: row`, never `dense`. A wide card that doesn't fit at the end of a row moves to the next row and leaves that row one cell short. That is the user's explicit choice, so sorted pages stay in their sort order.

The picture area keeps **one track's** portrait height, so a spanning card's title, meta and footer line up with its row neighbours'. Each grid publishes its gap as `--card-grid-gap` (it has to be the same value as its `gap`, as the More grid already does with `--extras-gap`). The card sets `--card-span` (1 by default, 2 when spanning), and the picture frame is sized with the padding-top technique already used for fr tracks:

```css
padding-top: calc((100% - (var(--card-span) - 1) * var(--card-grid-gap)) / var(--card-span) * 3 / 2);
```

At span 1 this is `150%`, the 2:3 box today's `aspect-ratio: 2 / 3` gives. Inside the frame the art is drawn at the frame's full height and at its own width, capped at the frame's width with `contain`. So a 16:9 visual nearly fills a spanning card, a square is drawn square with fill at its sides, and an `upright` picture in a one-track card is drawn whole with thin fill bands above and below.

The same rule replaces the More tile's current one (`--extras-gap` becomes that grid's `--card-grid-gap`), so More and the browse grids can't drift apart.

**One-column guard.** At the narrowest widths an `auto-fill` grid can drop to one track, and `span 2` would then create an implicit second column and scroll the page sideways. Each grid becomes an inline-size container (`container-type: inline-size`). The span applies only under a container query for "room for two tracks": its own `minmax()` floor twice, plus one gap. Below that, a wide card stays one track and is drawn whole inside it. The More grid has this latent bug today, and the change fixes it there too.

*Alternative rejected:* `grid-auto-flow: dense`. It keeps rows full, but moves the card after a row-end wide card ahead of it. The season page's "Unwatched" divider made it worse: a later card could be packed into a hole *above* the divider, in the wrong section. The user rejected it for the ordering reason alone.

*Alternative rejected:* leaving wide cards in one track in the grids. That was offered and not chosen.

### D5 — Fixed boxes keep their box; the picture is whole inside it

The carousel (through `AnimeCard` with no span), Top anime rank 4–10 cards, the Recap podium and the score board keep their existing frames exactly. Only the frame's content changes: `PosterPicture` fills the frame, and for non-poster art the art is `contain` over the fill. This is what keeps each of those surfaces' existing specs true without modification:

- The carousel's cards never change width, so the "exactly five cards, no sliver" bound, the one-card arrow step (`second.left - first.left`) and the edge hover reserve are untouched.
- The podium's descending silhouette and shared score box are untouched.
- The score board's tile still follows the poster art's proportion and is drawn near source resolution. Its 2px tier border moves from the `<img>` to the wrapper, so it outlines the tile rather than a letterboxed picture inside it.
- Rank 4–10 cards stay one row of seven.

Placeholders are unchanged, including the podium's and the showcase's deliberately transparent ones, which let the rank-1 wave show through.

The **series timeline** is a fixed box with one alternative width. A `wide` picture (square now included, via `:has()` in place of `isLandscape`) takes the existing widened card, `--card-w: 384px`, with the picture area pinned at the portrait height. An `upright` picture keeps the portrait card and is drawn whole inside it. `series-page` still says "one widened size", so card width stays free of duration or magnitude meaning.

### D6 — Height-bound tiles: the width follows the picture

In the profile strips, a tile's **height** is fixed and its width is the art's own width at that height, capped at 16/9 of it (the row rule's bound, for the same reason). Width is free there: the strip scrolls sideways, and nothing aligns across tiles horizontally.

The portrait tile size has to be expressible as a length, so that a wider tile's picture can stay at the portrait tile's height instead of 3/2 of its own width. Each strip becomes an inline-size container, and:

```css
--strip-tile-w: calc((100cqw - 9 * 10px) / 10);   /* ten across, as today */
--strip-tile-h: calc(var(--strip-tile-w) * 3 / 2);
```

Portrait tiles keep `flex: 0 0 var(--strip-tile-w)`, identical to today's `calc((100% - 9 * 10px) / 10)` because `cqw` resolves against the same content box. Non-poster tiles size to their picture instead. The cap is a `calc()` over a px-valued property, never a percentage, following the WebKit flex-basis note in `RowPicture.css`/`UpdateCard.css`. The Top series tile's chip row sits under the picture and simply spans the wider tile.

**Fitting is measured, not counted.** `items.length <= STRIP_VISIBLE_TILES` is only true while every tile is a portrait tile. `useStripScroll` now owns a `fits` flag. It compares the last tile's `offsetLeft + offsetWidth` against the strip's inner right edge, with a 1px tolerance so sub-pixel sums never tip a strip into scrolling. It re-measures from a `ResizeObserver` on the strip and on its tiles, because a tile widens when its picture loads. Offsets ignore transforms, so a hover-scaled tile can't tip it either. The strip's `--fits` class then follows that flag, and the profile spec's "not scrollable, no grab cursor, when everything fits" holds unchanged.

**The Top anime showcase** poster is a height-bound tile inside a flex row. It keeps its 140 × (368/260) box for posters. For other shapes the picture keeps that height and takes its own width, up to 16/9 of the height. The poster link may shrink (`flex: 0 1 auto; min-width: 140px`) while the info column keeps a minimum readable width. When squeezed, the art is `contain`ed over the fill. So a single-column showcase (≤1024px) shows a key visual at full height, and a three-across desktop showcase shows it as wide as the column allows, never at the cost of the rank, title and chips.

### D7 — The series header draws upright and square art at its own height

A poster in the header keeps today's `cover` in the 140/198 box, per the "posters identical" goal. For an upright or square picture, `.series-page__picture` takes the detail page's technique (`.anime-detail-page__picture`): it keeps its width and takes `height: auto`, so the whole picture is drawn at its own proportions. A square is drawn square and an upright picture slightly taller. The landscape header layout (grid, larger width) is unchanged and still keyed on strictly-wider-than-tall.

The header therefore needs two facts from one image: its shape and whether it is strictly landscape. The shared core hook exposes the loaded width-to-height ratio. `usePictureShape` and `useLandscapePicture` are thin derivations of it, and the header derives both from one ref instead of attaching two callback refs to one `<img>`. There is no fill here: the picture sets its own box, so nothing is ever empty.

### D8 — Before load, everything is a poster

Shape is `poster` until the art has loaded, so every surface first renders exactly today's box. It adopts the whole-picture treatment on load, as rows and the series page already specify. On the browse grids that means a wide card starts in one track and reflows to two when its picture loads. That shift only ever involves pictures the user chose: browse pages show MAL's main picture for anime not on the list, and those are posters. So a grid of ordinary season art never moves.

### D9 — A reveal step tops up to a complete row

Season, Year, Search and the Series browser reveal an already-loaded list 24 (Search: 48) cards at a time, as a sentinel under the grid scrolls into view. At six columns a step of 24 used to end on a full row. A wide card takes two cells, so a step of 24 cards now fills 25 or more, and the last card sits alone on a new row until the sentinel just below it is reached and the next step fills in beside it. An auto-fill grid below the desktop breakpoint already had the same gap whenever its column count didn't divide the step.

A shared hook, `useCompleteLastRow`, tops the revealed count up until the last revealed row is complete. It measures the laid-out grid: the gap between the last card's right edge and the grid's, divided by one track plus its gap. It doesn't count cells, because whether a card spans two columns is only known once its picture has loaded. It runs in a layout effect after every reveal or change of list, so a step's lone card never paints. It also runs from a ResizeObserver on the grid, because a picture loading as wide, or the window resizing, pushes a card onto a new row without rendering the page, and that changes the grid's height. It only ever raises the count (an absolute target, so the two triggers can't both add a row) and stops at the end of the list, so it can't fight the sentinel, shorten a restored grid, or break "a refresh can't show fewer cards".

*Trade-off:* the ResizeObserver path settles one frame later, because a state update from an observer callback renders after that frame's paint. Forcing it with `flushSync` would risk a "ResizeObserver loop" error whenever a topped-up card's cached wide picture wraps within the same frame. The single frame falls inside the reflow that the wide card's load is already causing (D8).

*Alternative rejected:* revealing the next step earlier, with a `rootMargin` on the sentinel. It hides the lone card only when scrolling is slow enough, and leaves the rows unfinished.

## Risks / Trade-offs

**A row can end one cell short** → Accepted by the user as the price of exact sort order. It happens only in the row before a wide card that lands in the last column, and only on pages holding chosen wide art. `season-browser` and `year-browser` are amended so the gap isn't a spec violation.

**Grids reflow when a wide picture loads** → Bounded to chosen art (D8). The cards before it never move. Only the cards after it shift along.

**Safari not clipping the blurred copy to the rounded wrapper** → A known WebKit quirk with filtered children inside `overflow: hidden` plus `border-radius`. The wrapper gets `isolation: isolate`. It does *not* get `transform: translateZ(0)`, which `ScoreBoardOverlay.css` documents as the cause of stale, pixelated rasters across displays with different pixel densities. Verify in Safari on the podium, the score board and a strip.

**`:has()` invalidation cost on long grids** → Selectors are anchored to direct children and keyed on one class. Only non-poster art ever changes that class, and it changes once per picture load.

**Strip fit oscillating at the boundary** → The 1px tolerance and transform-free offsets mean a strip can only change state when a tile's layout width actually changes, which happens once per picture load, or on resize.

**The WebKit `aspect-ratio`-in-fr-track bug** → Fixed-box frames keep their padding-top technique. The grid frame moves *to* it (D4) rather than relying on `aspect-ratio` on a replaced element. The art `<img>` is sized against a definite frame, never by its own aspect ratio.

**An upright picture between 0.667 and 0.75 still loses up to ~11% in a 2:3 box** → Accepted as "mostly shown". It is the same order of crop as an ordinary MAL poster today, and the classification can be tuned in one constant if that ever changes.

**Many surfaces change at once** → Mitigated as for rows: poster rendering is identical by construction (no fill element, same box, same `cover`), so the regression surface is the non-poster path only. Tasks keep each surface individually checkable, starting with the one the user reported (a square More tile).

## Migration Plan

Frontend only. There is nothing to migrate, and reverting the commit rolls it back.

Land order is shared-first: the shape hook and `PosterPicture`, then the series page (the reported square More tile, then timeline and header), then `AnimeCard` and the five grids, then the fixed boxes, then the strips with the fit measurement, then the showcase, then the row threshold.

## Open Questions

None. The two layout choices that could have gone either way, packing versus strict order on grids and blurred versus neutral fill, were answered by the user: strict order, blurred fill.
