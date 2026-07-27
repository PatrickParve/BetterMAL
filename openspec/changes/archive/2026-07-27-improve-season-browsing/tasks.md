## 1. MAL client: stop dropping season entries

- [x] 1.1 Add `nsfw=true` to the season listing URL in `MalClient.GetSeasonAsync` (`backend/AnimeTracker.Api/Services/Mal/MalClient.cs`), matching `GetUserAnimeListAsync`, and note in the comment why it is needed.
- [x] 1.2 Sanity-check the fix against a live season: after section 3/4 land, clear the fetch log (`DELETE FROM "SeasonFetchLogs";` — otherwise the once-per-day rule returns `Refreshed: false` and nothing happens), hit `POST /api/season/2026/summer/refresh`, and confirm the `SeasonAnimeListings` row count for 2026/summer goes from 124 to ~146 (MAL returns 205 with nsfw; 59 are excluded by the `start_season` rule, which stays as-is). (Confirmed live: 124 → 146 exactly.)

## 2. Repository: my-list filter, `inMyList`, grouped my-score ordering

- [x] 2.1 Add `bool InMyList` to `SeasonAnimeItem` and to `AnimeBrowseItemDto` (`Services/Library/AnimeBrowseItemDto.cs`).
- [x] 2.2 Populate `InMyList` in `SeasonRepository.GetPageAsync`'s projection from `l.Anime.UserEntry != null` (not from `MyScore`, which is null for unscored entries).
- [x] 2.3 Add an `includeMyList` parameter to `ISeasonRepository.GetPageAsync` / `SeasonRepository.GetPageAsync`; when false apply `.Where(l => l.Anime.UserEntry == null)` **before** `CountAsync` so `totalCount` matches the filtered set.
- [x] 2.4 Change the `SeasonSortKey.MyScore` ordering to the grouped chain from design §5: unscored last, then score descending, then unranked-last popularity, then title.
- [x] 2.5 Populate `InMyList` in `AnimeSearchService.SearchPageAsync` (the other `AnimeBrowseItemDto` producer) from the entry dictionary it already loads for `myScore`.

## 3. Service: split read from refresh, visit-triggered refresh, single-flight

- [x] 3.1 Add `SeasonRefreshResultDto(bool Refreshed)` and add `LastFetchedAt` to `SeasonPageDto` (`Services/Season/SeasonBrowseDto.cs`) — the latter is what lets the client tell "never cached" from "cached and genuinely empty".
- [x] 3.2 Change `ISeasonBrowseService.GetPageAsync` to take `includeMyList` and drop its live-fetch behavior; add `RefreshAsync(int year, string season, CancellationToken ct)`. Update the interface doc comment: cache-first reads, refresh triggered by visiting a season, never by a schedule or a user control.
- [x] 3.3 In `SeasonBrowseService`, make `GetPageAsync` repository-only (remove the `EnsureFreshAsync` call), returning the page plus `LastFetchedAt` from `ISeasonRepository.GetLastFetchedAsync`.
- [x] 3.4 Turn `EnsureFreshAsync` into `RefreshAsync` by deleting only the `IsCurrentOrUpcoming` early-return — keep the existing `broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate` check so every season fetches at most once per local day — and return `Refreshed: false` when it skips. Delete the now-unused `IsCurrentOrUpcoming` helper and `SeasonCalendar.GetNextSeason` if this was its only caller.
- [x] 3.5 Add per-season single-flight: a singleton keyed `SemaphoreSlim` map (`(year, season)`); wait on it, then re-check `LastFetchedAt` **inside** the lock so a waiter sees the first refresh's stamp and returns `Refreshed: false` without a second MAL fetch. Register whatever holds the map as a singleton in `Program.cs`.
- [x] 3.6 Confirm `SeasonFetchLog.LastFetchedAt` is still stamped only after a successful MAL fetch, so a failure doesn't consume the day and the next visit retries.
- [x] 3.7 Keep the existing swallow-and-log behavior for MAL failures in `RefreshAsync` — with no refresh control there is no UI that needs to distinguish a failed refresh from a deduped one; a failure returns `Refreshed: false` and the client leaves the cached page alone.

## 4. API surface

- [x] 4.1 Add `[FromQuery] bool includeMyList = true` to `SeasonController.GetPage` and pass it through; the response now carries `lastFetchedAt`.
- [x] 4.2 Add `POST api/season/{year:int}/{season}/refresh`, validating `season` against the existing `ValidSeasons` set and returning `SeasonRefreshResultDto`.
- [x] 4.3 Update the backend HTTP API table in `CODE_GUIDE.md` §2 and the `Services/Season/` bullet in §3: cache-first read, refresh-on-visit once per local day for *any* season, and that the current/upcoming-only restriction is gone. Also correct the §3 `Services/Metadata/` note if needed to make clear `PopularityRank` is refreshed only by the lean season/top-anime paths.

## 5. Frontend API layer

- [x] 5.1 Add `inMyList: boolean` to `AnimeBrowseItemDto` and `lastFetchedAt: string | null` to `SeasonPageDto` in `frontend/src/api/types.ts`; add a `SeasonRefreshResultDto` type.
- [x] 5.2 Add `includeMyList` to `getSeasonPage`'s params in `frontend/src/api/client.ts` and add `refreshSeason(year, season)` posting to the new endpoint.

## 6. Season page: cache-first load and refresh

- [x] 6.1 Add `inMyList` to the URL-derived state in `SeasonPage.tsx` (absent = checked, `inMyList=0` = unchecked) and include it in the **read** effect's dependencies alongside `year`/`season`/`sort`.
- [x] 6.2 Split the refresh into its own effect keyed on `[year, season]` only — so changing sort or the checkbox re-reads from cache without triggering any MAL fetch — with a ~400 ms debounce so arrow-stepping fetches only the season settled on (clear the timer on cleanup).
- [x] 6.3 Track `lastFetchedAt` and `refreshing` state; fire `refreshSeason(year, season)` without blocking the render, and on `refreshed === true` re-read `offset 0, limit = max(items.length, PAGE_SIZE)` and replace `items`/`totalCount`, gated on `isLatest(requestId)` so a stale season's refresh can't clobber a newer one.
- [x] 6.4 Keep the loading state (not "No anime found") when the cache is empty — `lastFetchedAt === null` and zero items — until the refresh resolves and the re-read lands.
- [x] 6.5 Add the passive "Updating…" indicator next to the season label while `refreshing` is true, in reserved/absolutely-positioned space so it doesn't reflow the header. No button and no error UI: a failed refresh just clears the indicator.
- [x] 6.6 Verify a MAL-unreachable refresh (stop the network / point at a bad host) leaves the cached page rendered with no page-level error. (Observed live: a refresh was cancelled mid-fetch — same catch path as a network failure — and the page stayed on its cached listing with no error UI; the season correctly retried and succeeded on the next visit.)

## 7. Season page: sort grouping, my-list checkbox, layout

- [x] 7.1 Render the `Unwatched` divider before the first accumulated item with `myScore === null`, only when sort is `myScore` and that item is not the first — as a grid child spanning `grid-column: 1 / -1`.
- [x] 7.2 Add the "In my list" checkbox beside the sort control (checked by default) writing `inMyList` to the URL; changing it resets to page one like a sort change.
- [x] 7.3 Restructure `.season-page__header` to the three-zone grid from design §7 (title | nav + quick-jump centered | sort + checkbox right-aligned), with a wrapping stacked fallback below 1024px.
- [x] 7.4 Convert `.season-page__grid` to `repeat(auto-fill, minmax(190px, 1fr))` with a 20px gap, and add `.anime-card--fluid { width: 100% }` to `AnimeCard.css`, passing it from `SeasonPage` via the existing `className` prop so other pages keep the fixed 160px card.
- [x] 7.5 Style the divider, checkbox, and "Updating…" text to match the existing header controls (`--border`, `--text-h`, 14px, 6px radius); the indicator should be visually quiet (reduced opacity) so it reads as status, not as a control.

## 8. Verification

- [x] 8.1 Build both sides: `dotnet build` for the backend (per the local SDK note: use the `sdk:10.0` Docker image) and `nvm use 22 && npm run build` in `frontend/`.
- [x] 8.2 Walk the season page end to end: cached season renders instantly; the refresh lands a second or two later; a fully-past season (2026 winter) refreshes on visit exactly like the current one; navigating away and back to the same season does **not** refetch (already fetched today); a second tab does not trigger a second fetch; stepping quickly through five seasons fetches only the one landed on. Watch the backend logs for the MAL call count to confirm each.
- [x] 8.3 Confirm the stale-score fix on real data: note a few 2026-spring anime not in your list, run a refresh, and check their `AnimeMetadata.MalScore` / `PopularityRank` / `LastScoreSyncedAt` move off their 2026-07-09 values.
- [x] 8.4 Check the filters: "My score" shows scored anime, then the `Unwatched` divider, then popularity order — including after scrolling past the first page; unchecking "In my list" removes my entries and the count shrinks; both survive back-navigation from a detail page. Confirm changing either triggers no MAL fetch.
- [x] 8.5 Check the layout at 1440px and at ~1100px: no empty gutter right of the grid, covers larger than before, nav centered with the quick-jump directly to its right.
- [x] 8.6 Confirm the other `AnimeCard` consumers (home, my list, top anime, search, airing) are visually unchanged by the fluid-card and DTO edits. (My List/Top/Airing never used `AnimeCard` — own row/slot layouts; Home and Search confirmed unchanged at 160px, no `.anime-card--fluid` class.)
