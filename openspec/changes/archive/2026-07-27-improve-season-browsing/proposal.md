## Why

The Season page is missing anime that MAL's own seasonal listing shows — measured at 22 anime, ~15% of summer 2026 — and there is no way to force it to catch up. A season is fetched live only on its first visit (plus once per local day for the current/upcoming season), and that fetch happens *while* the page request blocks, so the page is simultaneously slow to open and stale. Worse, a season that is no longer current never re-fetches at all: its cached MAL scores and popularity ranks freeze at first-cache time, because no other job refreshes anime outside my list (`MetadataRefreshService` is my-list-only and score-only). Spring 2026's 150 non-my-list anime still carry scores captured on 2026-07-09. The page's controls and grid also no longer fit the app's wider layout: cards leave a large empty gutter on the right, and the season quick-jump sits crowded at the far right of the header.

## What Changes

- **Missing anime**: the season fetch (`GET /v2/anime/season/{year}/{season}`) does not pass `nsfw=true`, so MAL silently omits R+/Rx-rated entries — the same omission that was already fixed for the my-list fetch. Season fetches will request `nsfw=true`, and re-fetching an already-cached season will add newly-returned anime to the cached listing.
- **Cache-first rendering**: the season read endpoint no longer blocks on MAL. It always serves the cached listing immediately, and reports when that listing was last fetched and whether it is empty.
- **Refresh on visiting a season, at most once per day**: opening a season kicks off a background refresh for it — past, current, or upcoming alike, with no season-class rules and no timer — unless that season was already fetched successfully on the current local day, in which case it is served from cache. Refresh is driven by the visit, the only strategy that stays correct on a deployment that does not run continuously. When it completes, the page updates itself in place without losing scroll position, showing a passive "Updating…" indicator meanwhile. Remaining guards: one in-flight refresh per season, and a short client debounce so arrow-stepping fetches only the season you land on. This is the existing daily rule with its current/upcoming-only gate removed, so past seasons stop being frozen.
- **No refresh button**: the season page gets no control that triggers a MAL fetch. Refresh is a consequence of opening a season and nothing else. The accepted cost is that a change MAL makes mid-day is not visible until the next day.
- **Stale scores unfreeze**: because past seasons now refresh, each visit's lean upsert updates every listed anime's MAL score and popularity rank, so score/popularity sorts on older seasons stop ranking by values captured months earlier.
- **My-score grouping**: sorting by "My score" lists scored anime first (highest score down), then a labelled `Unwatched` divider, then everything else by popularity — instead of dumping unscored anime into one undifferentiated tail.
- **"In my list" toggle**: a checkbox beside the sort control, checked by default; unchecking it removes anime already in my list from the results (and from the total count), so the page shows only what is left to discover.
- **Grid fills the width**: season cards grow to fill the row so the right-hand gutter disappears, with larger cover images.
- **Header layout**: season navigation (arrows + label) is centered in the header, with the year/season quick-jump dropdowns immediately to its right, and sort/filter controls grouped after them.

## Capabilities

### New Capabilities

None — this extends the existing season browsing capability.

### Modified Capabilities

- `season-browser`: cache-first read with a visit-triggered background refresh for every season (replacing the blocking live-fetch and the current/upcoming-only daily rule); NSFW titles included in season listings; re-fetch adds newly-returned anime and refreshes cached scores/ranks; "my score" sort splits into scored / `Unwatched` groups; new "in my list" inclusion filter; season grid fills the content width and header controls are re-ordered.
- `mal-api-integration`: the `nsfw=true` requirement extends from the user animelist to season listing requests.

## Impact

- **Backend**: `MalClient.GetSeasonAsync` (nsfw param); `SeasonBrowseService` (split read path from refresh path, drop the season-class gate from the daily check, add a per-season in-flight guard); `ISeasonBrowseService` / `SeasonController` (new refresh endpoint, `includeMyList` query param, freshness metadata on the page DTO); `SeasonRepository` / `ISeasonRepository` (my-list filter, grouped my-score ordering); `AnimeBrowseItemDto` (new `inMyList` flag — also flows through search results).
- **Frontend**: `SeasonPage.tsx` / `SeasonPage.css` (cache-first load, debounced background refresh split from the read effect, "Updating…" indicator, `Unwatched` divider, in-my-list checkbox, header and grid layout); `api/client.ts` and `api/types.ts` (refresh call, new params and DTO fields); `AnimeCard.css` (fluid-width card variant).
- **API contract**: `GET /api/season/{year}/{season}` gains `includeMyList`, returns `lastFetchedAt`; new `POST /api/season/{year}/{season}/refresh`.
- **Data**: no schema migration — existing `SeasonAnimeListing` / `SeasonFetchLog` tables are reused. Seasons last fetched on an earlier date self-heal on their next visit; seasons already fetched today heal the following day, or immediately if the fetch log is cleared once at deploy.
- **MAL budget**: a season refresh is 3 paged calls (205 entries at 100/page), at most once per season per day; the single-flight guard and client debounce bound it further to seasons actually opened.
