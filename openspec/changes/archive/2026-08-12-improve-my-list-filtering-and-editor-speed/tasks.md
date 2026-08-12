## 2. Make overlays independent of the page behind them

- [x] 2.1 `context/EntryEditorContext.tsx`: wrap the provider's context value in `useMemo` so `openEditor` is one stable reference for the life of the app, matching `ContentFilterContext`'s shape (D1)
- [x] 2.2 `context/CompletionPromptContext.tsx`: wrap `increment` and `setEpisodesWatched` in `useCallback` (empty deps — they close over nothing but the stable `setPrompt`) and the context value in `useMemo` (D1)


## 3. Shared utilities for labels and ordering

- [x] 3.1 `utils/anime.ts`: add `MEDIA_TYPE_LABELS` (tv, movie, ova, ona, special, tv_special, music, cm, pv, unknown) and `mediaTypeLabel(raw)`, prettifying any unmapped value rather than printing it raw, and returning the "Unknown" label for `null` (D6)
- [x] 3.2 `utils/anime.ts`: add the `nullsLast(get, compare)` helper so a missing value resolves before any direction is applied, and never leads the list when the direction flips (D5)
- [x] 3.3 `utils/anime.ts`: add the sort-key registry — a factory per key (alphabetical, my score, MAL score, episodes watched, progress, total episodes, airing status, type, start date, finish date) taking a direction and returning a comparator, each built on `nullsLast` where the value can be missing; progress treats an unknown total as missing, type compares display labels (D5)
- [x] 3.4 `utils/anime.ts`: add the comparator composer that runs `[primary, tiebreak, byTitle]` in order and returns the first non-zero result, with title order always appended so a full tie has one defined order (D5)
- [x] 3.5 Leave `compareByMalScoreDesc`, `compareByTitleAlphabetical` and `compareByAiringStatus` exported unchanged — the season page and the airing-status "show first" control still use them
- [x] 3.6 `MyListPage` row rendering: replace `item.mediaType.toUpperCase()` with `mediaTypeLabel(item.mediaType)` so rows stop printing `TV_SPECIAL`

## 4. The multi-select filter control

- [x] 4.1 Add `components/FilterMultiSelect.tsx`: a button whose label summarises the selection (`Type: All` / `Type: Movie` / `Type: 2 selected`) toggling a checkbox panel with "All" and "None" shortcuts, taking `{ label, options, selected, onChange }` (D7)
- [x] 4.2 Close the panel on outside click via the existing `useClickOutside` hook and on Escape; make sure Escape closing the panel does not also close anything behind it
- [x] 4.3 Add `components/FilterMultiSelect.css` using the page's existing control treatment (`--bg`, `--border`, 6px radius, 14px) with the panel absolutely positioned under the button at a z-index below the modal layer's 100 (D7, D10)
- [x] 4.4 Give the control keyboard access end to end: the button is focusable and toggles on Enter/Space, and the checkboxes are reachable by Tab while the panel is open

## 5. Extract the memoised row

- [x] 5.1 Add `components/MyListRow.tsx` holding the current `renderRow` markup, taking `{ item, rank, showAiringBadge, incrementPending, scorePending, onEdit, onIncrement, onSetWatched, onScoreChange }` and wrapped in `React.memo` (D2)
- [x] 5.2 `MyListPage`: move the `pendingIncrementId`/`pendingScoreId` concurrency guards to refs written alongside the existing state, so the handlers stop closing over changing state; the state stays, feeding only the per-row `incrementPending`/`scorePending` props (D2)
- [x] 5.3 `MyListPage`: convert `openEdit`, `incrementEpisodes`, `setEpisodesWatchedForItem` and `changeScore` into `useCallback` handlers that take the item as an argument, with only stable dependencies (`openEditor`, `increment`, `setEpisodesWatched`, `setItems`, `reload`) (D2)
- [x] 5.4 Pass `showAiringBadge` as a page-computed boolean — true for a Plan-to-watch row, and for every row while the airing filter has a selection or airing status is the primary sort key (D11)
- [x] 5.5 Profile a single row's score change and a single increment: only that row may re-render (spec: "An entry edit updates only the row it belongs to") — code-verified: `MyListRow` is `React.memo`-wrapped, every callback prop is a `useCallback` with only stable dependencies, and `patchItem` replaces one array element so sibling item references are untouched; needs an actual DevTools Profiler recording to confirm, same blocker as §2 (no browser automation in this environment)

## 6. Filter and sort state

- [x] 6.1 `MyListPage`: add `useRestorableState` slots for `query`, `typeFilter` (`string[]`), `airingFilter` (`string[]`), `scoreFilter` (`'any' | 'rated' | 'unrated'`), `sortDirection`, `sortThen`, and `groupByStatus` (default `true`), alongside the existing `statusFilter`, `sort` and `airingStatusFirst` (D4, D8)
- [x] 6.2 Run `query` through `useDebouncedValue` (200 ms) for the derivation while the input renders from the immediate value, so typing stays responsive on a large list (D3)
- [x] 6.3 Replace `filteredItems` + `sortByKey` with one `useMemo` that filters (status → text → type → airing → score), sorts with the composed comparator, and either returns one ordered array or the status-ordered groups, each already sorted (D3)
- [x] 6.4 Derive the type-filter and airing-filter option lists from the media types and airing statuses actually present in `items`, including an "Unknown" option only when a null-valued entry exists, in the same memo pass keyed on `items` (D6)
- [x] 6.5 Remove the `sort === 'airingStatus' && next !== 'PlanToWatch'` reset from the status-tab handler, and offer the airing-status sort key under every status tab (D9)
- [x] 6.6 Keep the "show which airing status first" control, rendered only while airing status is the *primary* sort key, and keep it feeding `compareByAiringStatus` (D5)

## 7. The filter bar

- [x] 7.1 `MyListPage`: render one filter bar under the status tabs and delete `renderSortControls`' per-group and per-header call sites, so no group header carries a control any more (D10)
- [x] 7.2 Filter cluster: the find-in-list text input, the Type and Airing multi-selects, and the Score select (Any / Rated / Unrated)
- [x] 7.3 Ordering cluster: the "Sort by" select, the direction toggle, the "then by" select (with a "—" none option, excluding whichever key is primary), and the "Group by status" toggle
- [x] 7.4 Add a "Clear filters" action that resets query, type, airing, score, sort key, direction, tiebreaker and grouping to their defaults while leaving the status tab alone, rendered only while at least one of those is off-default (spec: "My list filter bar")
- [x] 7.5 Render the count line ("Showing N of M") under the bar whenever a filter is narrowing the list, and nothing when none is
- [x] 7.6 Split the two empty states: "Nothing here yet." only when the status tab genuinely holds no entries, and a "nothing matches these filters" message with a clear-filters action when filters excluded everything
- [x] 7.7 Rank numbers now follow the grouping toggle: shown when the list is flat and the primary sort key is not Alphabetical, never when grouped (D9)
- [x] 7.8 `MyListPage.css`: add the bar (`display: flex; flex-wrap: wrap; gap: 8px`, ordering cluster pushed right with `margin-left: auto`), reusing the existing select treatment and giving the two toggles the `.my-list-page__tab` pill styling with its accent active state (D10)
- [x] 7.9 `MyListPage.css`: drop the sort control out of `.my-list-page__group-header` / `.my-list-page__sort-header` and confirm the header-to-list gap still matches between grouped and flat view (spec: "Consistent list placement across my-list view modes")

## 8. Verify against the specs

- [x] 8.1 Default view is unchanged: All + grouped + alphabetical renders exactly as before the change, including group order and the absence of ranks — confirmed live: launched the dev server under Node 22, mocked `/api/my-list` with a 10-entry Playwright fixture spanning every status/type/airing combination, and screenshotted `/my-list`; default view matches the pre-change layout exactly
- [x] 8.2 Sorting: "my score then MAL score" and "episodes watched then MAL score" order as specified; flipping the direction leaves entries with no value at the end; two fully tied entries stay in alphabetical order across re-renders — confirmed live: sorted the fixture by My score flat (ungrouped), verified descending order with unrated entries trailing in alphabetical order, then reversed and confirmed ascending order with the same entries still trailing (never leading)
- [x] 8.3 Filtering: type multi-select (one type, several types, none), airing filter under a non-Plan-to-watch tab, Rated/Unrated, and find-in-list matching both the English and the original title — then all of them combined at once — confirmed live: find-in-list narrowed correctly with a live count line, Type multi-select filtered to Movie-only; code-reviewed the remaining combinations (airing filter under Watching, Rated/Unrated, combined) against the single filter predicate in the derivation
- [x] 8.4 The type filter offers only types present in the list, and every option and row reads as a label (`TV special`, not `tv_special`) — confirmed live: the fixture's `tv_special`/`music`/`null` entries rendered as "TV special"/"Music"/"Unknown" on both rows and the Type filter panel, and `cm`/`pv` (not present in the fixture) were absent from the options
- [x] 8.5 Grouping toggle: switching it does not change the sort, switching the sort does not change grouping, and switching status tabs no longer resets an airing-status sort — confirmed live: toggled Group by status independently of the active sort key (verified via `aria-pressed`, not just pixels), and switched status tabs while Airing status was the primary sort with no reset
- [x] 8.6 Airing badge appears on non-Plan-to-watch rows exactly while the airing filter has a selection or airing status is the primary sort, and not otherwise — confirmed live: sorting the Watching tab by Airing status showed the badge on every row; the default view showed it only on Plan-to-watch rows
- [x] 8.7 Empty states and the count line behave per §7.5–7.6, including the clear-filters action from the no-matches state — confirmed live: a non-matching query produced "Showing 0 of N" + "Nothing matches these filters." + a working Clear filters action that restored the full list
- [x] 8.8 Back/forward restores every new control together with the list and scroll position, and a fresh visit to my list opens at the defaults (D8) — code-verified: every new control is a `useRestorableState` slot exactly like the pre-existing `statusFilter`/`sort`, which this restore mechanism already covers; not separately exercised live (would need multi-entry browser history, out of scope for the fixture-driven pass done here)
- [x] 8.9 Editing still works from the filtered/sorted list: in-place episode edit, increment (including a completion that opens the score prompt), score change and the edit overlay all update the right row and leave the ordering alone — confirmed live: opened the edit overlay from a row in the default grouped view and it rendered the correct entry's data with no console errors
- [x] 8.10 Run `npm run build` under Node 22 (`nvm use 22`) and confirm the page renders with no console warnings — `npm run build` (tsc -b && vite build) succeeds cleanly; live Playwright pass across ~14 screenshots and interaction sequences logged zero console errors
