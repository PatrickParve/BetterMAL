## 1. Repository: generalise the listing query to a set of season points

- [x] 1.1 In `Data/Repositories/ISeasonRepository.cs`, add point-set overloads alongside today's single-season methods: `GetPageAsync(IReadOnlyCollection<(int Year, string Season)> points, SeasonSortKey sort, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken)` and `HasListingAsync(IReadOnlyCollection<(int Year, string Season)> points, CancellationToken)`. Document that a year is passed as its four points and that the ordering, filtering, counting, and paging rules are identical whatever the point count (design D1).
- [x] 1.2 In `SeasonRepository.cs`, move the body of `GetPageAsync` onto the point-set overload, replacing only the opening `Where(l => l.Year == year && l.Season == season)` with a membership test over the requested points. Leave the in-my-list filter, the hentai filter, the type filter (including the `unknown` arm), the projection, `CountAsync`, all four sort orderings, and `Skip`/`Take` untouched.
- [x] 1.3 Reduce the existing single-season `GetPageAsync` and `HasListingAsync` to one-element calls into the new overloads, so there is exactly one implementation of the ordering and filtering rules.
- [x] 1.4 Verify the point-set `Where` translates to SQL rather than falling back to client evaluation — EF cannot translate `Contains` over a collection of value tuples, so build the predicate from the points explicitly (e.g. OR-ed year/season pairs, or a year plus season-name test where the points share a year, which is exactly the year case).
- [x] 1.5 Add `Data/Repositories/SeasonRepositoryTests.cs` covering the point-set path against an in-memory/SQLite context: a four-point read returns the union with no duplicates; each sort orders across all four points rather than within them (popularity's unranked-last rule, MAL score, alphabetical, and my-score's scored-first grouping); `TotalCount` is computed after the filters; and `Skip`/`Take` page continuously across the point set.
- [x] 1.6 Run the existing backend test suite and confirm every season-level test still passes unchanged — that is the regression signal for 1.2/1.3 (design "Risks").

## 2. Service: year read and year refresh

- [x] 2.1 In `Services/Season/SeasonBrowseDto.cs`, add `YearPageDto(int Year, List<AnimeBrowseItemDto> Items, int Offset, int Limit, int TotalCount, DateTimeOffset? LastFetchedAt, bool HasListing)` and `YearRefreshResultDto(SeasonRefreshOutcome Outcome)`, reusing the existing `SeasonRefreshOutcome` enum and its camelCase wire names. Document that `LastFetchedAt` is the most recent of the year's four season stamps (null only when no season of the year has ever been fetched), which is what the client's never-cached loading state keys off.
- [x] 2.2 In `ISeasonBrowseService.cs` / `SeasonBrowseService.cs`, add `GetYearPageAsync(int year, string sortKey, bool includeMyList, bool hideHentai, IReadOnlyCollection<string>? types, int offset, int limit, CancellationToken)`: build the year's four `(year, season)` points, call the point-set repository read, and pair it with the year's `HasListing` and its latest `LastFetchedAt`. Repository-only — it must never call MAL.
- [x] 2.3 Add `RefreshYearAsync(int year, CancellationToken)`: loop the four seasons in calendar order, `await` each existing `RefreshAsync` **sequentially** (design D2 — the scoped `DbContext` cannot serve concurrent queries), and fold the four outcomes with the precedence `Fetched > Skipped > NotListed > Failed`. Comment why that precedence is what makes a year outcome mean what a season outcome means. Do not add a `year:` `RefreshGate` key — the per-season keys already collapse the work where MAL is actually called.
- [x] 2.4 Extend `Services/Season/SeasonBrowseServiceTests.cs` (or add a sibling) for the fold: one season fetching and three skipped → `fetched`; all four skipped → `skipped`; all four not listed → `notListed`; all four failing → `failed`; two listed and two not → `fetched`; three fetching and one failing → `fetched`. Assert the refreshes run sequentially and that a season already fetched today triggers no MAL call.

## 3. API surface

- [x] 3.1 Add the year endpoints — `GET /api/year/{year:int}` and `POST /api/year/{year:int}/refresh` — mirroring `SeasonController`'s parameter handling exactly: the same `sort`/`includeMyList`/`hideHentai`/`type`/`offset`/`limit` query parameters, the same comma-split of `type`, and the same `Math.Clamp(limit, 1, 100)`.
- [x] 3.2 Do **not** add a year bounds endpoint. The year ceiling is `GET /api/season/bounds`'s `latestYear` (design D3); note that in a comment on the year controller so a future reader does not add one.
- [x] 3.3 Add controller tests for the year endpoints: parameter passthrough, the type comma-split, the limit clamp, and the refresh outcome serialising in camelCase.

## 4. Frontend data layer

- [x] 4.1 In `api/types.ts`, add `YearPageDto` and `YearRefreshResultDto`, reusing `AnimeBrowseItemDto` and the existing `'fetched' | 'notListed' | 'skipped' | 'failed'` outcome union.
- [x] 4.2 In `api/client.ts`, add `getYearPage(year, opts)` and `refreshYear(year)` built the same way `getSeasonPage`/`refreshSeason` are, including the same query-string assembly for `types`.

## 5. The Year page

- [x] 5.1 Create `pages/YearPage.tsx` as a sibling of `SeasonPage.tsx` (design D4 — no shared abstraction), reusing `AnimeCard`/`AnimeCardMeta`, `FilterMultiSelect`, `usePageData`, `useLatestRequest`, `useDebouncedValue`, `useContentFilter`, and the `MEDIA_TYPE_ORDER`/`mediaTypeLabel` helpers. Head the file with a comment stating that its effect structure deliberately mirrors `SeasonPage`'s and why.
- [x] 5.2 Read `year`, `sort`, `inMyList`, and `type` from the URL search params with the same defaults the season page uses (current year, `popularity`, checked, none), and key `usePageData` on `year:${year}` (design D5).
- [x] 5.3 Implement the two-effect split: a read effect re-running on year, sort, type, in-my-list, and hide-hentai (via `reload` through a ref, guarded so it does not double-fire on the initial load), and a **separate** refresh effect keyed on the debounced year alone at the season page's 400 ms, so no sort or filter change ever triggers a MAL fetch and arrow-stepping only refreshes the year settled on.
- [x] 5.4 Wire `useLatestRequest`: bump the generation on every read parameter change, gate applying background re-read and load-more results on `isLatest`, and clear `loadingMore` unconditionally in `finally` so a superseded load-more cannot wedge infinite scroll.
- [x] 5.5 Implement the four terminal states — grid, filters-empty, not-listed, load-failed — resolved in the season page's order from `items.length`, `lastFetchedAt`, `hasListing`, and `refreshOutcome`, and reset `refreshOutcome` the instant the year changes so one year's outcome can never decide another's render. Word the two failure states for a year ("MyAnimeList hasn't listed this year yet", "This year couldn't be loaded — it'll be retried next time you open it").
- [x] 5.6 Implement the accumulated type-option set keyed on the year and reset when it changes, so selecting one type does not make the year's other types disappear from the picker.
- [x] 5.7 Implement the year ceiling: seed it with the year of `shiftSeason(currentYear, currentSeason, 2)` — the season page's own default ceiling, whose year is the current year in the first half of the calendar and the next one in the second half, not a flat `currentYear + 1`, replace it from `getSeasonBounds()`'s `latestYear`, and keep the default on failure. Disable the next-year arrow at the ceiling, cap the dropdown at it, floor the previous-year arrow and the dropdown at the season page's `EARLIEST_YEAR`, and keep the currently-viewed year among the options so a URL past the ceiling still has a valid `<select>` value.
- [x] 5.8 Render the header in the season page's three-zone arrangement: title left; previous/next arrows, the "{year}" label, and the passive "Updating…" indicator centered; the year dropdown immediately right of the arrows; sort, type filter, and "In my list" after them.
- [x] 5.9 Render the results grid with the `Unwatched` divider under the my-score sort, plus the infinite-scroll sentinel and its `IntersectionObserver`.
- [x] 5.10 Create `pages/YearPage.css` reusing the season page's header grid, control, fixed six-column results grid, mobile breakpoint, divider, and sentinel rules so the two pages are visually identical below the header.

## 6. Routing and navigation

- [x] 6.1 Add the `/year` route to `AppShell.tsx`.
- [x] 6.2 Add the **Year** link to `components/Navbar/Navbar.tsx`, directly to the right of Season in `NAV_LINKS_AFTER_RECAP`, defaulting to the current year (built at render, not hoisted, so a tab open across New Year targets the right year — the same reason the Recap link is built at render).

## 7. The recap's year-page control

- [x] 7.1 In `pages/RecapPage.tsx`, add a yearly-mode sibling to `renderSeasonPageLink` rendering the same markup — the same `<Link>`-as-button, the same `CalendarIcon`, the same `&rsaquo;` — with `family--year` in place of `family--season`, the label "Browse the year", and a target of `/year?year={startYear}`. Keep the two mutually exclusive by mode so multi-year offers neither and the control row keeps its shape.
- [x] 7.2 Confirm `.recap-page__season-button`'s hover and focus rules pick up the year family's `--fam-bg`/`--fam-border`/`--fam-from` from the `family--year` class alone; add CSS only if the existing rules turn out to be scoped in a way that prevents it.

## 8. Verification

- [x] 8.1 Build the backend and run the full test suite (per the project's Docker `sdk:10.0` route), confirming the new repository, service, and controller tests pass and no existing season test regressed.
- [x] 8.2 Build the frontend with Node 22 via nvm and confirm it type-checks and compiles clean.
- [x] 8.3 Exercise the Year page by hand: a cold year fetches its four seasons once and renders; reopening it the same day makes no MAL request; visiting a season page first spares that season on the year page; each of the four sorts orders across the whole year; the type filter keeps offering the year's other types after one is selected; unchecking "In my list" shrinks the count; infinite scroll pages continuously; and back-navigation from an anime restores the year with its sort, filters, scroll position, and loaded pages.
- [x] 8.4 Exercise the edges: a future year past the ceiling reports that MAL hasn't listed it and blocks forward navigation; a bookmarked URL past the ceiling renders as asked; arrow-stepping quickly through years refreshes only the one landed on; and "Hide NSFW" removes `rx` titles from the year while leaving `r`/`r+` visible.
- [x] 8.5 Check the recap: a yearly recap offers "Browse the year" and opens the Year page on that year; a season recap still offers "Browse the season" and no year control; a multi-year recap offers neither; and the year control wears the year family on hover and focus while resting neutral.
- [x] 8.6 Run `openspec validate add-year-browser --strict` and confirm the implementation matches every scenario in the delta specs.
