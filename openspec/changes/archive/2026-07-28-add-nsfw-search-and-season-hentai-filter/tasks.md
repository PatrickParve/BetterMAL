## 1. MAL client: NSFW search results and the content rating

- [x] 1.1 Add `&nsfw=true` to the search URL in `MalClient.SearchAnimeAsync` (`backend/AnimeTracker.Api/Services/Mal/MalClient.cs`), with a comment matching the one on `GetSeasonAsync` — without it MAL silently omits `r+`/`rx` entries, so titles visible on the season page can't be found by searching.
- [x] 1.2 Add `rating` to `DefaultAnimeFields` in the same file. This is deliberately on the *lean* field list so season/top-anime/user-list/full-detail all record it with no extra request; note that in the comment above the constant.
- [x] 1.3 Add `public string? Rating { get; set; }` to `MalAnimeNode` (`Services/Mal/Dto/MalAnimeNode.cs`) with a comment listing MAL's vocabulary (`g, pg, pg_13, r, r+, rx`) and that `rx` means hentai. Snake-case JSON naming is already handled globally by `JsonOptions`.
- [x] 1.4 Sanity-check the search fix live before touching anything else: `GET /api/anime/search?q=<a known r+/rx title>` on the dev backend (port `BACKEND_PORT` from `.env`, currently 5050) returns the title, where it returned nothing before.

## 2. Persist the rating

- [x] 2.1 Add `public string? Rating { get; set; }` to `AnimeMetadata` (`Models/AnimeMetadata.cs`), next to `MediaType`/`AiringStatus`, with the same "raw MAL vocabulary kept as a string" rationale — an unrecognized value must not break ingestion.
- [x] 2.2 Set `target.Rating = node.Rating;` in **both** `ApplyTo` (rich) and `ApplyLeanTo` (lean) in `Services/Mal/MalMappingExtensions.cs`. Add a short note on `ApplyLeanTo` that `rating` is in both field sets, so writing it here does not violate the lean-never-clobbers-rich rule.
- [x] 2.3 Add the EF migration for the nullable `Rating` text column on `AnimeMetadata` — `dotnet ef migrations add AddAnimeRating` inside the `mcr.microsoft.com/dotnet/sdk:10.0` image (the local SDK is 9.0; bind-mounting `~/Documents` fails, so rsync `backend/` to `/private/tmp/bm-build` first and copy the generated files back), or hand-write the migration + `.Designer.cs` + `AnimeTrackerDbContextModelSnapshot.cs` entry following `20260727133000_AddTopAnimeSelectionPosition` as the template.
- [x] 2.4 Start the backend (`db.Database.Migrate()` runs at startup) and confirm the column exists: `\d "AnimeMetadata"` shows `Rating | text`.
- [x] 2.5 Populate ratings for one season to test against: `DELETE FROM "SeasonFetchLogs" WHERE "Year" = 2026 AND "Season" = 'summer';`, then `POST /api/season/2026/summer/refresh`, then confirm `SELECT "Rating", count(*) FROM "AnimeMetadata" GROUP BY 1;` shows real values including at least one `rx`.

## 3. Season read path: the `hideHentai` filter

- [x] 3.1 Add a `bool hideHentai` parameter to `ISeasonRepository.GetPageAsync` / `SeasonRepository.GetPageAsync` and apply `query = query.Where(l => l.Anime.Rating != "rx")` **before** `CountAsync`, so `totalCount` matches the filtered set (same shape as the existing `includeMyList` filter). Pull `"rx"` into a named `const string HentaiRating = "rx"` with a comment that MAL returns it lowercase.
- [x] 3.2 Verify the null semantics: log or capture the generated SQL for a `hideHentai=true` read and confirm it emits `("Rating" IS NULL OR "Rating" <> 'rx')`, not a bare `<>`. A row with `Rating = NULL` must still be returned. If EF ever translates it as a bare `<>`, switch to the explicit `l.Anime.Rating == null || l.Anime.Rating != "rx"`.
- [x] 3.3 Thread `hideHentai` through `ISeasonBrowseService.GetPageAsync` / `SeasonBrowseService.GetPageAsync` to the repository call. `RefreshAsync` is untouched — the filter is a read concern only and must never influence what gets cached.
- [x] 3.4 Add `[FromQuery] bool hideHentai = false` to `SeasonController.GetPage` and pass it through. Defaulting to `false` keeps an older frontend against a newer backend behaving exactly as today.
- [x] 3.5 Check both directions against real data: `GET /api/season/2026/summer?hideHentai=false&limit=1` and `…&hideHentai=true&limit=1` return different `totalCount` values, and the difference equals `SELECT count(*)` of that season's listings whose anime `Rating = 'rx'`.

## 4. Search API: single-shot candidate set

- [x] 4.1 Raise the `limit` clamp in `AnimeSearchController.SearchPage` from `Math.Clamp(limit, 1, 50)` to `Math.Clamp(limit, 1, 100)`, matching `SeasonController` and the service's own `MaxResults = 100`, so the client can pull the whole sorted candidate set in one request. Leave the `offset`/`limit` contract otherwise unchanged.
- [x] 4.2 Confirm `GET /api/anime/search/page?q=…&limit=100` returns up to 100 items with `totalCount` equal to the item count when under the cap.

## 5. Frontend API layer

- [x] 5.1 Add `hideHentai: boolean` to `getSeasonPage`'s params in `frontend/src/api/client.ts` and include it in the query string.
- [x] 5.2 No `types.ts` change is needed — `AnimeBrowseItemDto`/`SeasonPageDto`/`SearchPageDto` shapes are unchanged. Confirm this rather than assuming it.

## 6. Content-filter preference

- [x] 6.1 Create `frontend/src/context/ContentFilterContext.tsx` exposing `{ hideHentai, toggleHideHentai }`, persisted to `localStorage` under `bettermal.hideNsfw`, modelled directly on `ScoreVisibilityContext.tsx` (lazy initial read guarded by `typeof window`, a `useEffect` that writes on change, a `useMemo`'d value, and a `useContentFilter()` hook that throws outside the provider). Default `false` when nothing is stored.
- [x] 6.2 Mount `ContentFilterProvider` in `AppShell.tsx` alongside the existing providers, and update the comment there that lists which providers are app-wide.
- [x] 6.3 Add a "Content" section to `SettingsPage.tsx` — place it right after "Score display" so the two display preferences sit together — with a `settings-box__hint` explaining the scope ("Hides hentai (MAL `Rx`) from the seasonal browser. R and R+ titles, search results, and anything already in your list are unaffected.") and a `settings-toggle` checkbox labelled **Hide NSFW**, wired to `useContentFilter()`. No new CSS needed; reuse `.settings-box` / `.settings-toggle`.

## 7. Season page wiring

- [x] 7.1 In `SeasonPage.tsx`, read `hideHentai` from `useContentFilter()` and pass it to every `getSeasonPage` call (initial read, infinite-scroll `loadMore`, and the post-refresh re-read).
- [x] 7.2 Add `hideHentai` to the **read** effect's dependency array (alongside `year`/`season`/`sort`/`inMyList`) and to the infinite-scroll effect's deps, so toggling the setting re-reads from page one.
- [x] 7.3 Add a `hideHentaiRef` next to the existing `sortRef`/`inMyListRef` and use it in the debounced refresh effect's re-read. Do **not** add `hideHentai` to the refresh effect's `debouncedSeasonKey` — changing the setting must never trigger a MAL fetch, same rule as sort and the "In my list" checkbox.
- [x] 7.4 Confirm the "never cached" loading branch still works: `neverCached` is derived from `lastFetchedAt`/`items.length`, and `lastFetchedAt` is independent of the filter, so a fully-filtered season shows "No anime found for this season." rather than a permanent spinner.

## 8. Search page: continuous scroll

- [x] 8.1 Remove the `page` URL parameter, the `setPage` helper, `PAGE_SIZE`-based offsets, the `totalPages` computation, and the `<Pagination>` import/render from `SearchPage.tsx`. Leave `components/Pagination.tsx` in place — `TopAnimePage` still uses it.
- [x] 8.2 Fetch once per `(q, sort)`: `getSearchPage(q, { sort, offset: 0, limit: 100 })`, storing the full array plus `totalCount`, still guarded by `useLatestRequest` and still clearing to empty for a blank query.
- [x] 8.3 Add a `visibleCount` state (initial `CHUNK_SIZE = 48`) reset to `CHUNK_SIZE` whenever the query or sort changes, render `items.slice(0, visibleCount)`, and grow it via an `IntersectionObserver` sentinel below the grid — the same pattern as `SeasonPage`'s infinite scroll, minus the network call since everything is already loaded.
- [x] 8.4 Add `<div ref={sentinelRef} className="search-page__sentinel" />` after the grid and a `.search-page__sentinel { height: 1px }` rule to `SearchPage.css`, mirroring `.season-page__sentinel`. Keep the existing `search-page__loading` element for the in-flight fetch.
- [x] 8.5 Keep the "{totalCount} results" counter showing the true total (not the revealed count), and keep the empty state gated on `items.length === 0 && !loading`.
- [x] 8.6 Update the header comment at the top of `SearchPage.tsx` — it currently says "paginated (not infinite scroll, unlike Season)", which becomes the opposite of the truth. Note the fetch-once/reveal-in-chunks approach and why (no cache behind the search endpoint; paging would re-run the live MAL search per chunk).

## 9. Docs

- [x] 9.1 `CODE_GUIDE.md` §1: note `nsfw=true` on the `GET anime?q=` row and add `rating` to the fields the app persists.
- [x] 9.2 `CODE_GUIDE.md` §2: add `hideHentai` to the `GET /api/season/{year}/{season}` query-param list, and update the `/api/anime/search/page` row's "≤ 100 from MAL" note to mention the client now pulls the whole set in one request (limit clamp is 100).
- [x] 9.3 `CODE_GUIDE.md` §3 (`Services/Search/`): the search page is continuous-scroll, server-sorted once across the ≤100 candidate set.
- [x] 9.4 `CODE_GUIDE.md` §4: add `context/ContentFilterContext.tsx` to the contexts list with its `localStorage` key, and correct any pages-list wording that describes search as paginated.
- [x] 9.5 `SETTINGS.md`: document the new "Hide NSFW" toggle — what it hides (`rx` only), where it applies (season browser only), and that it defaults to off.

## 10. Verification

- [x] 10.1 Build both sides: backend via the `sdk:10.0` Docker image (rsync to `/private/tmp/bm-build` first), frontend with `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`.
- [x] 10.2 Search NSFW end to end: search a known `r+` title and a known `rx` title in the navbar dropdown and on the search page — both appear. Confirm they still appear with "Hide NSFW" enabled (the setting is season-only).
- [x] 10.3 Search scroll: run a broad query (e.g. "love"), confirm no pagination controls anywhere, that scrolling appends cards smoothly to the end of the result set, that the counter shows the full total throughout, and that only **one** `/api/anime/search/page` request is made per query (check the network tab and the backend log for a single MAL call).
- [x] 10.4 Search sort: change the sort mid-scroll — results reset to the top chunk, and scrolling back down shows one continuous sorted order with no repeats or gaps across chunk boundaries.
- [x] 10.5 Back-navigation: open an anime from search results, press Back — the query and sort are restored from the URL. Load an old-style `/search?q=x&page=3` URL and confirm it renders the first chunk without erroring.
- [x] 10.6 Hide-NSFW default: with a cleared `bettermal.hideNsfw` key, the Settings checkbox is unchecked and a season containing hentai shows it.
- [x] 10.7 Hide-NSFW on: enable it, and on a season with known `rx` entries confirm those cards disappear, the result count drops by exactly the number of `rx` listings, `r`/`r+` titles stay, and scrolling to the end loads no hentai on any later page. Watch the backend log to confirm toggling triggers **no** MAL fetch.
- [x] 10.8 Persistence: reload and open a second tab — the setting survives both.
- [x] 10.9 Confirm the untouched surfaces are untouched with the setting on: my list, top anime, airing, home dashboard, search, and an `rx` anime's own detail page all render exactly as before.
