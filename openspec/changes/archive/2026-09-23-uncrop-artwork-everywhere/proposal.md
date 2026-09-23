## Why

`artwork-selection` lets a my-list anime carry any picture MAL publishes for it, including landscape key visuals and square art. The rows learned to draw those whole (`artwork-presentation`), but every card, tile and poster box in the app still crops them into a portrait box. So the picture you chose shows whole on My List and cut down to a centre slice on the Home page, Season, Year, Search, the Series browser, Top anime, the Recap podium and score board, and the profile strips. On the series page itself, a **square** picture is still cropped on the header, the timeline cards and the More tiles, because those only treat a picture wider than it is tall as "not a poster".

The earlier change left the grids out on purpose, because their design is that every tile is the same size. This change brings them in. It keeps each surface's structure, colours and information, and restyles only the geometry wide art needs.

## What Changes

**One shape rule, used app-wide.** A loaded picture is one of three shapes: a **poster** (no wider than 3:4, which fills a portrait box with at most a sliver cropped), an **upright** picture (still portrait, but too wide to fill a poster box without a real crop), or **wide** (square or landscape). Posters render exactly as today everywhere. Upright and wide pictures are drawn whole. The shape is read from the loaded image, as it is today, so nothing new is fetched or stored.

**Column grids: a wide card spans two columns, in strict order.** On the Season, Year and Search grids, the Series browser, the Home page's "Followed shows airing" grid and the series page's More tiles, a card holding a wide picture takes two columns, the way More tiles already do for landscape art. Its picture area keeps a normal card's height, and the picture is drawn whole at its own width inside it. The cards stay in their sorted order: when a wide card doesn't fit at the end of a row, it moves to the next row and leaves that row one cell short. An upright picture keeps its one column and is drawn whole inside the normal box. Where a grid only has room for one column, a wide card stays one column too.

**Fixed boxes keep their size and draw the picture whole inside.** The Home "Currently watching" carousel (its exact five-card bound stays), the Top anime rank 4–10 cards, the Recap podium, the Recap score board, and the series timeline cards keep the box each has today. A non-poster picture is drawn whole inside that box. On the timeline, a square now takes the existing widened card, just as landscape art does.

**The leftover space is filled from the art itself.** Wherever a picture drawn whole leaves part of its box empty, that space is filled with a soft, dimmed, blurred copy of the same picture, not a flat grey band. Poster art gets no copy, so it renders exactly as it does today.

**Height-bound tiles take their picture's width.** The profile's "My top anime", "Top series", "Most rewatched" and "Most time spent" strips keep their tile height. A wide or upright picture's tile takes that picture's own width at that height, up to a bound. Whether a strip scrolls is decided by whether its tiles actually overflow, not by counting ten. The Top anime rank 1–3 showcase poster works the same way, and it never squeezes the rank, title and score column beside it below a readable width.

**The series page header draws any non-landscape picture whole.** A square or upright picture stays at the portrait width and takes its own height, as the detail page already does. Landscape headers are unchanged.

**Rows adopt the same threshold.** A row picture now takes its whole-picture treatment whenever it isn't poster-shaped, not only when it is at least square. A near-square portrait picture stops losing a fifth of its width. Posters are unchanged.

**Already whole, untouched:** the anime detail page, update cards, and the picture picker.

## Capabilities

### New Capabilities

_None._ The rules extend the existing `artwork-presentation` capability.

### Modified Capabilities

- `artwork-presentation`: adds the three picture shapes, the card/tile/box rules (grids span two columns in strict order; fixed boxes draw whole inside; height-bound tiles widen), and the blurred fill. The row rule moves to the new threshold. The exclusion of poster grids and strips is replaced by assigning each of them to a family. Shape detection covers every surface, not just rows.
- `series-page`: the landscape-only whole-picture requirement becomes a whole-picture requirement for every shape. Squares take the widened timeline card and the two-column More tile, upright pictures are drawn whole in the portrait box, and the header draws any non-landscape picture at its own proportions.
- `profile-stats`: list rows adopt the new threshold. The strips move from "fixed tile size" to "fixed tile height", and whether a strip fits (and so scrolls) is measured.
- `library-views`: my-list and top-anime rows adopt the new threshold.
- `season-browser`: the full-width grid rule allows the one row-end gap a wide card leaves in strict order.
- `year-browser`: the same allowance in the Year page's presentation requirement.
- `navigation-and-search`: a series card on the search results page matches the size and shape of an anime card holding a picture of the same shape, rather than every card around it.

## Impact

**Frontend only.** No backend, API, DTO, schema or dependency change.

- `frontend/src/hooks/useLandscapePicture.ts`: gains a shape-returning variant (`poster` / `upright` / `wide`). `useWidePicture` is replaced by it. `useLandscapePicture` stays for the detail page and the series header's landscape layout.
- **New** `frontend/src/components/PosterPicture.tsx` / `.css`: the one component every card, tile and poster box renders its picture through. It handles image or placeholder, shape detection, the shape class hosts style against, and the blurred fill.
- `frontend/src/components/RowPicture.tsx`: moves to the shape hook.
- Cards and grids: `AnimeCard.tsx`/`.css` (Season, Year, Search, Series browser, Home carousel and current-season grid), `SeasonPage.css`, `YearPage.css`, `SearchPage.css`, `SeriesBrowserPage.css`, `CurrentSeasonSection.css`. The carousel gets its fixed-box behaviour through `AnimeCard` with no change of its own. Its cards never change width, so its arrow stepping and five-card bound are untouched.
- Series page: `SeriesExtraTile.tsx`/`.css`, `SeriesTimeline.tsx`/`.css`, `SeriesPage.tsx`/`.css`.
- Top anime: `TopAnimePage.tsx`/`.css` (showcase and rank 4–10 cards).
- Recap: `RecapPage.tsx`/`.css` (podium), `ScoreBoardOverlay.tsx`/`.css`.
- Profile: `ProfilePage.tsx`/`.css` (five strips, and the strip-fit measurement in `useStripScroll`).
