## Why

Searching for a franchise like "Attack on Titan" only ever returns individual anime entries — the season, the movies, the OVAs — even though the app already models the whole franchise as a series with its own page. The user who wants the franchise overview has to open one entry and then click through to its series page. Search should offer the series directly, clearly marked as a series so it is never mistaken for a single anime.

## What Changes

- Search matches **series** as well as anime. A series matches when the query matches its root anime's title or English title, or the title of any of its members — so "attack on titan", "shingeki", or "final season" all surface the Attack on Titan series.
- Matching series appear in **both** search surfaces:
  - the navbar type-ahead dropdown, pinned above the anime matches;
  - the full `/search` results page, at the front of the default (relevance) ordering.
- A series result is visually marked as a series: its title is followed on the line beneath by a **"Series" badge** plus its entry count (e.g. `SERIES · 4 entries`). Anime results are unchanged.
- Clicking a series result navigates to that series' page (`/series/{rootAnimeId}`), not an anime detail page.
- Series are only known once they have been built, and today a build happens only when someone opens a series page. To make search useful without adding MAL fetches to the request path, search **schedules a background build** for the top anime match when that anime has no stored series yet — so the franchise becomes searchable on a later search rather than never.
- Series results never cost the anime results their slots on the results page's total count semantics: the dropdown's 5-result budget is shared, with at most 2 series shown.

## Capabilities

### New Capabilities

None — this extends existing search and series behaviour.

### Modified Capabilities

- `navigation-and-search`: the type-ahead search and the full search results page gain series results, with a defined match rule, ranking/placement, cap, series indicator, and navigation target. Search also triggers the background series build described above.
- `series-page`: series building gains a second trigger — a background build scheduled from search — in addition to the existing "opening a series page" trigger. Existing single-flight and persistence behaviour is unchanged.

## Impact

- **Backend**
  - `Services/Search/AnimeSearchService.cs` — series lookup merged into both `SearchAsync` and `SearchPageAsync`.
  - `Services/Search/AnimeSearchResultDto.cs`, `SearchPageDto.cs` — result shapes gain a series variant (kind discriminator, series id, entry count, root anime id).
  - `Controllers/AnimeSearchController.cs` — unchanged routes, richer payloads.
  - New series-lookup read path over `Series`/`SeriesMembers` (title matching against member metadata), plus a background build scheduler reusing `SeriesGraphBuilder`/`RefreshGate`.
- **Frontend**
  - `api/types.ts`, `api/client.ts` — search result types gain the series variant.
  - `components/SearchBar.tsx` + `SearchBar.css` — render series rows with the badge line and link to `/series/:animeId`.
  - `pages/SearchPage.tsx` + `SearchPage.css` — render series cards in the grid with the badge line.
  - Possibly a small shared `SeriesBadge` component used by both surfaces.
- **No schema migration** — `Series` and `SeriesMember` already carry everything needed.
- **No new external dependencies**; no additional MAL calls on the search request path.
