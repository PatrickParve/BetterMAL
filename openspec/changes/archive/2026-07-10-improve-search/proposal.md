## Why

The navbar type-ahead returned only locally-cached titles and stopped there: if any cached title matched, it never consulted MAL. So a search for "attack" surfaced the single incidental cached entry ("Attack on Titan: The Last Attack") and missed the rest of the franchise, and "paradise" returned "Zombieland Saga Movie: Yumeginga Paradise" while never finding the uncached "Hell's Paradise". Matching was also word-boundary and ordered alphabetically rather than "starts-with, most-popular-first", so short queries (e.g. "a") returned scattered substring hits instead of the popular titles that begin with the query. There was also no way to see the full result set for a query — only the 5-item dropdown.

## What Changes

- **Type-ahead** now merges the local cache with a live MAL search on **every** query (no local-first short-circuit), ranks titles that **start with** the query ahead of ones that merely **contain** it, orders each group by **popularity**, and returns the top 5 — so uncached and franchise titles both appear.
- **Exact match**: wrapping a query in double quotes (`"…"`) switches from closest-match to exact-title matching, in both the dropdown and the results page.
- **New full search results page** (`/search`): all matches for a query, shown like the seasonal grid (picture, title, media type, episode count), defaulting to the API's relevance order and additionally sortable by Popularity, MAL score, Alphabetical, and My score, paginated at 50 per page.
- **Search submission**: pressing Enter or clicking a new magnifier button in the search box opens the results page; the submitted text stays in the box (and is restored from the URL on the search page) instead of being cleared.

## Capabilities

### New Capabilities
<!-- None — extends the existing navigation-and-search capability. -->

### Modified Capabilities
- `navigation-and-search`: the type-ahead merges local + live MAL and ranks prefix-first by popularity with exact-match quotes; a full search results page is added; and search submission keeps the query text in the box.

## Impact

- **Backend** (`backend/AnimeTracker.Api`): `Services/Search/AnimeSearchService` rewritten (merge + rank + exact) and extended with `SearchPageAsync`; new `Services/Search/SearchPageDto` (`SearchPageDto`, `SearchAnimeItemDto`); `Controllers/AnimeSearchController` gains `GET /api/anime/search/page`; `Data/Repositories` search index (`AnimeTitleProjection` / `GetSearchIndexAsync`) gains `PopularityRank`.
- **Frontend** (`frontend/`): `components/SearchBar` (Enter/magnifier submit + URL sync); new `pages/SearchPage.tsx` + `SearchPage.css`; `AppShell` `/search` route; `api/client.ts` (`getSearchPage`) and `api/types.ts` (`SearchPageDto`, `SearchAnimeItemDto`).
- **Data**: none — no schema change; My score is joined from `UserAnimeEntries` at read time. No breaking API changes (the new endpoint is additive).
- **External**: MyAnimeList search endpoint only, called with a higher limit for the results page; no new dependencies.
