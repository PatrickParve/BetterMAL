## Why

`uncrop-artwork-everywhere` made a wide picture (square or landscape) take **two columns** in the browse grids and a **wider tile** in the profile strips. That shows the picture whole, but it breaks the structure those surfaces are built on. A sorted grid ends rows one cell short and reflows as pictures load. A profile ranking stops showing its first ten entries as ten equal tiles: one wide tile pushes the tenth out of view and makes a ten-entry strip scroll. The Home page's "Currently watching" carousel already handles the same art differently. Its card keeps its portrait box and draws the picture whole inside it over a blurred copy of itself. The user wants that treatment on the grids and strips where a sideways-long picture would otherwise break the layout.

## What Changes

**These surfaces move to the fixed poster box treatment the "Currently watching" carousel uses.** Every card or tile keeps the portrait box a poster has. An upright or wide picture is drawn whole inside that box, centred, with the leftover space filled by the blurred, dimmed copy of the same picture:

- the Home page's **"Followed shows airing"** grid;
- the **Season**, **Year** and **Search** results grids;
- the **Series browser** grid;
- the profile page's **"My top anime"**, **"Top series"**, **"Most rewatched"** (both scopes) and **"Most time spent"** strips.

As a result:

- **Grids never span two columns.** Every card is one column wide whatever its picture, so a sorted grid's rows are always full and nothing reflows when a picture loads. The one-short-row exception the Season and Year specs allow for a wide card goes away.
- **Strips always show ten equal tiles.** Every tile is the poster tile's size, so a strip of ten entries always fits and never scrolls because of a picture's shape.
- **Posters render exactly as today** on every one of these surfaces. There is no new element and nothing is fetched.
- **The reveal rule stays.** A progressively revealed grid still ends each reveal on a complete row. That rule used to be justified by wide cards. It now rests on the grid itself, because auto-fill column counts below the desktop breakpoint don't divide the reveal step.

**Deliberately unchanged**, because the user named only the surfaces above:

- the series page's **"More" tiles**, which still span two columns under the column-grid rule, and its timeline cards and header;
- the **Top anime** page's rank 1–3 showcase poster, which still takes its picture's width, and its rank 4–10 cards, which are already fixed boxes;
- the recap podium and score board, which are already fixed boxes;
- the airing page's banner boxes, and every row picture slot (My List, Airing today, feeds, overlays, the search dropdown).

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `artwork-presentation`: the named grids and strips move out of the column-grid and height-bound-tile families and into the fixed-poster-box family. The column-grid rule then covers only the series page's More tiles, and the height-bound-tile rule only the Top anime showcase. The reveal-completes-a-row paragraph moves out of the column-grid rule. The scenarios that used the moved surfaces as examples are rewritten.
- `season-browser`: the grid no longer allows a row to end short for a wide card. The rule that each progressive reveal ends on a complete row moves here, the home of the shared browse grid form.
- `year-browser`: drops the same wide-card short-row allowance from its presentation requirement.
- `profile-stats`: the poster strips go back to a fixed tile **size**. Every tile is the poster tile whatever its picture, the picture is drawn whole inside it, and a strip of ten always fits.

## Impact

**Frontend only.** No backend, API, DTO, schema or dependency change, and nothing to migrate.

- `frontend/src/pages/SeasonPage.css`, `YearPage.css`, `SearchPage.css`, `SeriesBrowserPage.css`, `frontend/src/components/CurrentSeasonSection.css`: drop the `@container` rule that spans a wide card over two columns, and the `--card-grid-gap` / `container-type` it needed.
- `frontend/src/components/AnimeCard.css`: the picture frame no longer needs the span-aware `padding-top` calc, since no host spans any more.
- `frontend/src/pages/ProfilePage.css`: drop the four strips' non-poster widening rules, so every tile keeps the poster tile's size and `PosterPicture` fits the art inside it over its fill.
- `frontend/src/pages/ProfilePage.tsx` (`useStripScroll`), `frontend/src/hooks/useCompleteLastRow.ts`, `frontend/src/components/PosterPicture.tsx`: behaviour kept. Their comments are updated where they justify themselves by wide cards or widening tiles.
- Untouched: `SeriesExtraTile`, `SeriesTimeline`, `SeriesPage`, `TopAnimePage`, `RecapPage`, `ScoreBoardOverlay`, `AiringPage`, `RowPicture`, and the shape hook.
