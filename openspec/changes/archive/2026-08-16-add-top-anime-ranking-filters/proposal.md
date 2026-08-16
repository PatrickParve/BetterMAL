## Why

The Top anime page shows exactly one list — MAL's overall "all" ranking — so there is no way to ask the questions the ranking is most useful for: what are the best movies, the best OVAs, what is most popular, what is most favourited. MAL exposes all of those as first-class rankings on the same endpoint the page already calls, so the data is one query parameter away.

## What Changes

- Add a row of ranking-list buttons to the Top anime page: **All**, **TV**, **Movie**, **OVA**, **Special**, **Popularity**, **Favourite**. The button for the list currently shown is highlighted as a selected/pressed state, so the active list is unambiguous.
- **All** is the default on a fresh visit to the page. The selected list lives in the URL (`?type=…`), so back/forward navigation returns to the list that was being viewed, exactly as pagination and every other view control on the page already behave.
- Each list gets its own 500-row cached snapshot, its own daily-refresh clock, and its own single-flight guard, so refreshing one list neither blocks nor invalidates another.
- Switching to a list already loaded in this browser session is instant (no network call, no loading state). Switching to a list not yet loaded keeps the outgoing list on screen in a muted, non-interactive state rather than blanking the page to "Loading…".
- Adding or editing an anime from one list patches that anime in every cached list at once, so a title that appears in both All and Movie never shows "Add" on one and "Edit" on the other.
- **ONA** and **Music** buttons are deliberately not included: MAL API v2's `/anime/ranking` endpoint rejects `ranking_type=ona` and `ranking_type=music` with `400 invalid ranking_type` (verified against the live API with this project's client ID). Per the request, no second API provider is introduced to cover them. `airing` and `upcoming` are supported by MAL but were not requested, so they are out of scope.

## Capabilities

### New Capabilities

None — this extends the existing Top anime page rather than introducing a new area of the product.

### Modified Capabilities

- `library-views`: The Top anime page gains a ranking-list selector; its existing rendering, tiering, and pagination requirements are re-scoped to "the selected list" rather than "the ranking". The daily-refresh requirement becomes per-list.
- `metadata-refresh`: The Top Anime listing refresh and its single-flight guarantee are now keyed per ranking list rather than being one global ranking.

## Impact

**Backend**
- `Models/TopAnimeRankingEntry.cs`, `Models/TopAnimeFetchLog.cs` — both gain a `RankingType` dimension (composite keys).
- `Data/AnimeTrackerDbContext.cs` + one EF migration — re-key `TopAnimeRankingEntries` on `(RankingType, AnimeId)` and `TopAnimeFetchLogs` on `RankingType`, backfilling existing rows as `all`.
- `Data/Repositories/ITopAnimeRepository.cs` / `TopAnimeRepository.cs` — reads take a ranking type.
- `Services/Library/TopAnimeService.cs` — fetch, cache, and freshness check per ranking type; `RefreshGate` key becomes `top-anime:{type}`.
- `Controllers/TopAnimeController.cs` — `GET /api/top-anime` accepts an optional `type` query parameter, validated against the allow-list.
- No change to `MalClient` — `GetRankingAsync` already takes `rankingType`.

**Frontend**
- `pages/TopAnimePage.tsx` / `.css` — the selector row, per-type module cache, and cross-list entry patching.
- `api/client.ts`, `api/types.ts` — `getTopAnime(type)` and a `TopAnimeRankingType` union.

**Docs**
- `CODE_GUIDE.md` — endpoint table and the `Data/` table list.
