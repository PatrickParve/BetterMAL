## Why

Series are already derived, stored, and richly presented — but only one at a time. A series page is reachable only by opening one of its members or by searching for it, so there is no way to see the franchises my list is made of side by side: which I've finished, which are still running with episodes I'm behind on, how they compare on score. The profile page's Top series strip is the closest thing, and it is a ranked strip of a single metric, not a browsable page.

## What Changes

- A new **Series** page listing every stored series with at least one member in my list. A series built by exploration whose every member is outside my list is not shown at all.
- A **Series** navbar link between **My List** and **Recap**, opening that page.
- A **series card** in the same fluid grid the Season, Year, and Search pages use, carrying: the series' main picture (its root's), the MAL and my main-line average scores, its status pill (Airing/Ongoing/Upcoming/Finished), my progress badge (Completed/Caught up/N behind, or none), its year span, its main-line episode total, and its entry count. The card links to that series' page.
- MAL averages on the card honour the global hide-scores toggle under the **same reveal rule the series page applies** to its main-series average, so a card can never leak a score the series page itself would blur.
- A **sort control** over the whole list: alphabetical, MAL average, my average, series status, newest, oldest, and my progress. Sorting is over every listed series at once, not just what has been scrolled to. The page opens on my average score.
- A new **series list read endpoint** returning every eligible series with the figures the card needs, computed at read time from stored members (no MAL calls, no series builds).
- The sort selection and scroll position are restored on back-navigation, as on every other browse page.

## Capabilities

### New Capabilities
- `series-browser`: The Series page — which series are listed, what a series card shows, how the card's MAL average obeys the hide-scores rules, the sort orders and their tie-breaks, the list read endpoint and its freshness behaviour, and the page's empty and loading states.

### Modified Capabilities
- `navigation-and-search`: the navbar's left group gains a **Series** link between My List and Recap, changing the documented left-group order and its scenarios.
- `page-state-restoration`: the enumeration of routed pages that participate in restoration gains the Series page, and the requirement gains the rule that a page revealing a loaded list progressively must restore how much of it had been revealed.

## Impact

- **Backend**: new `GET /api/series/list` on `SeriesController`; new list projection service alongside `SeriesRankingLookup`/`SeriesRankingIndex` (the same `SeriesMembers ⋈ AnimeMetadata ⋈ UserEntry` join, extended with the airing/date/episode fields the card needs); batch `IEpisodeScheduleService.EpisodesAiredAsOfAsync` for the behind-count. No schema change, no new migration.
- **Frontend**: new `SeriesBrowserPage` + CSS, a new `/series` route (the existing series page keeps `/series/:animeId`), a `Series` navbar link, a new `getSeriesList` client call and DTO types. Reuses `AnimeCard`, `ScoreChip`, `ScoreValue`, `SeriesBadge`, and `usePageData`.
- **Reused, not duplicated**: series composition, main-line classification, status computation, and the average/reveal arithmetic all come from existing code paths (`SeriesAverages`, the series page's status precedence) rather than a second implementation.
- **Not affected**: series building — the page reads what is stored and never triggers a build, so opening it costs no MAL fetches.
