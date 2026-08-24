## 1. Shared backend groundwork

- [x] 1.1 Extract the series status precedence out of `SeriesService.ComputeStatus` into a `SeriesStatusRules.Compute(IEnumerable<string?> mainLineAiringStatuses, IEnumerable<string?> allAiringStatuses)` static helper in `Services/Series/`, operating on airing-status strings rather than `AnimeMetadata`, so a projection with no navigation properties can call it (design D5). Keep the four-step precedence and its comments verbatim.
- [x] 1.2 Rewrite `SeriesService.ComputeStatus` to delegate to the new helper, changing no behaviour, and confirm `SeriesServiceComputeStatusTests` still passes untouched.
- [x] 1.3 Add `Task<Dictionary<int, int>> EpisodesAiredAsOfAsync(IReadOnlyCollection<int> animeIds, DateTimeOffset nowUtc, CancellationToken ct = default)` to `IEpisodeScheduleService`, implement it in `EpisodeScheduleService` as the direct `IEpisodeAiringRepository.GetMaxAiredEpisodesAsync` call, and make the existing `AnimeMetadata` overload delegate to it (design D6). Leave the four existing callers on the entity overload.
- [x] 1.4 Widen `SeriesRankingMemberProjection` and the `SeriesRankingLookup` query with the fields the list page needs and the ranking index does not yet project: `AiredFrom`, `AiredTo`, and the member's `Order`. Confirm `SeriesRankingLookupTests` and the profile's Top series tests still pass — the added columns must change nothing about the existing aggregations (design D2).

## 2. Series list projection

- [x] 2.1 Add `SeriesListItemDto` in `Services/Series/` carrying: `SeriesId`, `RootAnimeId`, `Title`, `EnglishTitle`, `PictureUrl`, `Status`, `ProgressBadge`, `BehindEpisodes`, `MalMain`, `MineMain`, `MalRevealed`, `FirstYear`, `LastYear`, `MainLineEpisodeTotal`, `HasUnknownEpisodeCounts`, `EntryCount`, `MainLineWatchedEpisodes`, `MainLineAiredEpisodes`. Add `SeriesListDto(List<SeriesListItemDto> Items)`. Document why the episode total is main-line-scoped while the entry count is not (design D7).
- [x] 2.2 Define the progress badge as an enum-like wire value `"Completed" | "CaughtUp" | "Behind" | "None"` with `BehindEpisodes` populated only for `Behind`, and note in its doc comment that it mirrors `SeriesPage.tsx`'s client-side `completionBadge` precedence (design D5) — the two must be changed together.
- [x] 2.3 Add `ListedSeries(Dictionary<int, int> airedEpisodesByAnimeId)` to `SeriesRankingIndex`, beside `EligibleSeries()` and `RewatchedSeries()`. Filter to series with at least one member whose `EntryStatus is not null`, and deliberately **do not** apply `EligibleSeries()`'s two-aired-main-line-entries coverage rule — document why at the call site (design D3).
- [x] 2.4 In `ListedSeries`, compute status via `SeriesStatusRules.Compute` over the main line's and all members' airing statuses.
- [x] 2.5 In `ListedSeries`, compute the two main-line averages with `SeriesAverages.Mal`/`SeriesAverages.Mine` and `MalRevealed` as `SeriesAverages.MainLineSettledByMe(mainLine) && !mainLineAiring` — the same expression `EligibleSeries()` uses, so the card can never reveal what the series page blurs (design D4).
- [x] 2.6 In `ListedSeries`, compute the main-line episode total exactly as `SeriesService.BuildStats` does: a known `TotalEpisodes` contributes in full, an unknown contributes its aired-so-far count and sets `HasUnknownEpisodeCounts` (design D7).
- [x] 2.7 In `ListedSeries`, compute `FirstYear`/`LastYear` from members' `AiredFrom`/`AiredTo` across every member, matching the series page's year span, with null when no member has a date.
- [x] 2.8 In `ListedSeries`, compute the progress badge by the four-step precedence in the `series-browser` spec: `Completed`, then `CaughtUp`, then `Behind` with its summed unwatched-broadcast count, then `None` — including the two "no badge" cases (an unwatched finished-airing main-line entry; an unknown broadcast count for a currently-airing main-line entry, i.e. an id absent from the aired-episodes dictionary).
- [x] 2.9 In `ListedSeries`, compute `MainLineWatchedEpisodes` and `MainLineAiredEpisodes` — the two figures the client's My-progress sort divides — with `MainLineAiredEpisodes` summed over known-total main-line members only, matching `SeriesService.MainLineAiredEpisodesFromMap`.
- [x] 2.10 Add `SeriesListService` (or a method on the existing service seam, whichever matches the surrounding pattern) that loads the index via `SeriesRankingLookup`, resolves aired-episode counts for the currently-airing member ids in one batched call via the new id overload, calls `ListedSeries`, and returns the items in the deterministic default order: my main-line average descending, nulls last, then raw title case-insensitively.

## 3. Read endpoint

- [x] 3.1 Add `GET /api/series/list` to `SeriesController` returning `SeriesListDto`, with a doc comment stating that it builds nothing, refreshes nothing, and makes no MAL call — its cost is bounded by what is stored (design D1, spec "Series list read endpoint").
- [x] 3.2 Confirm an empty store returns `{ items: [] }` with a 200 rather than a 404 or an error, and that the endpoint short-circuits the join when no series exists at all, the way `SeriesRankingLookup.LoadAsync` already does.
- [x] 3.3 Register any new service in `Program.cs` alongside the existing series registrations.

## 4. Backend tests

- [x] 4.1 `SeriesListEligibilityTests`: a series with one member in my list is listed; a series with no member in my list is not; an extra-only membership still lists the series; a three-aired-main-line-entry franchise with only one entry in my list is listed here even though `EligibleSeries()` excludes it.
- [x] 4.2 `SeriesListProgressBadgeTests`: the four precedence outcomes plus every "no badge" case — an unwatched finished-airing main-line entry, a series with nothing aired at all, and a currently-airing entry with no stored aired count. Cover the same cases `SeriesPage.tsx`'s `completionBadge` handles, since the two implementations must agree (design D5).
- [x] 4.3 `SeriesListScoreRevealTests`: `MalRevealed` is true only when every finished-airing main-line entry is Completed **or Dropped** and no main-line entry is currently airing; false for a finished-airing entry absent from my list; false while a main-line entry airs.
- [x] 4.4 `SeriesListFiguresTests`: main-line episode total with and without unknown counts (including the all-unknown zero-total case), year span across members including the single-year case, entry count covering extras, and `MainLineWatchedEpisodes`/`MainLineAiredEpisodes`.
- [x] 4.5 `SeriesListOrderingTests`: the endpoint's default order is my-average descending with nulls last and title as the tie-break, and is stable across two calls on unchanged data.
- [x] 4.6 A test asserting the endpoint issues no MAL call and triggers no build when stored series are partial — the guarantee the whole page rests on.

## 5. Frontend data layer

- [x] 5.1 Add `SeriesListItemDto`, `SeriesProgressBadge`, and `SeriesListDto` to `frontend/src/api/types.ts`, mirroring the backend records with the same doc comments about the two differently-scoped counts.
- [x] 5.2 Add `getSeriesList()` to `frontend/src/api/client.ts` following the existing call conventions.

## 6. The series card

- [x] 6.1 Add `SeriesCard.tsx` + `SeriesCard.css` composing `AnimeCard` with `to={/series/${rootAnimeId}}` and a series meta block as `children`, so it inherits the fluid grid sizing, hover treatment, title truncation, and click target (design D10).
- [x] 6.2 Render the status pill and progress badge in one row, reusing the series page's four status colours and three badge colours rather than defining new ones — extract the shared colour rules if they are currently local to `SeriesPage.css`.
- [x] 6.3 Render the two averages as `ScoreChip size="compact"` chips, the MAL one wrapping `<ScoreValue value={...} completed={malRevealed} />` exactly as the Top series strip and the series page do, and my average as its two-decimal value or `No score`. No scored-count suffix on either.
- [x] 6.4 Render the meta line: year span, main-line episode total via a shared `formatEpisodeTotal`-equivalent (lift the one in `SeriesPage.tsx` into `utils/anime.ts` rather than copying it), and the entry count using `SeriesBadge`'s entry/entries wording.
- [x] 6.5 Keep every element passive and inside the card's link, per `navigation-and-search`'s clickable-cards requirement — `ScoreValue`'s reveal button already stops its own propagation.

## 7. The Series page

- [x] 7.1 Add `SeriesBrowserPage.tsx` + `SeriesBrowserPage.css`, loading through `usePageData` on a fixed key so back-navigation restores the list without refetching.
- [x] 7.2 Add the page header per `page-header-design`: an `<h1>` in a page-specific wrapper with the margin zeroed, sized to share its row with the controls, and the sort `<select>` in a filter/sort cluster on the shared control height.
- [x] 7.3 Read the sort from `?sort=` with `myScore` as the default when absent or unrecognised, and write it back through `setSearchParams`, mirroring `SeasonPage`'s handling.
- [x] 7.4 Implement the seven comparators — alphabetical, MAL average, my average, status, newest, oldest, my progress — as a pure `sortSeries(items, sort)` module function: display-title key for alphabetical (via `pickDisplayTitle`), unrankable items after rankable ones, "nothing aired" after every real ratio under my progress, and display title ascending as every comparator's final tie-break (design D8).
- [x] 7.5 Render the sorted list into the fluid grid, revealing it incrementally through an `IntersectionObserver` sentinel over the already-loaded array (no network paging), matching `SeasonPage`'s sentinel pattern and page size.
- [x] 7.6 Hold the revealed-count in `useRestorableState`, not `useState`, so a deep-scrolled restore has the cards to scroll back to (design D9, `page-state-restoration` delta).
- [x] 7.7 Implement the three terminal states in order: the grid whenever there is anything to show; the "series are still being discovered from your list" empty state linking to Settings' build action, worded as the profile's Top series section words it; and a distinct load-failed message. Show the loading indicator instead of the empty message while loading.

## 8. Route and navbar

- [x] 8.1 Add `<Route path="/series" element={<SeriesBrowserPage />} />` to `AppShell.tsx` above the existing `/series/:animeId` route, and confirm the detail route still resolves for a member id.
- [x] 8.2 Add the `Series` link to `Navbar.tsx`'s left group between `My List` and `Recap`, restructuring the `NAV_LINKS_*` constants as needed so the ordering reads from the source rather than from where the literals happen to sit.
- [x] 8.3 Verify the navbar's current-page border marks Series on `/series` and does **not** mark it on `/series/:animeId` — `NavLink`'s default prefix matching would mark both, so pass `end` (spec scenario "The Series link marks itself current").

## 9. Verification

- [x] 9.1 `dotnet test` — all backend tests green, including the untouched status, ranking-lookup, and Top series suites that the D2/D5 refactors move under.
- [x] 9.2 `npm run build` and `npm run lint` in `frontend/` clean (use nvm's Node v22, not the default v16).
- [x] 9.3 Manual pass against the spec's scenarios: every sort reorders the whole list including cards not yet revealed; the hide-scores toggle blurs card averages and one card's reveal does not affect its neighbours; a card's badge and averages match the series page opened from it; deep-scroll → open a series → back restores list, sort, revealed count, and scroll position.
- [x] 9.4 Confirm on a store with partial series that opening the page issues no MAL request (check the backend log for fetch activity), the guarantee spec'd in "The Series page lists every series with a member in my list".
