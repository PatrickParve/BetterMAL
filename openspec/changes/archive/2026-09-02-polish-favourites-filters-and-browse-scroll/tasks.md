## 1. Season and Year stop paging, sorting, and filtering on the server (design D1, D3)

- [x] 1.1 In `Data/Repositories/SeasonRepository.cs`, change both `GetPageAsync` overloads into a listing read: drop `offset`, `limit`, `sort`, `includeMyList`, and `types`, keep `hideHentai`, and delete the closing `.Skip(offset).Take(limit)`. Keep the `AsNoTracking` projection (including `AiringStatus` and `Status`, which the my-score ordering needs) exactly as it is. Rename to `GetListingAsync` and update `ISeasonRepository`'s doc comments, which describe paging.
- [x] 1.2 Keep all four `OrderBy` chains verbatim — the popularity/MAL-score/alphabetical ones and the `myScore` join against `TopAnimeSelections` with its band expression — and run each as an id-only projection (`.Select(x => x.Id).ToListAsync()`). Comment that they are deliberately left in SQL: they carry the `anime-ranking` banding and Postgres's own title collation, which is why the ordering is shipped as a key rather than reimplemented client-side (design D3).
- [x] 1.3 Build one `animeId → position` map per sort from those four id lists and attach the four indices to each item. Note that the indices are positions in a total order over the whole listing, so client-side filtering preserves relative order.
- [x] 1.4 Add `BrowseSortOrderDto(int Popularity, int MalScore, int Alphabetical, int MyScore)` beside `AnimeBrowseItemDto`, and add `SortOrder` to `AnimeBrowseItemDto` as an **optional trailing** field defaulting to null — `AnimeSearchService` shares this record and must stay untouched. Document that only the season and year listing reads populate it.
- [x] 1.5 In `Services/Season/SeasonBrowseService.cs`, drop `sort`, `includeMyList`, `types`, `offset`, and `limit` from `GetPageAsync`/`GetYearPageAsync` and from `ISeasonBrowseService`; pass `SortOrder` through when mapping to `AnimeBrowseItemDto`.
- [x] 1.6 In `SeasonPageDto`/`YearPageDto`, remove `Offset` and `Limit`. Keep `TotalCount` as the count of the whole listing; the page derives its own filtered count.
- [x] 1.7 In `SeasonController.GetPage` and `YearController.GetPage`, remove the `offset`, `limit`, `sort`, `includeMyList`, and `type` query parameters and the `Math.Clamp(limit, 1, 100)` call, keeping `hideHentai`. Rewrite both doc comments — they open "One page of…" and must now say what the endpoint returns and why: a season holds a few hundred anime and a year a few thousand at most, so one read serves the page for as long as it is open.
- [x] 1.8 Confirm `hideHentai` stays server-side, and that nothing else calls either endpoint or either service method — `SeasonPage.tsx` and `YearPage.tsx` are the only clients.
- [x] 1.9 In `frontend/src/api/client.ts`, reduce `getSeasonPage`/`getYearPage` to `(year[, season], { hideHentai })`; in `api/types.ts`, add `sortOrder: BrowseSortOrderDto | null` to `AnimeBrowseItemDto` and drop `offset`/`limit` from `SeasonPageDto`/`YearPageDto`.

## 2. Season and Year reveal, sort, and filter client-side (design D2, D3)

- [x] 2.1 In `SeasonPage.tsx`, key `usePageData` on `season:{year}/{season}:{hideHentai}` and call the reduced `getSeasonPage` — the season and the NSFW setting are now the only things a read depends on.
- [x] 2.2 Derive the displayed array in one memo: filter by `inMyList` and by `mediaType` (`item.mediaType ?? 'unknown'`, matching the existing convention), then sort by `a.sortOrder[sort] - b.sortOrder[sort]`. Comment that the comparator is an integer compare on a server-computed key, and that no ordering rule may be reimplemented here (design D3).
- [x] 2.3 Add `const [visibleCount, setVisibleCount] = useRestorableState('visibleCount', PAGE_SIZE)`, render `displayed.slice(0, visibleCount)`, and change the `IntersectionObserver` sentinel to `setVisibleCount((prev) => Math.min(prev + PAGE_SIZE, displayed.length))` instead of fetching. Comment that the reveal is load-bearing rather than decorative: the whole listing is loaded, and this is what keeps a 1,200-card year from rendering in one paint.
- [x] 2.4 Delete what only paging needed: `loadingMore`/`setLoadingMore`, the `hasMore`-from-`totalCount` derivation (now `visibleCount < displayed.length`), the `reload()`-on-sort/filter effect and its `isFirstFilterRun` guard, the `start()` invalidation effect, `itemsLengthRef`, `sortRef`, `inMyListRef`, and `typeFilterRef`.
- [x] 2.5 Delete `seenTypesRef` and derive the type options straight from the whole loaded listing — the accumulation workaround existed only because server-side filtering hid the unselected types, and with the filter client-side a selected type can no longer shrink the picker.
- [x] 2.6 Re-derive `terminalState` from the loaded listing and the filtered set: grid when the filtered set is non-empty; `filtersEmpty` when the listing has anime but the filters leave none; `notListed`/`loading`/`loadFailed` unchanged from `lastFetchedAt`, `hasListing`, and `refreshOutcome`.
- [x] 2.7 In the debounced MAL-refresh effect, re-read with the same single listing call; keep the `refreshOutcome` handling and the `hideHentaiRef` if the effect still needs it.
- [x] 2.8 Confirm `visibleCount` resets on a sort or filter change without any code doing it explicitly: those go through `setSearchParams`, which mints a new history entry, and `useRestorableState` reseeds to its initial value under a new location key. Note this where the reset would otherwise look missing.
- [x] 2.9 Keep `firstUnwatchedIndex` (the `Unwatched` divider) computed over the *displayed* array under the my-score sort.
- [x] 2.10 Apply 2.1-2.9 to `YearPage.tsx` identically, keeping the two pages' effect structure in step as their comments require.
- [x] 2.11 Confirm `usePageData` and `useScrollRestoration` need no change at all, and that the restore path now satisfies `page-state-restoration`'s existing revealed-count requirement the same way Search and the Series browser do.
- [x] 2.12 Check whether `useLatestRequest` still has a user on either page now that only the refresh re-read races; remove it if not.

## 3. The carousel's hover residue in Safari (design D4)

- [x] 3.1 In `components/CurrentlyWatchingCarousel.css`, add `transform: translateZ(0)` to `.carousel__track` with a comment naming the cause: `.anime-card::before` sits at `inset: -6px`, Safari's repaint rect for the hover state change excludes that overhang, and promoting the scrollport invalidates it as a unit. Say why the promotion is on the track and not on the pseudo-element (one layer per carousel vs. one per card across the Season/Year grids).
- [x] 3.2 Verify in Safari: hover the row's first card, move the pointer away without scrolling or hovering anything else, and confirm nothing is left under it; repeat at the row's right edge with the track scrolled fully right.
- [x] 3.3 If the artefact survives 3.1, apply the fallback instead — `backface-visibility: hidden` on `.anime-card::before` scoped to `.carousel__card` — and update the comment to record which treatment was needed.
- [x] 3.4 Confirm the row's text and posters still rasterise cleanly in Safari and that the arrows, scroll bounds, and 5-card budget are unchanged.

## 4. The ranking DTOs carry their histogram (design D5)

- [x] 4.1 In `Services/Recap/RecapDto.cs`, add `IReadOnlyList<int> ScoreCounts` to `RecapSeasonRankingDto` and `RecapYearRankingDto`, documented as ten counts in ascending score order (index 0 is score 1, index 9 is score 10) and as the same histogram the ranking's own tie-break reads.
- [x] 4.2 In `RecapRankingBuilder`, pass `c.Histogram[1..]` into both DTO constructions — the histogram is already built per group for `CompareGroups`; nothing new is computed.
- [x] 4.3 Confirm `RecapService` and `ProfileService` need no change beyond the DTOs flowing through them.
- [x] 4.4 In `frontend/src/api/types.ts`, add `scoreCounts: number[]` to both DTO types with the same ascending-order note.

## 5. Favourites score filter — data and ordering (design D6, D7)

- [x] 5.1 In `components/RankingSection.tsx`, add `scoreCountAt(row, score)` reading `scoreCounts[score - 1]`, so no caller open-codes the offset. Export it alongside the `describe*` functions.
- [x] 5.2 In `ProfilePage.tsx`, add `offeredScores(rows)` returning the scores 10→1 that at least one row holds one of, and `rankByScoreCount(rows, score)` = filter to `scoreCountAt > 0`, then `.sort((a, b) => scoreCountAt(b, score) - scoreCountAt(a, score))`. Comment that the sort is deliberately stable over an already-fully-ranked array, so ties fall back to the backend's own `CompareGroups` order without a second copy of that chain on the client — and what breaks if the sort is ever replaced by a hand-rolled comparator.
- [x] 5.3 Give `describeSeasonRanking`/`describeYearRanking` an optional `selectedScore` argument: with one, `meta` becomes `${scoreCountAt(row, score)} × ${score} · ${row.scoredCount} scored`; without one it is unchanged. Both RecapPage callers pass nothing and are unaffected.
- [x] 5.4 Hold the two selections in `ProfilePage` as `useRestorableState<number | null>('favouriteYearsScore', null)` and `('favouriteSeasonsScore', null)`, so each ranking restores its own selection and a fresh visit opens on All.
- [x] 5.5 Fall back to All when a restored selection names a score the reloaded ranking no longer offers — derive the effective selection as `selected !== null && offered.includes(selected) ? selected : null` rather than trusting the stored value.

## 6. Favourites score filter — control and colour (design D7, D8)

- [x] 6.1 In `RankingSection.tsx`, add an optional `scoreFilter?: { scores: number[]; selected: number | null; onSelect: (score: number | null) => void }` prop rendering a row between the title and the rows: an **All** button, the text **With most:**, then one button per score in `scores` order. Rendered only when the prop is given, so the recap page's four rankings are untouched.
- [x] 6.2 Give each button `aria-pressed`, and label the group so the control reads as a set of choices rather than eleven unrelated buttons.
- [x] 6.3 In `RankingSection.css`, style `.ranking-score-filter__button` on the `.profile-media-tabs__tab` treatment — neutral at rest, `--fam-bg`/`--fam-border` on hover, tinted with a `--fam-from` label when selected, `outline: 2px solid var(--fam-from)` on focus-visible, no size change between states — and note that it deliberately mirrors that control.
- [x] 6.4 Add per-tier classes that re-point `--fam-from/--fam-to/--fam-bg/--fam-border` at the tier tokens (`--tier-apex`, `--tier-red`, `--tier-blue`, `--medal-gold`, `--medal-silver`, `--medal-bronze` and their `-bg`/`-border` pairs), mirroring `ScoreDistribution.css`'s own `--tier` aliasing so the two can only ever agree.
- [x] 6.5 Pick each button's tier class from `scoreTier(score)` in `utils/anime.ts` — never a second 10→1 mapping. Leave **All** without a tier class so it inherits the section's `family--year`/`family--season`.
- [x] 6.6 Confirm the 10 button takes the apex hue flat rather than the distribution's sheen gradient, and that 6/5 share silver and 4-1 share bronze exactly as the distribution's rows do.
- [x] 6.7 Let the row wrap (`flex-wrap`) so a narrow favourites column reflows rather than overflows.

## 7. Favourites score filter — wiring (design D6, D7)

- [x] 7.1 In `ProfilePage.tsx`, compute each ranking's offered scores and displayed rows from its effective selection, and pass both the rows and the `scoreFilter` prop into its `RankingSection`.
- [x] 7.2 Keep the five-row cap and the "See all" control operating on the filtered rows — `RankingSection` already slices and counts what it is given, so no change is needed beyond passing the right array.
- [x] 7.3 Append the selection to the overlay title when one is active (e.g. `Favourite years — with most 10s`), so the overlay names what it is showing.
- [x] 7.4 Confirm the empty-state branch (`favouriteSeasons.length === 0 && favouriteYears.length === 0`) is unchanged: it tests the loaded rankings, not the filtered ones.

## 8. Profile episode-progress figure (design D9)

- [x] 8.1 Wrap the profile page's `<ProgressBar>` in a `div.profile-page__episode-progress` in `ProfilePage.tsx`.
- [x] 8.2 In `ProfilePage.css`, raise `.profile-page__episode-progress .progress-bar__label` to 18px, with a comment saying the override is scoped here because this is the app's only whole-list bar and `ProgressBar` is shared with the dashboard, my list, and the detail page.
- [x] 8.3 Confirm the track still spans the section, the unresolved-entries note is unmoved, and no other `ProgressBar` in the app changes size.

## 9. Recap control row and top 6-10 rows (design D10, D11)

- [x] 9.1 In `RecapPage.tsx`, wrap `renderFilterToggle()`, `renderSeasonPageLink()`, and `renderYearPageLink()` in one `<div className="recap-page__controls-trailing">` inside `.recap-page__controls`.
- [x] 9.2 In `RecapPage.css`, give `.recap-page__controls-trailing` `display: flex; align-items: center; gap: 16px; flex-wrap: wrap`, and comment that the wrapper exists so `space-between` sees two children in every mode — which is what moves the yearly recap's time filter beside **Browse the year**.
- [x] 9.3 Confirm the season and multi-year modes render identically to before (each has a single trailing control, so the group collapses to it).
- [x] 9.4 In `RecapPage.css`, add `padding-inline-end` to `.recap-top-ten-row__score` — not to the row, whose padding also places the rank and poster — so only the score moves in from the border.

## 10. Docs

- [x] 10.1 Update `CODE_GUIDE.md` where it describes the season/year read path and the profile page's favourites sections: the two endpoints now take only `hideHentai` and return the whole listing with a per-item sort key; the pages reveal, sort, and filter it client-side; and the favourites rankings carry a score filter.

## 11. Tests

- [x] 11.1 `Services/Season` (or wherever `SeasonRepository`/`SeasonBrowseService` are covered today): a season read returns the whole listing rather than a first page; `hideHentai` still excludes only `rx`; and the four sort keys order the listing exactly as the four sorts ordered it before — assert this against the pre-change ordering for each sort over one fixture, `myScore` included, so the move to keys cannot silently reorder anything. Update or delete any existing test that asserts paging, `sort`, `includeMyList`, or `type` behaviour at this layer.
- [x] 11.2 `Services/Season`: sort keys are dense positions over the whole listing, and filtering a subset out of the items preserves their relative order under every sort — the property the client's filtering relies on.
- [x] 11.3 `Services/Recap/RecapRankingBuilderTests`: a season's and a year's `ScoreCounts` hold ten ascending counts matching the group's scored entries; a score nobody gave is 0; the counts agree with the histogram the tie-break uses (two groups whose ranking order is decided by the 10 count report those same 10 counts).
- [x] 11.4 `Services/Profile`: the profile's favourite seasons and years carry the same `ScoreCounts` a recap covering the same period reports, so the two surfaces cannot disagree.

## 12. Verify

- [x] 12.1 Build and run the backend test suite in the `sdk:10.0` Docker image (the local SDK is 9.0).
- [x] 12.2 Build the frontend with Node 22 via nvm (the default `node` is v16) and typecheck; run `npm run lint`.
- [x] 12.3 Check the largest year (2023, ~1,230 anime) on a cold load: the first paint is no slower than it was under paging, and the five queries the read now runs are not a visible cost. Re-measure if either is.
- [x] 12.4 Season/Year, with the network panel open: scroll a season to its last card and confirm nothing stops short of the season's own size and that no read is issued; open a card from deep in the grid, go back, and confirm the same number of cards is shown and the page stays where it was restored to, before and after the refresh lands. Repeat on the largest year.
- [x] 12.5 Season/Year controls: each sort re-orders instantly with no read and matches the order that sort produced before the change (check `myScore` against the `Unwatched` divider and a tied score, and alphabetical against a non-ASCII title); the type filter and "In my list" apply instantly; selecting one type leaves the other types in the picker; each of the three returns the grid to the top on its first screenful; changing the NSFW setting still re-reads.
- [x] 12.6 Safari: the carousel hover check from 3.2, plus a pass over the Season grid to confirm no card hover residue was introduced there.
- [x] 12.7 Profile: All is selected on load; picking 10, then 9, then a score only a few groups hold re-ranks and drops the right groups; the counts on each row match; "See all" follows the selection and names it; the buttons wear the distribution's colours; going back from a recap restores the selection; reaching the page from the navbar resets both to All.
- [x] 12.8 Recap: the yearly recap's time filter sits beside **Browse the year**; the season and multi-year rows are unchanged; the top 6-10 scores sit clear of the border.
- [x] 12.9 Run `openspec validate polish-favourites-filters-and-browse-scroll --strict`, then `/opsx:verify` before archiving.
