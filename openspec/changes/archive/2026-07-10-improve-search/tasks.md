## 1. Backend — type-ahead merge, ranking, and exact match

- [x] 1.1 In `Data/Repositories/IAnimeMetadataRepository.cs`, add `PopularityRank` to `AnimeTitleProjection`; project it in `AnimeMetadataRepository.GetSearchIndexAsync`
- [x] 1.2 Rewrite `Services/Search/AnimeSearchService.SearchAsync` to merge local-cache candidates with a live MAL search (dedupe by id, coalesce missing fields), never short-circuiting on local matches
- [x] 1.3 Rank matches prefix-first (title or English title starts with the query), then contains, each group ordered by popularity (nulls last); take the top 5
- [x] 1.4 Add a shared quoted-query parser: `"…"` → exact title/English-title match; unquoted → contains/prefix. Wrap the MAL call so a failure degrades to local-only
- [x] 1.5 Remove the now-unused word-boundary matcher (`MatchesWordBoundaryPrefix` / `WordSplitter` regex)

## 2. Backend — full search results endpoint

- [x] 2.1 Add `Services/Search/SearchPageDto.cs` with `SearchAnimeItemDto` (animeId, title, englishTitle, pictureUrl, totalEpisodes, mediaType, malScore, popularityRank, myScore) and `SearchPageDto`
- [x] 2.2 Add `SearchPageAsync(query, sortKey, offset, limit, ct)` to `IAnimeSearchService` / `AnimeSearchService`: fetch up to 100 MAL results preserving relevance order, apply the exact-match filter, join My score from `UserAnimeEntries`, sort (relevance/popularity/malScore/alphabetical/myScore, nulls last), and page
- [x] 2.3 Add `GET /api/anime/search/page` to `Controllers/AnimeSearchController` (`q`, `sort=relevance`, `offset`, `limit` clamped to 50); leave the existing `/api/anime/search` action's contract intact

## 3. Frontend — search bar submission

- [x] 3.1 In `components/SearchBar.tsx`, add Enter-key and magnifier-button submit that navigates to `/search?q=<query>` without clearing the input
- [x] 3.2 Sync the input from the `q` URL param while on `/search` so a reload or direct link shows the term; style the new `.search-bar__submit` button in `SearchBar.css`

## 4. Frontend — search results page

- [x] 4.1 Add `SearchAnimeItemDto` / `SearchPageDto` to `api/types.ts` and `getSearchPage` to `api/client.ts`
- [x] 4.2 Add `pages/SearchPage.tsx` (+ `SearchPage.css`): season-style card grid, sort `<select>` (Relevance default, Popularity, MAL score, Alphabetical, My score), 50/page via the `Pagination` component, with `q`/`sort`/`page` in the URL
- [x] 4.3 Register the `/search` route in `AppShell.tsx`

## 5. Verification

- [x] 5.1 `dotnet build backend/AnimeTracker.Api/AnimeTracker.Api.csproj` passes with no new warnings
- [x] 5.2 `cd frontend && npm run build` and `npm run lint` pass with no new errors
- [x] 5.3 After `docker compose up -d --build`, confirm the type-ahead returns the full franchise for "attack" and surfaces "Hell's Paradise" for "paradise", and that `GET /api/anime/search/page` returns the paginated/sortable shape
