## Why

Four pages each carry a small friction that has accumulated as they grew: the home page orders "Currently watching" alphabetically (so the show I am about to finish sits wherever the alphabet drops it) and forgets its "Followed shows airing" sort the moment I open an anime and come back; the profile's "Most rewatched" strip breaks rewatch-count ties by score, which scatters equally-rewatched shows unpredictably; the series page states "Finished" for a franchise with an announced sequel, pads its score chips with a scored-count that nobody reads, shows a "Time left" of zero on series I have finished, and reports entries-completed for the main line only; and the anime detail page renders its `<h1>` in an ad-hoc position that the seven other pages moved away from when they gained dedicated header blocks.

None of these is a defect in isolation, but together they are the difference between pages that feel finished and pages that feel assembled.

## What Changes

**Home page**

- Order "Currently watching" by episodes watched descending, then alphabetically by display title — the shows I have invested the most in lead the carousel, instead of whatever the alphabet puts first.
- Make the "Followed shows airing" sort selection survive back/forward navigation, like every other view control in the app. It is currently plain component state, so it silently resets to Popularity on every return.

**Profile page**

- Order "Most rewatched" by rewatch count descending, then alphabetically by title. The intermediate my-score tie-break is removed, so equally-rewatched entries read in a predictable order.

**Series page**

- Replace the `Finished · sequel upcoming` status pill with `Ongoing`. A franchise with an announced-but-unaired member is not finished; `Finished` is reserved for a series with nothing left to come.
- Remove the `· N of M scored` count from the MAL and Mine score chips, leaving the average itself.
- Hide the "Time left" and "Time watched" stats entirely when there is no time left to watch, rather than reporting `0min` remaining on a series I have finished.
- Report "Entries completed" for the main line **and** extras as two labelled figures in one stat, instead of the main line alone. The extras figure is omitted for a series with no extras.

**Anime detail page**

- Move the title into the same dedicated page header block, giving it the spacing the other pages have rather than sitting flush against the top of the page.

## Capabilities

### New Capabilities

None. Every change modifies behaviour that an existing capability already specifies.

### Modified Capabilities

- `main-dashboard`: the ordering of the "Currently watching" carousel changes from alphabetical to episodes-watched-descending-then-alphabetical; the "Followed shows airing" sort control is brought under page-state restoration.
- `profile-stats`: the "Most rewatched" strip's tie-break drops my-score and breaks ties alphabetically by title.
- `series-page`: the status-pill vocabulary loses `Finished · sequel upcoming`; the score averages drop their scored-count suffix; the entries-completed stat covers extras; time watched/left are conditional on there being time left.
- `anime-detail`: the page's title moves into a dedicated header block.
- `page-header-design`: the dedicated-header-block requirement extends from seven pages to eight, adding the anime detail page.

## Impact

**Frontend** (`frontend/src`)

- `components/CurrentSeasonSection.tsx` — swap `useState` for `useRestorableState` on the sort key.
- `pages/SeriesPage.tsx` / `.css` — status-pill map and class lookup, `MalScoreChip`/`MineScoreChip`/`formatAverage`, the stats grid's entries-completed and time cells.
- `pages/AnimeDetailPage.tsx` / `.css` — title placement and header wrapper.

**Backend** (`backend/AnimeTracker.Api`)

- `Services/Dashboard/MainDashboardService.cs` — currently-watching ordering.
- `Services/Profile/ProfileService.cs` — `BuildRewatchedSection` ordering.
- `Services/Series/SeriesService.cs` — `ComputeStatus` return values; `BuildStats` gains an extras-completed count.
- `Services/Series/SeriesDto.cs` — `SeriesStatsDto` gains `ExtrasCompleted`.
- `frontend/src/api/types.ts` — mirrors the DTO change and narrows the `SeriesStatus` union.

**Tests**

- Existing series-status tests asserting `"Finished · sequel upcoming"` need updating; new coverage for the two orderings and the extras-completed stat.

**Not affected**: no API route, database schema, or MAL/AniList integration changes. The `SeriesStatus` union narrowing is the only type-level breaking change, and it is internal to this repo.
