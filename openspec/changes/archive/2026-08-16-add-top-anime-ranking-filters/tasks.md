## 1. Backend — ranking type as a first-class dimension

- [x] 1.1 Add `Services/Library/TopAnimeRankingType.cs`: the allow-list of MAL `ranking_type` tokens this app exposes (`all`, `tv`, `movie`, `ova`, `special`, `bypopularity`, `favorite`), the default (`all`), and a `TryParse`/`IsSupported` helper. Document in a comment that `ona` and `music` are absent because MAL API v2 answers those with `400 invalid ranking_type` (design D1).
- [x] 1.2 Add `RankingType` (string) to `Models/TopAnimeRankingEntry.cs` and to `Models/TopAnimeFetchLog.cs`; drop `TopAnimeFetchLog.Id`. Update both doc comments — the fetch log no longer holds "at most one row ever", it holds at most one row *per list*.
- [x] 1.3 In `Data/AnimeTrackerDbContext.cs`, re-key `TopAnimeRankingEntry` on `(RankingType, AnimeId)` and `TopAnimeFetchLog` on `RankingType`, keeping the existing cascade-delete FK from the ranking entry to `AnimeMetadata`.
- [x] 1.4 Generate the EF migration (`AddTopAnimeRankingType`) and check the generated SQL matches design's migration plan: `RankingType` added with a `'all'` default so existing rows backfill as the All ranking, primary keys dropped and re-added, `TopAnimeFetchLogs.Id` removed.

## 2. Backend — read and refresh per list

- [x] 2.1 `ITopAnimeRepository` / `TopAnimeRepository`: `GetLastFetchedAsync` and `GetRankingAsync` both take a ranking type and filter on it; ranking rows stay ordered by `Rank`.
- [x] 2.2 `ITopAnimeService` / `TopAnimeService`: `GetRankingAsync(rankingType, ct)`. Thread the type through `EnsureFreshAsync`, `IsFreshAsync`, and `FetchAndCacheAsync`; pass it to `malClient.GetRankingAsync`; scope the in-place rank update and the stale-row removal to that type's rows only, so refreshing Movie never deletes All's rows.
- [x] 2.3 Change the `RefreshGate` key from `"top-anime"` to `$"top-anime:{rankingType}"` so two different lists refresh in parallel while two requests for the same list still collapse into one.
- [x] 2.4 `TopAnimeController`: accept an optional `type` query parameter on `GET /api/top-anime`, defaulting to `all`; return `400` for a value outside the allow-list rather than silently serving All.

## 3. Backend — tests

- [x] 3.1 Add `Services/Library/TopAnimeServiceTests.cs` (in-memory `AnimeTrackerDbContext` + a fake `IMalClient`, following the existing `Services/Series` test setup) covering: a fetch of one list does not mark another as fetched for the day; refreshing one list leaves another list's cached rows intact; a same-day revisit makes no MAL call; a failed fetch leaves the list unmarked and still serves cached rows.
- [x] 3.2 Add a controller-level test that an unsupported `type` (e.g. `ona`) is rejected with `400` and never reaches the MAL client.
- [x] 3.3 Run `dotnet test` for `backend/AnimeTracker.slnx` and confirm the full suite passes.

## 4. Frontend — data layer

- [x] 4.1 `api/types.ts`: add a `TopAnimeRankingType` union matching the backend allow-list, plus the ordered list of `{ value, label }` pairs used by the selector (labels: All, TV, Movie, OVA, Special, Popularity, Favourite).
- [x] 4.2 `api/client.ts`: `getTopAnime(type)` sends `?type=…`, omitting the parameter for `all` so the default request URL is unchanged.

## 5. Frontend — Top anime page

- [x] 5.1 Replace the module-scoped `cachedItems` / `inFlightLoad` pair in `pages/TopAnimePage.tsx` with per-ranking-type `Map`s, keeping (and updating) the comment explaining why this page uses a module cache rather than `usePageData`.
- [x] 5.2 Read the selected list from `?type` in the URL, validating against the allow-list and falling back to `all`; derive the three render states from whether that type is already cached (cached → render, not cached but another list is on screen → render the outgoing list muted, nothing on screen → "Loading…").
- [x] 5.3 Add the selector button row below the header: one button per list, `aria-pressed` on the active one, a visible selected style, buttons that wrap on narrow windows, and no-op on selecting the list already shown.
- [x] 5.4 Selecting a list pushes a history entry and drops `page` from the URL, so the new list opens on page 1 and back returns to the previous list.
- [x] 5.5 Make `setEntry` patch the anime's entry in every cached list, not just the visible one, so an Add from one list is reflected in the others.
- [x] 5.6 `pages/TopAnimePage.css`: style the selector row (selected/unselected states, shared control height, wrapping) and the muted non-interactive state for the outgoing list during a switch.

## 6. Verification

- [x] 6.1 Build the frontend with Node 22 (`nvm use 22 && npm run build` in `frontend/`) and run `npm run lint`.
- [x] 6.2 Build the backend against the .NET 10 SDK image and confirm the migration applies cleanly against a database that already holds `all`-ranking rows (existing rows survive as the All list; no re-fetch forced).
- [x] 6.3 Manual pass on the running app: All is default on a fresh visit; each of the seven buttons loads its own ranking numbered from 1 with the podium/card tiers on page 1; re-selecting a loaded list is instant; selecting an unloaded list keeps the previous list visible and muted; back/forward steps through lists and pages; adding from one list shows "Edit" in another.

## 7. Docs

- [x] 7.1 Update `CODE_GUIDE.md`: the `GET /api/top-anime` row in the endpoint table (now takes `type`), and the `TopAnimeRankingEntries` / `TopAnimeFetchLogs` descriptions in the `Data/` section (now keyed per ranking list).
