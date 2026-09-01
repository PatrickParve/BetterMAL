## Why

Five small misreadings, each one where the app is stricter or blunter than the person using it. Search only matches the exact characters typed, so "full metal" finds nothing for Fullmetal Alchemist and "re zero" finds nothing for Re:Zero — the query is right and the app says no. The Settings page's force-refresh picker is the one search surface still naming anime in romaji while every other surface in the app shows English. A long detail-page title wraps wherever the browser finds a break opportunity, so "Fullmetal Alchemist: Brotherhood — Reflections" can snap mid-token at a hyphen and leave a dangling fragment. And the Series page's default sort — My average — leaves a large group of series in title order once several share the same average, with no way to narrow the page to the multi-entry franchises the profile's Top series section can already isolate.

## What Changes

- **Search ignores case, spacing, punctuation, and accents.** Every title comparison the search makes — anime and series alike, in the type-ahead, on the results page, in the local fallback, and for a double-quoted exact query — runs over a normalized form of both sides: lower-cased, accents folded, and every character that is not a letter or digit removed. "full metal", "Full-Metal", and "fullmetal" all match *Fullmetal Alchemist*; "re zero" matches *Re:Zero*; "kaguya sama" matches *Kaguya-sama*. Match quality (exact ahead of prefix ahead of contains), popularity ordering, the series/anime row budgets, and the background-build trigger are all unchanged — only what counts as a match widens. **BREAKING** for the `navigation-and-search` spec's "exactly equals the quoted text" wording, which becomes equality under the same normalization.
- **The Settings force-refresh picker names anime in English.** Its dropdown rows, the text it puts in the field when a row is picked, and its success/failure messages all use the app's existing English-title-preferred rule instead of the raw MyAnimeList title.
- **A detail-page title breaks only at spaces.** When the title wraps to a second line, the break falls at a space; a token containing a hyphen, colon, slash, or dash moves to the next line whole rather than being split across the two.
- **My average breaks its ties.** On the Series page, two series with the same main-line my-average are ordered by the average position their main-line entries hold in my rankings (nearer the top first), then by which has more main-line episodes aired, then — as every sort already does — by display title. A series none of whose main-line entries is ranked is placed after every tied series that has a rank average. This needs two figures the series-list endpoint does not carry today.
- **The Series page gets the Top series multi-entry filter.** The same control the profile page's Top series strip carries: switched on, it lists only series whose main line holds two or more entries that have started airing; switched off (the default), the page lists everything it lists today. It is a filter like the Progress and Status groups — it composes with them and with the sort, narrows the whole loaded list rather than the rendered part, and rides in the URL as they do.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `navigation-and-search`: title matching for both anime and series is defined over a normalized form (case, whitespace, punctuation, and diacritics all insensitive), including for a double-quoted exact query.
- `settings-page`: the force-refresh anime picker displays English titles, matching every other surface.
- `anime-detail`: the page title wraps at spaces only, never inside a token.
- `series-browser`: the My average sort gains a defined tie-break chain, and the page gains a multi-entry filter matching the profile's Top series control.

## Impact

- `backend/AnimeTracker.Api/Services/Search/SearchTextMatch.cs` — a `Normalize` helper plus normalized `EqualsIgnoreCase`/`ContainsIgnoreCase`/`StartsWithIgnoreCase`. Every anime and series match already routes through these three, so `AnimeSearchService` and `SeriesSearchIndex` need no matching logic of their own changed.
- `backend/AnimeTracker.Api.Tests/Services/Search/AnimeSearchServiceTests.cs`, `SeriesSearchLookupTests.cs` — cases for spacing, punctuation, and accent insensitivity, and for the quoted-exact path.
- `backend/AnimeTracker.Api/Services/Series/SeriesListDto.cs`, `SeriesRankingIndex.cs`, `SeriesListService.cs` — `MainLineAiredCount` and `MainLineAverageRank` on the list item; `ListedSeries` takes the ranking snapshot the way it already takes the aired-episode map; `SeriesListService` gains `IAnimeRankingService`.
- `backend/AnimeTracker.Api.Tests/Services/Series/*` — every `new SeriesListService(...)` call site gains the ranking argument; new figure and tie-break tests.
- `frontend/src/api/types.ts` — the two new `SeriesListItemDto` fields.
- `frontend/src/utils/anime.ts` — the My average comparator's tie-break chain and a `filterSeries` multi-entry parameter.
- `frontend/src/pages/SeriesBrowserPage.tsx`, `SeriesBrowserPage.css` — the multi-entry toggle button, its URL parameter, and its active styling.
- `frontend/src/pages/SettingsPage.tsx` — `pickDisplayTitle` in `AnimeRefreshPicker`.
- New `frontend/src/components/SpaceWrappedTitle.tsx` (+ css), used by `frontend/src/pages/AnimeDetailPage.tsx` for its `<h1>`.
- No database migration and no API contract removal: the series-list response gains two fields and nothing else changes shape.
