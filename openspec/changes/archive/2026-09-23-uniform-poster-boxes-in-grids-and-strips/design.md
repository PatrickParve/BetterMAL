## Context

`uncrop-artwork-everywhere` put every card, tile and poster box into one of four families and gave each its own way of drawing non-poster art whole (see the `artwork-presentation` spec):

| Family | Rule | Surfaces today |
|---|---|---|
| Column grid | a wide card spans two tracks, strict order | Season, Year, Search, Series browser, Home "Followed shows airing", series More tiles |
| Fixed poster box | box unchanged, art `contain`ed inside over a blurred fill | Home "Currently watching" carousel, Top anime 4–10, Recap podium, score board, series timeline |
| Height-bound tile | height fixed, width follows the art up to 16/9 | Profile "My top anime", "Top series", "Most rewatched", "Most time spent"; Top anime 1–3 showcase |
| Banner box | fixed wide band, every shape `contain`ed, no fill | Airing slots |

All the drawing is done by one leaf, `PosterPicture`. It sets `poster-picture--{shape}` on its wrapper and, for non-poster art, `object-fit: contain` over a blurred copy. The **host** decides the geometry. Column grids and height-bound tiles read the wrapper's shape with `:has()` and change the card's span or the tile's width. A fixed poster box does nothing at all, and `PosterPicture`'s default treatment inside the host's unchanged box *is* the "Currently watching" look.

The user has now seen the result and wants the "Currently watching" look on the browse grids and the profile strips. Spanning and widening break those surfaces' structure: rows end short, a strip of ten scrolls, and the top ten stop reading as ten equal tiles. The user named exactly which surfaces move and said no others should.

## Goals / Non-Goals

**Goals:**

- The Season, Year, Search, Series browser and "Followed shows airing" grid cards, and the four profile strips' tiles, draw non-poster art exactly as a "Currently watching" card does: same box as a poster, whole art centred inside, and the same blurred fill.
- They get it through the same code path as the carousel, not a copy of it, so the two can't drift apart.
- Posters render exactly as today on every surface.
- Nothing on these surfaces moves when a picture loads.

**Non-Goals:**

- The series page's More tiles (they still span two columns), timeline cards and header.
- The Top anime rank 1–3 showcase poster (it still takes its picture's width). The rank 4–10 cards, podium and score board are already fixed boxes.
- The airing banner boxes and every row picture slot.
- The shape classification, `PosterPicture`'s markup, and the fill's look. All of them stay as they are.
- Changing *which* picture is shown (`artwork-selection`).

## Decisions

### D1 — Move surfaces by deleting host geometry, not by adding a mode

`PosterPicture` already draws a fixed poster box's picture the way the carousel does. A host joins the fixed-box family simply by *not* reacting to the shape. So the change is almost entirely deletion:

- the five grids' `@container (min-width: …) { … > .anime-card:has(.poster-picture--wide) { grid-column: span 2; --card-span: 2 } }` blocks;
- the four strips' `:has(> .poster-picture:not(.poster-picture--poster))` widening blocks (tile `flex: 0 0 auto`, wrapper `width: auto`, art `height: var(--strip-picture-h)` / `max-width: … * 16 / 9`, and Top series' chip-row `width: 0; min-width: 100%`).

Once those are gone, each card and tile keeps its poster box, and `PosterPicture.css`'s `.poster-picture--upright/--wide > .poster-picture__art { object-fit: contain }` plus the fill do the rest. The carousel runs exactly this path today, through the same `AnimeCard`.

*Alternative rejected:* a `fixed` prop on `PosterPicture` or `AnimeCard`. Family membership is already expressed by whether the host reacts to the shape. A prop would be a second way of saying the same thing, and the two could disagree.

### D2 — `AnimeCard`'s frame goes back to one track

`AnimeCard.css` sizes the picture frame as `padding-top: calc((100% - (var(--card-span, 1) - 1) * var(--card-grid-gap, 0px)) / var(--card-span, 1) * 3 / 2)`. That calc exists only so a two-track card can keep one track's picture height. With no `AnimeCard` host spanning any more, it becomes `padding-top: 150%`, the value it already computes at span 1. The frame keeps the padding-top technique, not `aspect-ratio: 2 / 3`, for the two reasons its comment gives: the art is absolutely positioned against the padding box, and the WebKit aspect-ratio-in-an-fr-track mis-sizing.

The five grids then lose `--card-grid-gap` (their `gap` goes back to a literal) and `container-type: inline-size`. Both existed only for the span calc and for the one-column guard's container query.

Making the "Followed shows airing" grid a size container had a knock-on effect. The grid stopped lending its content width to the stacked home column, so `HomePage.css` gained `.home-page__main { width: 100% }` under the ≤1024px breakpoint. That rule stays. A stacked column filling its row is the intended layout whether or not the grid is a container, and removing the rule would make the column's width depend on the grid's intrinsic sizing again. Only its comment changes, to state the intent rather than the container workaround.

`SeriesExtraTile.css` has its own copy of the span-aware frame, fed by `.series-page__extras-grid`'s `--card-grid-gap`. More tiles still span, so it is untouched.

*Alternative rejected:* keeping the calc with its fallbacks. It is harmless at span 1, but it would be generality with no caller, and it would suggest that `AnimeCard` grids can still span.

### D3 — Complete-row reveal stays, and its spec moves to `season-browser`

`useCompleteLastRow` tops up a progressive reveal so its last row is complete. It was written for wide cards, but its comment already notes a second cause: below the 1024px breakpoint the grids use `repeat(auto-fill, minmax(…))`, whose column count needn't divide the reveal step (24, or 48 on Search). That cause remains. It measures the laid-out grid rather than counting, so it is correct with or without wide cards. Its ResizeObserver is still how a window resize that leaves the last row short gets topped up. The hook stays as it is, and only its comment changes to drop the wide-card rationale.

The spec rule lived in `artwork-presentation`'s column-grid requirement. Once no progressively revealed grid is a column grid, it has nothing to do with artwork there. It moves into `season-browser`'s "Season grid fills the content width". Year ("the same grid"), Search ("matches the season grid's layout") and the Series browser ("the same card and grid form") already defer to that grid form. The moved rule names all four grids so none of them relies on an inference.

*Alternative rejected:* deleting the hook. It would bring back lone cards at narrow widths, which that change fixed as a side effect.

### D4 — Strips keep their measured fit and their length-valued tile size

Every tile is now `var(--strip-tile-w)` × `var(--strip-tile-h)`, so "fits" by measurement and "fits" by `items.length <= 10` agree again. `useStripScroll` keeps measuring anyway. The measurement is already exact, including the border-box and 1px sub-pixel tolerance the spec asks for. Going back to counting would reintroduce an assumption (every tile is a poster tile) that the spec no longer needs. Its per-tile ResizeObserver no longer has a picture-load trigger, but its node-list diff is what re-measures after a filter switch swaps the tiles, so it stays. Only the comment's "a tile widens as its picture loads" rationale changes.

The strips also keep `--strip-tile-w` / `--strip-tile-h` and `container-type: inline-size`. They still give the one tile size all four strips share as a length, and Top series' picture height (`--strip-picture-h`) is written against it. Only the comments that justify them by the widening tile change.

### D5 — The narrowed families keep their rules

The column-grid family is left with the More tiles, and the height-bound-tile family with the Top anime showcase. Their requirements stay, and their scenarios are rewritten against the surfaces still in them. The column-grid requirement also says outright that a fixed-box grid never spans, so moving the browse grids is a stated rule rather than an absence. The `:has()` rules in `SeriesPage.css`, `SeriesTimeline.css` and `TopAnimePage.css` are untouched.

## Risks / Trade-offs

**Wide art is drawn smaller in the grids and strips** → A 16:9 key visual in a 2:3 box fills about 37% of the box's height, where a two-column card drew it at full height. This is the carousel's existing look, which the user asked for by name. The blurred fill keeps the box from reading as empty bands.

**Safari corner bleed on strip tiles during hover scale** → The fill used to appear in a strip only past a very wide tile's bound. Now it appears on every non-poster tile, inside a tile that has `border-radius`, `overflow: hidden` and a hover `transform: scale(1.08)`. The wrapper already has `isolation: isolate` for the known WebKit filtered-child quirk. Verify in Safari while hovering a non-poster tile. If the blur shows past the rounded corners, give the wrapper the tile's inner radius. Don't add a `translateZ` layer: `ScoreBoardOverlay.css` documents why.

**Stale comments** → Several comments cite "the column-grid rule", "span 2", "height-bound tile" or "a tile widens" for these surfaces (`AnimeCard.css`, the five grid stylesheets, `HomePage.css`, `ProfilePage.css`, `ProfilePage.tsx`, `useCompleteLastRow.ts`, `PosterPicture.tsx`). A task greps for them so none are left describing behaviour that no longer exists.

## Migration Plan

Frontend only. No data, API or stored state changes, and reverting the commit restores two-column cards and widening tiles.

Order: `AnimeCard` frame and the five grids first, since they are one visible unit and check the "Followed shows airing" case the user led with. Then the four strips. Then the comments. Then build, lint and a visual pass.

## Open Questions

None. The user fixed the scope ("those places I named are the only places I want this change") and the target look ("same style of image as in Currently watching").
