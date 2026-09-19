## Why

`/year?year=32932734` does not fail — it renders. The year dropdown is built as one option per year from the earliest offered year to the viewed one, so an absurd year asks the browser to lay out tens of millions of `<option>` elements: the tab locks up and takes the machine with it. The same URL also starts a read and a refresh for a year MAL could never have listed, which the API then refuses. Years that plainly cannot exist should not be reachable at all.

Two smaller things are wrong alongside it. The season and year dropdowns stop at 1989, hiding seven decades MAL does have — its archive starts at winter 1917, which the backend already knows and accepts. And "Alphabetical" everywhere sorts by MAL's romaji `title` while every card and row displays the English title when there is one, so an alphabetical list reads as unsorted. My list has the same title problem, plus an Airing-status sort the user does not want, and a "Reset filters & sort" button that is indistinguishable from the button next to it.

## What Changes

**Season and year pages**

- A URL may address exactly what the arrows and dropdowns already offer — winter 1917 through the navigable ceiling — and nothing further. Anything outside that is silently replaced with the current year (year page) or the current season (season page): no message, no error state, no read, no refresh, no MAL request. A non-numeric or missing year is normalised the same way. The ceiling itself is unchanged: the current season plus MAL's two-season forward window, raised on its own when a visit caches a season MAL has opened beyond it — at most one year ahead.
- The year dropdown and the previous-year/previous-season arrows are floored at 1917 instead of 1989, so the whole archive is reachable from the control rather than only by a link from elsewhere.
- The year and season dropdowns are built from the addressable range itself, so they can never be sized by an arbitrary URL value. **This is the crash fix** — nothing else in the page ever allocates per-year.
- "Alphabetical" orders by the title each card displays — the English title when MAL has one, otherwise the romaji title. The same displayed title becomes the tie-break for the popularity, MAL-score and my-score sorts, so the order matches what is on screen in every sort.

**Finding seasons MAL has opened (new)**

- Visiting a Season or Year page quietly probes the one season immediately past the ceiling, so the horizon climbs on its own as MAL opens each new season instead of waiting for someone to think of visiting a future year. The probe reuses the existing per-season fetch path, so it costs at most one MAL request per local day, treats a `404` as a fact rather than an error, and — because the season it targets sits outside the window the ceiling's `404` step-back considers — can only ever raise the ceiling, never lower it. The endpoint that triggers it takes no year or season: the target is derived from the ceiling server-side, so reaching past the accepted range stays one fixed rule rather than something a caller can ask for.
- With a dedicated discovery path in place, a year refresh stops fetching the seasons of that year that lie beyond the ceiling. Today `RefreshYearAsync` loops all four unconditionally, which on a future year spends MAL requests on certain `404`s — and was, accidentally, the only way the ceiling ever rose past the two-season window.

**My list**

- **BREAKING (view state):** the three Airing status choices are removed from the primary and tiebreaker sort selectors. The airing-status *filter* is untouched, and so is the airing badge it drives. A saved or restored view that still names Airing status as its sort falls back to Alphabetical.
- "Alphabetical" — as primary key, as tiebreaker, and as the final fallback every sort ends in — orders by the displayed title rather than the romaji one.
- "Reset filters & sort" is drawn as an accent-coloured call to action so it reads as the way out of a narrowed list, rather than as a second copy of the "Recap a period" button beside it.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `season-browser`: out-of-range seasons addressed by URL are replaced with the current season instead of rendered; the quick-jump year list starts at 1917 and is bounded by the addressable range; alphabetical ordering and every sort's title tie-break use the displayed title; a page visit probes the season past the ceiling so the horizon climbs on its own, and that probe is named as the one fetch allowed past the accepted range.
- `year-browser`: the same URL-replacement rule and dropdown floor for years; the same displayed-title ordering; a year refresh skips the seasons of that year MAL could not have opened, and folds its outcome over the seasons it actually considered.
- `library-views`: Airing status is removed from both my-list sort selectors and from the rank-number and airing-badge rules that referenced it; alphabetical ordering uses the displayed title; the Reset filters & sort action is given visual prominence.

## Impact

- **Frontend** — `pages/SeasonPage.tsx` (the shared `EARLIEST_YEAR`/`FUTURE_SEASON_WINDOW` constants live here), `pages/YearPage.tsx`, `pages/MyListPage.tsx`, `pages/MyListPage.css`, `components/MyListControls.tsx`, `utils/anime.ts` (`SortKey`, `SORT_KEY_FACTORIES`, `SortableListItem`).
- **Backend** — `Data/Repositories/SeasonRepository.cs` computes the four per-item sort positions the season and year pages consume; the alphabetical position and the title tie-breaks change there. `Services/Season/SeasonBrowseService.cs` gains the horizon probe and trims its year-refresh loop; `Services/Season/ISeasonBrowseService.cs` and `Controllers/SeasonController.cs` gain the parameterless probe endpoint. `Services/Season/SeasonCalendar.cs` carries a comment about the frontend's 1989 floor that stops being true. No DTO or migration changes, and no change to `SeasonHorizon`, `SeasonRefreshCadence`, `SeasonRequestRange` or the accepted range itself.
- **Not changing** — the API's own range checks (already correct), MAL's two-season forward window and the ceiling arithmetic itself, the airing-status filter, the search page's own sort, and the Recap page's year pickers.
