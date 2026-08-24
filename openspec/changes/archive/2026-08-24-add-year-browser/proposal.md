## Why

The season browser answers "what came out this season", but there is no way to ask the same question of a whole year. Seeing everything that aired in 2019 today means visiting four season pages in turn, comparing four separate sorts by eye, and holding the result in your head — the one grouping the recap page already ranks seasons *within*, and the one a yearly recap naturally invites you to go and browse.

Everything needed to answer it is already cached: a year is exactly its four seasons, and the season browser already stores each anime under the season MAL files it in. The missing piece is a page that reads the four together.

## What Changes

- **A new Year page** at `/year`, titled *Yearly anime*, showing every anime MAL classifies under any of the selected year's four seasons — winter, spring, summer, and fall of that year — as one combined grid. No new MAL concept and no new MAL endpoint: the year is the union of the four season listings the season browser already fetches and caches.
- **The navbar gains a Year link**, directly to the right of Season, opening the current year by default.
- **Year selection mirrors season selection**: previous/next arrows step one year at a time, a dropdown jumps straight to a year, and the selection lives in the URL so it survives back-navigation from an anime page. Forward navigation stops at the year of the season browser's own navigable ceiling, so the Year page can never offer a year MAL has not opened any part of.
- **The same controls the season page has**: sort by popularity, MAL score, alphabetically, or my score (with the same scored-first ordering and `Unwatched` divider); a multi-select Type filter; and an "In my list" checkbox, checked by default. Sorting and filtering are applied across the whole year at once, server-side, so paging and infinite scroll stay correct.
- **The same cache-first read and visit-triggered refresh**: the page renders from the cache immediately and refreshes the year's four seasons in the background, each subject to the season browser's existing once-per-local-day rule and per-season single-flight guard — so a year whose seasons were fetched today costs no MAL request at all, and a year page and a season page visited in either order never fetch the same season twice.
- **A yearly recap links to its year page**, mirroring the season recap's existing "Browse the season" button: a *Browse the year* control on a yearly recap opens the Year page on that year. It takes the **year** colour family on hover and focus, as the season control takes the season family, and is not offered on a season or multi-year recap.
- **Hide NSFW covers the Year page** as it covers the season browser, since the Year page is the same browsing surface at a wider grain.

The name: **Year**, not *Annual* — the app already speaks in years (the recap's Yearly tab, the recap's year rankings, the achromatic `year` colour family), and "Year" keeps the navbar link one short word beside "Season".

## Capabilities

### New Capabilities

- `year-browser`: browsing every anime that aired in a selected year, assembled from that year's four cached season listings — year selection and its navigable ceiling, the combined listing, the sort/type/in-my-list controls, cache-first reads with a per-season background refresh, the page's terminal empty states, and infinite scroll.

### Modified Capabilities

- `navigation-and-search`: the navbar's left group gains **Year** directly to the right of Season, making the order Home, My List, Recap, Top, Season, Year, Airing.
- `list-recaps`: a yearly recap offers a control that opens the year browser on that year, drawn in the year colour family — the year-level counterpart to the season recap's existing season-page control.
- `season-browser`: the Hide-NSFW exclusion is no longer scoped to the season browser alone — it applies to the year browser too, which is the same listing data read at a wider grain. Every other page it names stays unaffected.
- `page-state-restoration`: the enumeration of routed pages that participate in restoration gains the Year page.

## Impact

**Backend**

- `Services/Season/ISeasonBrowseService.cs`, `SeasonBrowseService.cs` — a year read that pages the union of four seasons' listings, and a year refresh that runs the four existing per-season refreshes and reports one combined outcome.
- `Data/Repositories/ISeasonRepository.cs`, `SeasonRepository.cs` — the existing paged listing query generalised from one `(year, season)` point to a set of them, so one query sorts and pages the whole year rather than four queries merged in memory.
- `Controllers/SeasonController.cs` (or a sibling year controller) — `GET /api/year/{year}` and `POST /api/year/{year}/refresh`.
- No schema change, no migration, no new MAL call: `SeasonAnimeListing`, `SeasonFetchLog`, and `IMalClient.GetFullSeasonAsync` are used exactly as they are today.

**Frontend**

- `pages/YearPage.tsx` + `YearPage.css` — the new page, following `SeasonPage`'s two-effect shape (a cache-first read that re-runs on sort/filter changes, and a debounced refresh keyed on the period alone).
- `api/client.ts`, `api/types.ts` — `getYearPage` / `refreshYear` and their DTOs.
- `AppShell.tsx` — the `/year` route.
- `components/Navbar/Navbar.tsx` — the Year link.
- `pages/RecapPage.tsx`, `RecapPage.css` — the yearly recap's *Browse the year* control.

**Not affected**

The season page itself keeps its behaviour, its route, and its URL state unchanged; the Year page is additive. The recap's season-page control, the season browser's horizon rules, and the MAL fetch path are reused rather than altered — the once-per-day rule and single-flight guard are what keep a year visit from multiplying MAL traffic.
