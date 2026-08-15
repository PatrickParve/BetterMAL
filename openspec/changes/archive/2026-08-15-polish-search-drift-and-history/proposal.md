## Why

Five presentation defects, three of them in the app's most-used chrome. The type-ahead dropdown still grows a row taller whenever a series matches, so the list visibly jumps as results change under the cursor — the earlier badge fix sized the pill for the results grid but left the dropdown row's own line box untouched. The search field's focus ring is drawn outside the input with a 2px offset, so it crosses the seam and lands on top of the magnifier button beside it. And on a Mac trackpad, a two-finger sideways flick drags the whole page left and right, because nothing stops horizontal overscroll from chaining out of the poster strips to the document. The fourth is the full-history overlay: its Close button sits below the list, where the overlay's own scrolling can push it out of view, and the list's `55vh` cap slices a row in half at whatever height the window happens to be. The fifth is the anime detail page's two scores, each wrapped in a tinted, bordered chip inside a box that otherwise holds plain labelled lines — a box inside a box, where the colour of the number alone would say the same thing.

## What Changes

- **The type-ahead dropdown's rows are one height.** A series row and an anime row take exactly the same vertical space, so the dropdown neither grows nor reflows as matches change. The series title and its badge line both fit within that shared row height, and the badge stays a legible pill.
- **The search field is restyled as one control.** The input and the magnifier become a single bordered field with the icon button inset, and the focus ring is drawn on the field as a whole rather than on the input alone — nothing overlaps the icon, at any font size.
- **The page no longer drifts horizontally.** A sideways trackpad gesture — anywhere on any page, including at the end of a horizontally scrolling poster strip or timeline — leaves the page where it is instead of sliding it or rubber-banding it. The strips themselves keep scrolling exactly as they do now.
- **The full-history overlay closes from a ✕ in its top-right corner.** The Close button below the list is replaced by an icon control aligned with the title, keyboard-reachable and labelled, with Esc and click-outside still closing the overlay.
- **The full-history list opens showing five whole rows.** Its height is derived from the row height rather than from the viewport, so five rows are fully visible with none clipped mid-row; the rest scroll.
- **The detail page's two scores become coloured numbers.** The MAL score and my score drop their chips and read as `MAL score: 8.42` and `My score: 9` on labelled lines, the number carrying the blue or purple role and nothing drawn around it — the same colour-only form My List and the Top anime page already use. The two boxes themselves stay, as does everything else in them; the chip form stays in use on the series page and the profile's top-series tiles.

## Capabilities

### New Capabilities

None — each item refines behaviour an existing capability already owns.

### Modified Capabilities

- `navigation-and-search`: dropdown rows are uniform height whether the match is a series or an anime; the search field is a single control whose focus indication stays inside its own bounds; and a new requirement states that no page scrolls or drifts horizontally under a trackpad gesture.
- `profile-stats`: the full-history overlay is dismissed by a ✕ in its top-right corner, and its list shows five whole rows on open rather than a viewport-derived height that clips one.
- `anime-detail`: the two score boxes present their scores as coloured numbers on labelled lines instead of chips, with the boxes and the hide/reveal behaviour otherwise unchanged.
- `score-presentation`: the chip-versus-coloured-value rule gains the case this creates — a score that sits as one labelled line among others takes the coloured-value form — and the detail page moves from the chip side of that rule to the value side.

## Impact

**Frontend**
- `components/SearchBar.css` — the field restyle (single bordered wrapper, inset button, `:focus-within` ring) and the uniform dropdown row height.
- `components/SearchBar.tsx` — markup for the restyled field; the series row's text column gains nothing beyond what the new row height needs.
- `components/SeriesBadge.css` — an explicit line-height so the badge line stops inheriting the root's ~23px one, which is what makes the dropdown row grow.
- `index.css` — `overscroll-behavior-x` on the document and horizontal-overflow containment on the app shell.
- `components/CurrentlyWatchingCarousel.css`, `components/SeriesTimeline.css`, `pages/ProfilePage.css` — scroll containment on the horizontal strips so their overscroll does not chain to the page.
- `components/EditHistoryOverlay.tsx`, `EditHistoryOverlay.css` — the ✕ control in a title row, and the five-row list height.
- `pages/AnimeDetailPage.tsx`, `AnimeDetailPage.css` — the two scores become labelled lines carrying `.score--mal` / `.score--mine`; the chip-scoped `min-width` override in the page CSS goes with them. `ScoreChip` itself is not touched.

**Backend**
- None. No DTO, endpoint, or query changes.

**Not affected**
- Search ranking, the merge of local and live results, the 5-row dropdown budget, and everything the results page renders.
- The strips' own drag-to-scroll, their hover treatments, and the restoration of their scroll offsets on back-navigation (`page-state-restoration`).
- The history overlay's fetch, its title/date filtering, its row content, and its Esc / click-outside dismissal.
- `ScoreChip` and its other callers — the series page's average chips, the series timeline cards, the series extra tiles, and the profile page's top-series tiles all keep the chip form. Score *visibility* is untouched: the hide toggle, the per-score reveal, and the completed-scores setting all behave as they do now on the detail page.
