## 1. Backend: the `Airing` status value

- [x] 1.1 `Services/Series/SeriesService.cs`: change `ComputeStatus` to take the main-line members alongside every member, and add the first precedence arm from design D1 — a main-line member with `AiringStatus == "currently_airing"` returns `"Airing"`. Leave the four existing arms byte-identical below it, so only a main-line-airing series changes value.
- [x] 1.2 Update the call site (`SeriesService.cs:156`) to pass the `mainLineMembers` list it already computed at line 133 — as the member anime, matching whatever shape `ComputeStatus` takes — alongside `allAnime`.
- [x] 1.3 `Services/Series/SeriesDto.cs`: extend the `Status` field's doc comment (line ~93) to name all four values and state that `Airing` is main-line-only while `Ongoing` now covers "an extra is airing, or something is announced".

## 2. Frontend: the `Airing` pill

- [x] 2.1 `api/types.ts`: add `'Airing'` to the `SeriesStatus` union (line 565).
- [x] 2.2 `pages/SeriesPage.tsx`: add `Airing: 'airing'` to `SERIES_STATUS_CLASS` (line 27) — the `Record<SeriesStatus, string>` type makes this a compile error until it is done, which is the intent.
- [x] 2.3 `pages/SeriesPage.tsx`: rename `isOngoing` (line 442) to `isRunning` and derive it as `series.status === 'Airing' || series.status === 'Ongoing'` (design D3). Update both uses — the `AiringProgressBar`/`ProgressBar` choice and the `showAired` prop — so an `Airing` series keeps the broadcast bar.
- [x] 2.4 `pages/SeriesPage.css`: add `.series-page__status-pill--airing` using the file's existing `--airing` alias (declared at line 11 as `var(--status-watching)`), and move `.series-page__status-pill--ongoing` (line 122) onto the `--status-onhold*` triple, per design D2's four-colour table. Comment why `Ongoing` changed colour: green now means "on air".
- [x] 2.5 Read `components/SeriesTimeline.tsx` and `components/SeriesEntryRow.tsx` for any other consumer of `SeriesStatus` beyond the two found in the grep (`SERIES_STATUS_CLASS` and `isOngoing`) and update it if one exists.

## 3. Recap page: the season button's colour

- [x] 3.1 `pages/RecapPage.tsx`: add `family--season` to the `<Link>` in `renderSeasonPageLink` (line ~375) so the `--fam-*` vocabulary resolves to the season family.
- [x] 3.2 `pages/RecapPage.css`: replace `.recap-page__season-button:hover`'s `--accent-bg`/`--accent-border` (lines 166-170) with the same three declarations `.recap-page__tab:hover` uses — `background: var(--fam-bg); color: var(--text-h); border-color: var(--fam-border)` — and change `:focus-visible` (line 172) to `outline: 2px solid var(--fam-from)`, matching `.recap-page__tab:focus-visible`. Leave the resting state neutral.

## 4. Recap page: holding the scroll on a period change

- [x] 4.1 `pages/RecapPage.tsx`: pass `{ keepScroll: true }` to every `updateParams` call inside `renderPeriodControls` — the multi-year `from`/`to` selects, the yearly year select and its two arrows, and the season mode's season select, year select, and two arrows (design D5).
- [x] 4.2 `pages/RecapPage.tsx`: pass `{ keepScroll: true }` to the dynamic-filter fall-back's `updateParams` call inside the effect at line ~220, with a comment saying it fires only as a correction to a period or mode change the user just made, never to a filter the user chose.
- [x] 4.3 Confirm by reading that `switchMode` and the two `renderFilterToggle` buttons still call `updateParams` with no options, so the recap-type tabs and the manual time filter keep returning to the top.
- [x] 4.4 Update the comment above `updateParams` (line 198), which currently states that only the ranking-basis toggle and the media-type select opt in.

## 5. Anime detail page: the whole picture

- [x] 5.1 `pages/AnimeDetailPage.css`: change `.anime-detail-page__picture` (line 79) to `width: 260px; height: auto; aspect-ratio: auto 260 / 368` and drop `object-fit: cover`. Comment why the `auto` keyword matters — the natural ratio wins once the image has loaded, and `260 / 368` reserves the portrait box until then (design D6).
- [x] 5.2 `pages/AnimeDetailPage.css`: give `.anime-detail-page__picture--placeholder` (line 87) an explicit `height: 368px`, since it has no image to take a ratio from, and an `aspect-ratio: auto` reset if the base rule's ratio would otherwise apply to it.
- [x] 5.3 Check `.anime-detail-page__picture--landscape` (line 94) and its `max-width: 1024px` override (line 243) still do what they did: they set width only, so the base rule's `height: auto` and natural ratio now carry the height they used to set explicitly. Simplify only what is provably redundant.
- [x] 5.4 Leave `hooks/useLandscapePicture.ts` and its use in `pages/AnimeDetailPage.tsx` untouched — the landscape modifier still exists to widen the column, which the natural ratio cannot decide.

## 6. Backend: the search fallback

- [x] 6.1 `Data/Repositories/IAnimeMetadataRepository.cs` / `AnimeMetadataRepository.cs`: add a fallback projection alongside `GetSearchIndexAsync` — id, title, English title, picture, popularity rank, plus `MediaType`, `TotalEpisodes`, and `MalScore`, which a results card renders and `AnimeTitleProjection` does not carry (design D7).
- [x] 6.2 `Services/Search/AnimeSearchService.cs`: change `SearchMalAsync` to return `(List<MalAnimeListEdge> Edges, bool Failed)`, setting `Failed` only in the `catch`. Update `SearchAsync` to destructure and ignore `Failed` — the type-ahead's behaviour must not change.
- [x] 6.3 `Services/Search/AnimeSearchService.cs`: in `SearchPageAsync`, when `Failed` is true, build `SearchPageCandidate`s from the fallback projection instead of MAL's edges, filtered by the same `SearchTextMatch` predicates the method already uses (`EqualsIgnoreCase` for a quoted query, `ContainsIgnoreCase` on title or English title otherwise).
- [x] 6.4 Assign `RelevanceIndex` for fallback candidates by the local ranking of design D7 — exact title matches, then prefix matches (`StartsWithIgnoreCase`), then the rest, each band ordered by `SearchTextMatch.PopularityKey` then title, case-insensitively — so the `"relevance"` sort branch below needs no special case.
- [x] 6.5 Truncate the fallback candidate list to the same `MaxResults` (100) cap before it reaches the sort, so a one-letter query cannot rank the whole cache.
- [x] 6.6 Confirm by reading that everything downstream of candidate construction — the exact-match filter, `totalCount`, the `myScores` lookup, the four non-relevance sorts, `Skip`/`Take`, and the `AnimeBrowseItemDto` projection — is shared by both paths rather than duplicated.
- [x] 6.7 `Services/Search/SearchPageDto.cs`: add `bool MalSearchFailed` and document it as "the live MAL search failed and these results came from local storage". Return it from both paths in `SearchPageAsync`, including the empty-query early return (false).
- [x] 6.8 Confirm `ScheduleSeriesBuildForTopMatch` and the series-index match still run on the fallback path, so a fallback page still leads with matched series under the relevance sort.

## 7. Frontend: the search fallback notice

- [x] 7.1 `api/types.ts`: add `malSearchFailed: boolean` to `SearchPageDto` (line 219) and to `SearchReadState` in `pages/SearchPage.tsx`, carrying it through the `getSearchPage(...).then(...)` mapping.
- [x] 7.2 `pages/SearchPage.tsx`: render a notice above the grid whenever `malSearchFailed` is true, saying the MAL search could not be reached and that these are the app's locally stored matches. It must render alongside results **and** in place of the "No anime found" empty state, so an empty fallback still explains itself.
- [x] 7.3 `pages/SearchPage.css`: style the notice as an inline notice within the page's flow, not as the fixed bar `ConnectionStatusNotice` uses — the backend is reachable here, one upstream service is not.
- [x] 7.4 Confirm the rest of the page is untouched: the type filter, the count line, the chunked reveal, and the series cards all read the same fields they do today.

## 8. Backend: rewatch-time arithmetic

- [x] 8.1 `Services/Watching/WatchMath.cs`: add `RewatchOnlyEpisodes(entry) = entry.RewatchCount * (entry.Anime.TotalEpisodes ?? entry.EpisodesWatched)` and re-express `RewatchInclusiveEpisodes` as `entry.EpisodesWatched + RewatchOnlyEpisodes(entry)` (design D8). Move the existing doc comment's explanation of the published-total baseline onto the new method, since that is where it now lives.
- [x] 8.2 Confirm by reading `ProfileService.BuildStats` and the recap's own callers that `RewatchInclusiveEpisodes` still returns exactly what it did, so the "Days" and "Episodes" stats are unchanged.

## 9. Backend: the rewatched-series section

- [x] 9.1 `Services/Series/SeriesRankingLookup.cs`: widen `SeriesRankingMemberProjection` with `RewatchCount`, `EpisodesWatched`, `TotalEpisodes`, and `AverageEpisodeDurationSeconds`, selected from the same join (null-guarded off `member.Anime.UserEntry` the way `MyScore` and `EntryStatus` already are).
- [x] 9.2 `Services/Series/SeriesRankingIndex.cs`: add a `RewatchedSeries()` reading beside `EligibleSeries()` — group members by series; per member compute `RewatchCount * (TotalEpisodes ?? EpisodesWatched) * (AverageEpisodeDurationSeconds ?? 24*60)`; sum over **every** member, main line and extras; keep series whose sum is above zero; order by sum descending then title, case-insensitively (design D9). Note in a comment that the Top series coverage rule deliberately does not apply here.
- [x] 9.3 Keep the per-member expression in step with `WatchMath`: use `WatchMath`'s constants/fallbacks rather than restating 24 minutes, or add a small shared helper if the projection's shape makes calling `WatchMath` directly awkward. The two must not be able to disagree.
- [x] 9.4 `Services/Profile/ProfileDto.cs`: add `RewatchedSeriesItemDto(int SeriesId, int RootAnimeId, string Title, string? EnglishTitle, string? PictureUrl, long RewatchSeconds)` and `RewatchedSeriesSectionDto(List<RewatchedSeriesItemDto> Items)`.
- [x] 9.5 `Services/Profile/IProfileService.cs` + `ProfileService.cs`: add `GetRewatchedSeriesSectionAsync`, loading `seriesRankingLookup` and projecting `RewatchedSeries()`. Do **not** call `ScheduleMissingSeriesBuildsAsync` — document that the same page's Top series read already does (design D10).
- [x] 9.6 `Controllers/ProfileController.cs`: add `GET /api/profile/rewatched-series`, with a doc comment matching the `top-series` one's explanation of why it is not embedded in `GET /api/profile`.

## 10. Frontend: the Series scope on Most rewatched

- [x] 10.1 `utils/anime.ts`: add `formatRewatchTime(totalSeconds: number): string` implementing design D11 — sub-hour to two decimals, otherwise round to a tenth of an hour before splitting days off, trailing zeros trimmed, `<0.01h` for a positive total that would render `0h`. Comment why it is separate from `formatRuntime` and why the rounding happens before the day split.
- [x] 10.2 `api/types.ts`: add `RewatchedSeriesItemDto` and `RewatchedSeriesSectionDto` mirroring the backend records.
- [x] 10.3 `api/client.ts`: add `getRewatchedSeriesSection()` hitting `/api/profile/rewatched-series`, with a comment matching `getTopSeriesSection`'s.
- [x] 10.4 `pages/ProfilePage.tsx`: widen the "Most rewatched" scope state from `TopAnimeMediaType` to a `RewatchedScope = TopAnimeMediaType | 'series'` and append `{ value: 'series', label: 'Series' }` to the tab list this section renders — as its own list, so `MEDIA_TYPE_TABS` keeps serving My top anime unchanged.
- [x] 10.5 `pages/ProfilePage.tsx`: add a second `usePageData` keyed `rewatched-series`, fetched only under the Series scope, with its own "hold the last section on screen" ref mirroring `rewatchedDisplayRef` so switching into and out of Series does not collapse the strip.
- [x] 10.6 `pages/ProfilePage.tsx`: render the Series scope's strip — same `rewatched-strip` classes and `rewatchedStripScroll` handlers, tiles linking to `/series/{rootAnimeId}`, badge showing `formatRewatchTime(item.rewatchSeconds)`. Key the strip-scroll restore on the scope value so Series keeps its own offset.
- [x] 10.7 `pages/ProfilePage.tsx`: extend `REWATCHED_EMPTY_MESSAGES` with `series: 'No series have been rewatched'`.
- [x] 10.8 `pages/ProfilePage.css`: check whether the time badge needs any change from `.rewatched-strip__count` — it carries several characters rather than one or two digits — and adjust only if it overflows the tile, keeping the shape, position, and white/silver colour the spec fixes.

## 11. Backend tests

- [x] 11.1 `Services/Series/`: cover `ComputeStatus`'s new arm — a main-line entry currently airing reads `Airing`; only an extra airing reads `Ongoing`; a main-line entry airing alongside an announced sequel reads `Airing`; a main-line entry airing with nothing finished reads `Airing` rather than `Upcoming`; and the `Upcoming`/`Finished`/announced-sequel-`Ongoing` cases are unchanged.
- [x] 11.2 `Services/Search/AnimeSearchServiceTests.cs`: cover the fallback — a failing MAL search returns locally stored matches with `MalSearchFailed` true; a succeeding one is unchanged with the flag false; a failing search with no local match returns empty with the flag true; a quoted query under fallback matches exactly; local relevance ranks exact, then prefix, then substring, each by popularity; the non-relevance sorts order the local set correctly; and series still ride along under relevance.
- [x] 11.3 `Services/Watching/` (or the existing `WatchMath` test home): cover `RewatchOnlyEpisodes` — zero rewatches is zero; the published total is the baseline regardless of current progress; `EpisodesWatched` is the baseline when no total is published — and assert `RewatchInclusiveEpisodes` still returns what it did.
- [x] 11.4 `Services/Profile/`: add a rewatched-series test file covering the sum across main line and extras, exclusion of first watches, the no-published-total and no-published-duration fallbacks, the "listed when above zero" rule, ordering by total then title, and that a franchise with one rewatched entry out of three aired main-line entries is listed (unlike in Top series).
- [x] 11.5 `Services/Series/SeriesRankingLookupTests.cs`: assert the four new projection columns are populated, including nulls for a member with no user entry.
- [x] 11.6 `Services/Profile/ProfileServiceTopSeriesTests.cs` and `ProfileServiceTopSeriesBackfillTests.cs`: confirm they still pass with the widened projection, and that the rewatched-series read enqueues no background builds.

## 12. Verification

- [x] 12.1 Build the backend and run the test suite. The local SDK is 9.0, so compile via the `sdk:10.0` Docker image as the project's notes describe.
- [x] 12.2 Build the frontend with Node 22 via nvm (the default `node` is v16 and will not run the Vite build), and fix any type errors the `SeriesStatus` and `SearchPageDto` widenings surface.
- [x] 12.3 Walk the six behaviours in the running app: a series with an airing main-line season reads `Airing` in its own colour; the season recap's "Browse the season" button highlights in the season colour on hover; stepping a recap's year or season leaves the scroll where it was; an anime with a tall poster (Link Click season 3) shows the whole image; the search page falls back with its notice when MAL search is unreachable; and Most rewatched's Series scope ranks franchises with `3d 7.7h`-style badges that open series pages.
- [x] 12.4 Run `openspec validate polish-airing-recap-and-rewatch-series --strict` and confirm it still passes.
