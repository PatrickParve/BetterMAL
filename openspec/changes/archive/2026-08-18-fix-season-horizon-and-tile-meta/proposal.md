## Why

Stepping past the last season MyAnimeList publishes leaves the Season page spinning "Loading…" forever: MAL answers `404` for a season it has not opened yet (verified today, 2026-08-18 — summer 2026, fall 2026 and winter 2027 return `200`; spring 2027 and beyond return `404`), `SeasonBrowseService.RefreshAsync` swallows that as a failure, no fetch log is written, and the page's never-cached loading state never clears. Nothing in the UI stops the user walking there in the first place — the next arrow and the year quick-jump are unbounded.

Separately, a More-section tile on the Series page wraps its meta line onto a second row in a 140px tile ("TV special · 2023 · 5 ep" for *Jujutsu Kaisen Season 2 Recaps*), knocking that tile's chips and footer out of line with its neighbours.

## What Changes

**Season page — future seasons**

- MAL's `404` for an unopened season stops being treated as a fetch failure. It is recorded as a fact about that season ("MAL has no listing for it") with a fetch timestamp, so the daily-fetch rule applies and the page leaves its loading state.
- The Season page learns a navigable ceiling from the backend: the current season plus MAL's two-season forward window, raised to any later season already cached, and lowered past a season MAL answered `404` for earlier the same local day. Forward navigation stops at that ceiling — the next arrow disables and the quick-jump dropdowns do not offer seasons beyond it. The previous arrow is floored at the quick-jump dropdown's own earliest year (1989) for the same reason. Both arrows visibly grey out when disabled, at rest and on hover.
- A season MAL has no listing for reads "MyAnimeList hasn't listed this season yet" instead of "No anime found for this season."; a season whose very first fetch failed outright reads as a retryable load failure instead of spinning or claiming the season is empty.
- The refresh endpoint's result gains an outcome (`fetched` / `notListed` / `skipped` / `failed`) in place of the bare `refreshed` boolean, so the client can tell those four apart. **BREAKING** for the `POST /api/season/{year}/{season}/refresh` response shape (internal API, frontend is the only caller).

**Series page — More-section tile meta**

- The tile's meta and aired lines are pinned to a single line with ellipsis, matching the timeline card's existing treatment, so no value can ever add a second row. The media type stays on the tile (unchanged) — only the wrapping is fixed.

Non-goals: no "Later" bucket for unannounced anime (MAL's v2 API exposes no such endpoint — the horizon is where the data stops); no change to how a season's membership is classified, cached, or refreshed on the happy path.

## Capabilities

### New Capabilities

_None._

### Modified Capabilities

- `season-browser`: MAL having no listing for a season becomes a recorded, distinct outcome rather than a swallowed failure; season selection gains a navigable ceiling that blocks forward movement past the furthest season MAL publishes; the empty and never-cached states gain distinct wording for "not listed yet" and "first fetch failed".
- `series-page`: the More-section tile's meta line (media type, year, episode count) and aired line are specified to occupy one line each, truncating with an ellipsis rather than wrapping.

## Impact

- Backend: `Models/SeasonFetchLog.cs` (+ EF migration), `Services/Mal/MalClient.cs` / `IMalClient` (404-tolerant season fetch), `Services/Season/SeasonBrowseService.cs`, `ISeasonBrowseService`, `SeasonBrowseDto.cs`, `Controllers/SeasonController.cs` (new bounds endpoint), `Data/Repositories/SeasonRepository.cs` / `ISeasonRepository`.
- Frontend: `pages/SeasonPage.tsx` (+ `.css`), `api/client.ts`, `api/types.ts`, `components/SeriesExtraTile.tsx` / `.css`.
- API: `GET /api/season/{year}/{season}` gains a listing-availability field; `POST /api/season/{year}/{season}/refresh` returns an outcome; new `GET /api/season/bounds`.
- No change to MAL request volume on the happy path; one wasted MAL request per boundary season per local day at most while the ceiling re-probes.
