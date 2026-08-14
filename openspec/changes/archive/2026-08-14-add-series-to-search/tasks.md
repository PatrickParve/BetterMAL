## 1. Series lookup for search (backend)

- [x] 1.1 Add a series-match read path — a new `Services/Search/SeriesSearchLookup.cs` (scoped, registered in `Program.cs`) that projects `SeriesMembers ⋈ AnimeMetadata` once per search into `(SeriesId, AnimeId, Title, EnglishTitle, IsMainLine, PopularityRank)` plus each series' `RootAnimeId`.
- [x] 1.2 Implement matching over that projection using the search service's existing `EqualsIgnoreCase` / `StartsWithIgnoreCase` / `ContainsIgnoreCase` helpers (lift them to a shared internal static class if cleaner), honouring the quoted-query exact mode from `ParseQuery`.
- [x] 1.3 Collapse member matches to one row per series: keep the strongest match quality (exact > prefix > contains) and the best popularity rank among matching members; project the display fields (title, English title, picture) from the **root** member, and the entry count from the series' full member count.
- [x] 1.4 Order matched series by match quality then popularity rank (unranked last), mirroring `PopularityKey`'s convention.
- [x] 1.5 Return an empty result set cheaply when no series are stored at all, so a fresh install pays nothing.

## 2. Wire series into the two search entry points (backend)

- [x] 2.1 Add a `SeriesSearchResultDto` (series id, root anime id, title, English title, picture url, entry count) in `Services/Search/`.
- [x] 2.2 Type-ahead: change `AnimeSearchResultDto` (or wrap it) so dropdown rows carry a `kind` discriminator of `"anime" | "series"`; in `SearchAsync`, take at most 2 matched series, emit them first, and fill the remaining rows with anime up to the 5 total.
- [x] 2.3 Results page: add a `Series` array to `SearchPageDto` (separate from `items`, per design decision 5); in `SearchPageAsync`, populate it with at most 3 matched series **only** when `sortKey` is relevance, and leave `TotalCount` counting anime only.
- [x] 2.4 Confirm `AnimeSearchController` needs no route or signature change; update its XML doc comments to mention series rows.

## 3. Background series build trigger (backend)

- [x] 3.1 Add `Services/Series/ISeriesBuildTrigger.cs` + `SeriesBuildTrigger.cs` mirroring `IAiringRefreshTrigger`/`AiringRefreshTrigger` (concurrent queue + semaphore), with an internal bounded set of already-enqueued anime ids for dedupe.
- [x] 3.2 Add `Services/Series/SeriesBuildTriggerBackgroundService.cs` mirroring `AiringRefreshTriggerBackgroundService`: drain the queue, resolve `ISeriesService` in a fresh scope, call `GetSeriesAsync(animeId)`, log-and-continue on failure (including `SeriesNotFoundException` for a lone anime).
- [x] 3.3 Register the trigger as a singleton and the background service as a hosted service in `Program.cs`, next to the existing airing trigger registrations.
- [x] 3.4 In `AnimeSearchService`, after ranking, enqueue the top-ranked anime match when it has no `SeriesMembers` row — guarded by query length ≥ 3, at most one id per search, and never awaited or allowed to throw into the response.
- [x] 3.5 Verify the enqueue path adds no DB round-trip beyond the membership check already needed by the series lookup (reuse the projection from 1.1 rather than issuing a second query).

## 4. Frontend types and API client

- [x] 4.1 Add `SeriesSearchResult` to `api/types.ts`; make `AnimeSearchResult` a discriminated union (or add `kind`) matching the dropdown payload from 2.2.
- [x] 4.2 Extend `SearchPageDto` in `api/types.ts` with the `series` array from 2.3.
- [x] 4.3 Confirm `searchAnime` / `getSearchPage` in `api/client.ts` need no signature change beyond the new return types.

## 5. Series indicator component

- [x] 5.1 Add a small shared `components/SeriesBadge.tsx` (+ CSS) rendering the `SERIES · N entries` line: a pill plus the count, styled to sit on the line below a title in both the dropdown row and the card meta slot.
- [x] 5.2 Pluralise the count correctly (`1 entry` / `N entries`).

## 6. Navbar type-ahead

- [x] 6.1 In `components/SearchBar.tsx`, branch on `kind`: render a series row with the root picture, the series title, and `SeriesBadge` beneath it; keep anime rows exactly as they are.
- [x] 6.2 Navigate series rows to `/series/{rootAnimeId}` (clearing the query and closing the dropdown, as `goToAnime` does).
- [x] 6.3 Update `SearchBar.css` for the two-line row (title above, badge below) without changing the single-line anime row's height behaviour or the dropdown's max width.
- [x] 6.4 Check the settings-page anime-refresh picker, which shares `useAnimeSearch` — it must ignore/filter series rows rather than offering a series as a refresh target.

## 7. Search results page

- [x] 7.1 In `pages/SearchPage.tsx`, read the new `series` array into the page's read state alongside `items` and `totalCount`.
- [x] 7.2 Render series cards first in the grid: reuse the `AnimeCard` shell where possible (or a thin `SeriesCard` wrapper) linking to `/series/{rootAnimeId}`, with `SeriesBadge` in the slot `AnimeCardMeta` occupies.
- [x] 7.3 Keep chunked reveal over anime only — series cards are always all rendered and never counted into `visibleCount`.
- [x] 7.4 Verify the count line still reads the anime `totalCount`, and that the "No anime found" empty state does not appear when only a series matched.
- [x] 7.5 Add any needed `SearchPage.css` rules so a series card is the same size and shape as the anime cards beside it.

## 8. Tests

- [x] 8.1 Add `backend/AnimeTracker.Api.Tests/Services/Search/SeriesSearchLookupTests.cs`: match on a non-root member's title, root-based display fields, strongest-match-wins collapsing, exact-mode quoting, entry count including extras.
- [x] 8.2 Add tests for the type-ahead composition: series first, capped at 2, total still 5, unchanged output when no series match.
- [x] 8.3 Add tests for the results-page composition: series present under relevance, absent under every other sort, `TotalCount` unchanged by series.
- [x] 8.4 Add tests for the build trigger: enqueued when the top match has no series, skipped for queries under 3 characters, deduped across repeated searches, and never throwing into the search result.
- [x] 8.5 Run `dotnet test` for the backend suite (per the project's Docker `sdk:10.0` build path) and `npm run build` + `npm run lint` for the frontend.

## 9. Manual verification

- [x] 9.1 Search "attack on titan" with the series stored: it appears first in the dropdown and first on the results page, badged, and both click through to `/series/…`.
- [x] 9.2 Search a franchise whose series is not yet stored, wait for the background build, search again: the series now appears.
- [x] 9.3 Switch the results page sort away from Relevance and back: series disappear and reappear, count line stays anime-only.
- [x] 9.4 Confirm the type-ahead's perceived latency is unchanged with a warm series store.
