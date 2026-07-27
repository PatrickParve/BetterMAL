## Context

`SeasonBrowseService.GetPageAsync` calls `EnsureFreshAsync` *inside* the read path: on a season's first visit ever — and once per local calendar day for the current/upcoming season — the HTTP request pages through the whole MAL season (3 calls, ~200 entries) before a single card renders. Between those fetches the page can be up to a day stale, and there is no way to force it to catch up.

Two things make the listing look wrong compared to MAL's own season page:

1. `MalClient.GetSeasonAsync` builds `anime/season/{year}/{season}?limit=…&offset=…&fields=…` with no `nsfw` parameter. MAL defaults to excluding R+/Rx-rated entries, so they never reach the cache. `GetUserAnimeListAsync` already passes `nsfw=true` for exactly this reason (`mal-api-integration`, "NSFW-rated titles are not dropped"). Measured against MAL for summer 2026: 180 entries without the flag, 205 with it; after the `start_season` filter that is 146 anime versus the 124 currently cached — **22 missing, ~15% of the season**.
2. Once a season stops being current/upcoming it is frozen permanently. `EnsureFreshAsync` returns early the moment a `SeasonFetchLog` row exists, so a past season never re-fetches — and nothing else fills the gap. `MetadataRefreshService.RefreshStaleBatchAsync` filters `.Where(a => a.UserEntry != null)` and writes only `MalScore`; `TopAnimeService` covers the top 500. A season anime outside my list and outside the top 500 therefore keeps the `MalScore` and `PopularityRank` it had when the season was first cached — spring 2026's 150 non-my-list anime are still stamped `LastScoreSyncedAt = 2026-07-09`. Scores are most volatile while a season airs and freeze exactly when it stops being current, so score/popularity sorts on a past season rank by their least accurate values. (`PopularityRank` in particular is written by no refresh path except the lean season/top-anime upserts.)

(MAL's "TV (Continuing)" block is a deliberate difference, not a bug: `season-browser` files each anime under its `start_season` only — 59 of summer 2026's 205 entries. Out of scope here.)

The deployment matters for the fix: this stack does not run 24/7. Every timer-based `BackgroundService` in the repo (metadata 10 min, reconciliation weekly, schedule 6 h) simply does not fire while the container is down and has no catch-up pass, and the old season rule compounded that by keying off local calendar date — effectively "one refresh per day the app happens to be open". Visit-triggered refresh is the only strategy that is immune to the uptime pattern, because a visit implies the container is running.

The page's controls also predate the wider `#root` (1440px): the grid is `flex-wrap` over fixed `160px` cards, so the row ends wherever the last card lands, and the quick-jump `<select>`s are pushed to the far right by `justify-content: space-between`.

## Goals / Non-Goals

**Goals:**
- Season anime that MAL lists but we don't (NSFW-rated entries) show up, and a re-fetch backfills them into an already-cached season.
- The page renders from cache instantly and refreshes itself on every season visit — any season, no schedule, no user-facing refresh control — so cached scores and ranks stop being frozen at first-cache time.
- "My score" sort groups scored anime above an `Unwatched` divider, with the remainder by popularity — correct across paginated loads.
- An "In my list" checkbox (default on) can exclude anime already in my list, server-side so paging stays correct.
- The grid fills the content width with larger covers; the header centers season navigation with the quick-jump beside it.

**Non-Goals:**
- Including MAL's continuing long-runners in later seasons (contradicts the one-season-per-anime rule in `season-browser`).
- Browser-side caching (localStorage/service worker). Postgres remains the single cache; "cache-first" means the local API, per CODE_GUIDE's "every page renders from Postgres".
- Push/streaming updates (SignalR, SSE), any timer or scheduled job for seasons, or changes to how Top Anime / Home refresh.
- A user-facing refresh button, in any form. The season page has no control that triggers a MAL fetch; refresh is a consequence of visiting a season, nothing else.
- Fixing the broader `PopularityRank` staleness for my-list anime on other pages (`MetadataRefreshService` is score-only) — real, but a separate change.
- Any schema migration — `SeasonAnimeListing` and `SeasonFetchLog` are reused as-is.

## Decisions

### 1. Split the read path from the refresh path; the client drives the refresh

`ISeasonBrowseService` gets two methods:

- `GetPageAsync(year, season, sortKey, includeMyList, offset, limit, ct)` — repository-only, never touches `IMalClient`. Returns the page plus `LastFetchedAt` (nullable) so the client knows whether a cache even exists.
- `RefreshAsync(year, season, ct)` — does the MAL fetch, subject to the once-per-local-day rule and single-flight, and returns `SeasonRefreshResultDto(bool Refreshed)`.

Controller: `GET /api/season/{year}/{season}` (unchanged shape + `includeMyList` query param + `lastFetchedAt` on the DTO) and `POST /api/season/{year}/{season}/refresh`.

*Alternative considered:* keep one endpoint and fire the refresh with `Task.Run` after responding, letting the client poll `lastFetchedAt`. Rejected — the scoped `AnimeTrackerDbContext` is disposed when the request completes, so it would need its own scope and a hosted queue, and the client would have to poll to learn when to re-read. An awaited POST gives a definite completion signal with no new infrastructure.

*Alternative considered:* a `BackgroundService` that refreshes seasons on a timer. Rejected on deployment grounds (see Context): this stack is not up continuously, so a timer would miss its windows with no catch-up, and it would fetch seasons nobody is looking at. The codebase's visit-triggered pattern (`SeasonBrowseService`, `TopAnimeService`) already establishes "refresh what the user is actually viewing".

### 2. Visit-triggered, once per local day, for every season

The client calls `POST …/refresh` whenever the selected season changes, and the server decides. The rule is one line: **fetch unless this season was already fetched successfully on the current local date.** No season-class distinction, no timer.

This is a smaller diff than it sounds. `EnsureFreshAsync` already ends with exactly this day comparison; what it also has is a season-class gate in front of it that returns early for anything not current/upcoming — which is what froze past seasons permanently. Deleting that gate leaves the daily rule applying uniformly:

```csharp
if (lastFetched is { } fetchedAt)
{
    if (!IsCurrentOrUpcoming(year, season, todayLocalDate))   // ← delete
        return;
    if (broadcastConverter.GetLocalDate(fetchedAt) == todayLocalDate)  // ← keep
        return;
}
```

`IBroadcastLocalTimeConverter` is already a constructor dependency, so no new constant and no new injection. "Local day" is the app's hardcoded `Europe/Helsinki`, consistent with every other date-keyed behavior in the codebase.

The daily rule also subsumes every mechanical-repeat case a short window would have covered — React StrictMode's dev double-mount, back/forward remounts, and the single-flight waiter all land on the same local date. Single-flight via a singleton keyed lock (`ConcurrentDictionary<(int Year, string Season), SemaphoreSlim>`) still matters for genuine concurrency (two tabs opening the same season simultaneously, where neither has stamped `LastFetchedAt` yet): the waiter re-checks `LastFetchedAt` **inside** the lock, finds the first refresh's stamp, and returns `Refreshed: false` without fetching.

Only a *successful* fetch stamps `SeasonFetchLog.LastFetchedAt` — `FetchAndCacheAsync` writes it after the MAL call returns — so a failure does not burn the day, and the next visit retries.

Cost check: a full season is 205 entries at `FullListPageSize = 100`, so **3 MAL calls, once per season per day**, ~1–2 s wall-clock and entirely off the render path. Browsing back through ten seasons in an evening costs 30 calls the first time and nothing on revisits that day.

Client-side, a short debounce on the season selection (~400 ms) means stepping through seasons with the arrows fetches only the season you land on rather than every season you pass through on the first pass of the day.

*Consequence worth naming:* with no manual refresh control, there is no way to pull mid-day changes. If MAL adds an anime at noon and you opened the season at 09:00, you see it tomorrow. That is the same trade the old system already made for the current season, now applied consistently — and it is the accepted cost of the "no button" decision.

### 3. No change-detection: after a refresh, just re-read

An earlier draft computed a `Changed` flag by walking `ChangeTracker` for `AnimeMetadata` entries modified outside an ignore-set of `{ LastScoreSyncedAt }` — needed because `ApplyLeanTo` stamps that column on every row it touches, so "did `SaveChangesAsync` write anything" is always true. Dropped as over-engineering: its only purpose was to avoid a redundant *local* Postgres read of ~150 rows, which costs single-digit milliseconds. The client rule is simply "if the refresh reports `Refreshed: true`, re-read page zero." When nothing actually changed, React reconciles the identical list by `animeId` key and the DOM does not move, so there is no flicker to avoid either.

### 4. Frontend: render cache, refresh in the background, re-read on change

`SeasonPage.tsx` keeps its URL-as-state and `useLatestRequest` generation guard. Two distinct effects, with different dependencies — this split is what keeps sort/filter changes from causing MAL fetches:

**Read effect** — depends on `[year, season, sort, inMyList]`. Calls `getSeasonPage(...offset 0)` and renders. This is the only thing sort and filter changes trigger.

**Refresh effect** — depends on `[year, season]` only:

1. Debounce ~400 ms, so arrow-stepping settles first.
2. `refreshSeason(year, season)` — not awaited by the render, with `refreshing` state driving a passive "Updating…" indicator.
3. On `Refreshed: true`, re-read `offset 0, limit = max(items.length, PAGE_SIZE)` in one call and replace `items`/`totalCount`. Replacing rather than appending preserves scroll position and the number of loaded pages; React reconciles by `animeId` key.
4. On rejection, clear `refreshing` and do nothing else — the cached page stays as it is, with no error surfaced.

If the cache is empty (`lastFetchedAt === null` and no items), the page stays in its loading state until the refresh resolves and step 3 lands, so a never-cached season shows "Loading…" rather than "No anime found". Every applied result is gated on `isLatest(requestId)` so a slow refresh for the previous season cannot overwrite a newer one.

*Alternative considered:* merging refreshed items into the existing array by id. Rejected — order can change (new anime, new popularity ranks), and a whole-prefix replace is both simpler and consistent with what the next infinite-scroll page would return.

### 5. My-score grouping is one SQL ordering; the divider is derived client-side

`SeasonRepository.GetPageAsync` for `SeasonSortKey.MyScore` becomes:

```csharp
query.OrderBy(a => a.MyScore == null ? 1 : 0)
     .ThenByDescending(a => a.MyScore)
     .ThenBy(a => a.PopularityRank == null || a.PopularityRank == 0 ? 1 : 0)
     .ThenBy(a => a.PopularityRank)
     .ThenBy(a => a.Title)
```

Scored rows sort first by score descending; every unscored row falls through to the same unranked-last popularity ordering the popularity sort uses. Because the order is server-side, it holds across pages.

The `Unwatched` divider needs no API field: the client renders it before the first accumulated item with `myScore === null`, and only when that item is not the first in the list. `myScore` is already on `AnimeBrowseItemDto`.

### 6. `includeMyList` filters server-side; `inMyList` joins the DTO

`AnimeBrowseItemDto` gains `bool InMyList`. The season projection sets it from `l.Anime.UserEntry != null`; `AnimeSearchService` (the other producer) sets it from the same `entries` dictionary lookup it already builds for `myScore` — note `MyScore` alone can't stand in for membership, since a plan-to-watch entry has no score.

`GetPageAsync` takes `includeMyList` (default `true`); when false it applies `.Where(l => l.Anime.UserEntry == null)` **before** `CountAsync`, so `totalCount` matches the filtered set and infinite scroll terminates correctly. The checkbox lives in the URL as `inMyList=0` (absent = checked, matching the default).

### 7. Layout: CSS grid for the results, three-zone header

- `.season-page__grid` → `display: grid; grid-template-columns: repeat(auto-fill, minmax(190px, 1fr)); gap: 20px;`. `1fr` tracks stretch to consume the row, so the right-hand gutter disappears at every width and covers get larger (190px+ vs 160px) while `.anime-card__picture`'s `aspect-ratio: 2 / 3` scales the image with them.
- `AnimeCard.css` gains `.anime-card--fluid { width: 100%; }`; `SeasonPage` passes it via the existing `className` prop, leaving the fixed 160px default for every other page that uses the card.
- The `Unwatched` divider is a grid child with `grid-column: 1 / -1` so it breaks the row cleanly.
- `.season-page__header` → `display: grid; grid-template-columns: 1fr auto 1fr;` with the title in column 1, a center cluster (`__nav` then `__jump`, in that order) in column 2, and the controls cluster (sort select, "In my list" checkbox) right-aligned in column 3. Below ~1024px it collapses to a wrapping flex column so the clusters stack instead of crushing.
- The "Updating…" indicator is passive text, not a control — placed next to the season label so it reads as a property of the season being viewed, and occupying reserved space (or positioned absolutely) so its appearance does not reflow the header.

## Risks / Trade-offs

- **NSFW entries now appear on the season page** → That is the point (MAL's own listing includes them, and the my-list fetch already does), but it changes what the page shows. If it turns out to be unwanted, it is a one-line revert of the query parameter, and a per-user toggle would be a separate change.
- **A daily fetch even for seasons whose data cannot meaningfully change (e.g. 1995)** → Accepted deliberately, in exchange for one rule with no season-class special cases. The cost is 3 calls per season per day, and only for seasons actually opened. Adding an age tier would reintroduce exactly the "frozen past season" class of bug this change exists to remove.
- **Mid-day changes are invisible until tomorrow, with no way to force a refresh** → The accepted cost of no refresh button. Season listings change slowly and the previous system already made this trade for the current season. If it ever bites, the fix is a shorter interval, not a control.
- **Arrow-stepping through many seasons could burst the MAL API on a day's first pass** → The ~400 ms client debounce means only the settled season is fetched; the MAL client's existing 403 backoff is the backstop.
- **Whole-prefix re-read on every completed refresh** → Costs one local Postgres read of the loaded rows; when data is unchanged React reconciles by key and nothing visibly moves. Scroll offset is preserved because the item count never shrinks below what was loaded (excluded my-list items are a separate, user-initiated re-query).
- **Results can shift a second or two after the page settles** → Inherent to cache-first. Mitigated by the passive "Updating…" indicator, which explains the shift before it happens.
- **`AnimeBrowseItemDto` gains a field consumed by two services** → Search must populate `inMyList` too or its results silently report "not in my list"; called out explicitly in tasks.
- **Waiting on the single-flight semaphore holds an ASP.NET request for the length of a full season fetch** → Bounded by the MAL client's own timeouts and at most one waiter per season in practice (browser tabs); the read path is unaffected, so a stuck refresh never blocks rendering.

## Migration Plan

No data migration. `SeasonFetchLog` is untouched and the day comparison is unchanged; only the season-class gate in front of it is removed.

One deployment wrinkle follows from keeping the daily rule. Cached seasons refresh on their next visit *only if their last fetch was on an earlier local date*. As of writing, three of the four cached seasons were fetched today:

```
2026 winter | 2026-07-27  ← already "fetched today"
2026 summer | 2026-07-27  ← already "fetched today"
2026 fall   | 2026-07-27  ← already "fetched today"
2026 spring | 2026-07-09  ← refreshes on next visit
```

Deploy on the same day those were fetched and they will sit un-backfilled until the next local day — still missing their NSFW entries and carrying frozen scores. With no refresh control, two ways through:

- Do nothing and let them heal overnight, or
- Clear the fetch log once so every season looks unfetched: `DELETE FROM "SeasonFetchLogs";`. Listing rows are untouched, so nothing is lost — each season simply re-fetches on its next visit.

The second is also what makes the change testable on the day it is written (see task 1.2), since otherwise a verification refresh returns `Refreshed: false`. Rollback is a code revert — nothing written by this change is unreadable by the old code.
