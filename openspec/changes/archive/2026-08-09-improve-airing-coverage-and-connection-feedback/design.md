## Context

Five of the six asks are small, self-contained edits to code that already exists; the sixth (connection status) needs a new app-wide mechanism, because the app currently has no way to say "something failed" outside of the pre-shell auth gate in `App.tsx`.

What's already in place:

- **Add-triggered airing refresh** — `UserAnimeEntryEditService` already enqueues onto `IAiringRefreshTrigger`, which `AiringRefreshTriggerBackgroundService` drains into `EpisodeScheduleRefreshService.RefreshOneAsync`. That path handles a finished show fine: `RefreshOneCoreAsync` resolves the AniList id, pulls the whole `airingSchedule`, and replaces the stored rows wholesale. The only thing stopping a finished show from using it is the `anime.AiringStatus is "currently_airing" or "not_yet_aired"` guard on line 83.
- **Related-anime backfill** — `POST /api/anime/{id}/related-anime/refresh` already exists and is already idempotent and capped (`MaxRelatedBackfillCount = 20`). Nothing about it needs to change; only *when* the client calls it does.
- **Duration and button visibility** — `formatDuration` and `hideAddToWatching` in `AnimeDetailPage.tsx`, a few lines each.
- **Health endpoint** — `GET /api/health` returns `{"status":"ok"}` and touches neither the database nor MAL, so it is a true "is the process up" probe.

The gap is error surfacing. Every page's `.catch()` is deliberately silent ("the page just keeps showing what it had"), which is the right per-page behavior but leaves the user with no signal at all. `App.tsx`'s "Can't reach the backend" screen only covers the very first `getMalAuthStatus()` call, before the shell mounts.

## Goals / Non-Goals

**Goals:**

- Every anime added to my list gets its episode airing dates fetched, with no dependence on its MAL airing status.
- One central place that knows whether the backend is reachable, fed by the two fetch helpers every API call already goes through, so no page has to opt in.
- Media types in the More overlay resolved before the user asks for them, without adding a per-visit cost once the data is cached.
- Airing-page navigation where any week of any year is reachable in two clicks.

**Non-Goals:**

- No retry-on-failure or request queueing in the client. The notice tells the user the backend is down; recovering the page's data is still a navigation or reload away. Automatic re-fetching of whatever each page was showing is a much larger change and is not attempted here.
- No offline mode, no service worker, no cached-response fallback.
- No per-request error toasts. One app-wide "backend unreachable" notice, not a message per failed call — a backend that is down fails many requests at once, and one notice per failure would bury the page.
- No distinction in the UI between "the backend is down" and "the backend is up but MAL is down". The backend surfaces upstream MAL failures as its own errors; splitting them would need new error-shape plumbing on every endpoint.
- No change to the AniList request pacer, the backfill cap, or the health endpoint itself.

## Decisions

### 1. The add-trigger loses its airing-status condition entirely, rather than gaining a "finished but never fetched" carve-out

`UserAnimeEntryEditService` currently enqueues only when `isNew && anime.AiringStatus is "currently_airing" or "not_yet_aired"`. It becomes just `isNew`.

The tempting refinement is to skip a finished anime whose `AnimeAiringSync.LastFetchedAt` is already set (the rule `GetFullRefreshTargetsAsync` uses when picking full-refresh targets). Rejected: it would put a database read on the add path to save one AniList call in the rare case where a user adds an anime the app has already fetched airing data for — and the add path is a write path we want to keep short. `RefreshOneCoreAsync` already skips the *id lookup* for an anime fetched before, so the repeat cost is a single `airingSchedule` query, paced like every other AniList call and executed on a background queue that the add response never waits on.

The wholesale-replace semantics make a redundant fetch harmless: a re-fetch of a finished show writes back the same rows.

*Alternative considered:* trigger on every entry edit, not just `isNew`. Rejected — a status change or an episode increment tells us nothing new about the airing schedule, and the elapsed-time pass plus the recheck queue already cover ongoing shows.

### 2. Duration formatting: hours and minutes at 60 minutes and up, `/ep` suffix unchanged

`formatDuration(seconds, totalEpisodes)` keeps its signature and its existing `/ep` rule (suffix unless `totalEpisodes === 1`); only the number formatting changes:

| Rounded minutes | Renders as |
| --- | --- |
| < 60 | `24 min` |
| ≥ 60, not a whole hour | `1h 55min` |
| ≥ 60, whole hour | `2h` |

Applying the threshold to the rounded per-episode average — not to `mediaType === 'movie'` — is what the user asked for and is also the more robust rule: a 90-minute OVA and a feature-length special get the same treatment as a movie, and a 24-minute episode of a movie-typed entry does not.

The `/ep` suffix composes onto the hour form (`1h 5min/ep`) rather than being suppressed. A long-episode series is exactly the case where "is this per episode or total?" is worth answering.

*Alternative considered:* `1 h 55 min` with spaces, matching the sub-hour form's space. Rejected for width — the info-box grid cells are narrow, and `1h 55min` is the conventional compact runtime form.

### 3. Related-anime backfill moves to a load-time effect, gated on a missing media type

Today `handleOpenMoreOverlay` opens the overlay and fires `refreshRelatedAnimeMediaTypes` together. The call moves into its own `useEffect` that runs after `detail` lands, and `handleOpenMoreOverlay` reduces to `setShowRelatedOverlay(true)`.

The effect only fires when `detail.relatedAnime.some(r => r.mediaType === null)`. This matters: the endpoint does up to 20 paced MAL calls (~1/s), so firing it unconditionally on every detail view would put a slow background request behind every page visit forever. Gated, it runs at most once per anime — after the first visit resolves the media types, later visits see them all cached and skip the call entirely. An anime with more than 20 uncached relations still converges, one page visit at a time, exactly as it does today one overlay-open at a time.

The effect must not block the render or the `loading` flag; it hangs off the same cancelled-flag pattern the detail load already uses, so navigating away before it resolves does not write to unmounted state. The server-side work continues either way, which is fine — the point of it is to warm the cache.

`relatedAnimeLoading` and the overlay's "Loading media types…" note stay, and now cover the case where the user opens **More** while the load-time backfill is still running.

*Alternative considered:* backfill only the relations that would land in the More overlay (excluding prequel/sequel/parent-story, which have their own buttons and don't display a media type). Rejected — the endpoint takes an anime id, not a relation list, and narrowing it would mean a new request shape for a saving that only exists when a franchise's *only* uncached relations happen to be its prequel and sequel.

### 4. Airing page: month + year selects replace the date input

The `<input type="date">` goes away. In its place, two selects labelled "Jump to":

- **Month** — the twelve months, labelled in the user's locale.
- **Year** — descending from the current year + 1 down to 1960, so recent years (the ones actually used) are at the top of the list and older ones are still reachable without leaving the control.

Changing either select navigates to the week containing the *same day-of-month* in the newly chosen month/year, clamped to that month's length (so 31 January → February resolves to the 28th/29th, not to March 2nd/3rd). Keeping the day means changing only the year lands on the same point in the year, which is the "quickly change the year" case from the report.

Both selects read their displayed values from the current reference date, so they stay in sync when the `‹ / current / ›` week buttons move the view — including across a month or year boundary.

Year floor of 1960 is a deliberate constant rather than something derived from the data: deriving it would need a new query for the earliest aired-from date in my list, and the resulting list would then change length as the list changes. 1960 predates any anime a MAL list is likely to contain.

*Alternative considered:* keep the date input and add `«`/`»` year-step buttons beside it. Rejected — reaching 2003 from 2026 is still 23 clicks, and the control row gets a fifth and sixth button.

*Alternative considered:* a free-text year input. Rejected — needs validation and a commit gesture (blur or Enter); a select is one click and cannot produce an invalid value.

### 5. Connection status: a module-level store fed by the fetch helpers, read through `useSyncExternalStore`

Every API call in the app goes through `fetchJson` or `fetchVoid` in `api/client.ts` (the four hand-rolled `fetch` calls for 204/404-tolerant reconciliation endpoints are the exception and get the same treatment). That is the natural choke point.

A new `api/connectionStatus.ts` holds a boolean plus a listener set, and exposes `reportReachable()`, `reportUnreachable()`, `subscribe(listener)`, and `getSnapshot()`. It is a plain module, not a React context — `client.ts` is not a React module and threading a context into it would mean either a provider-injected client or a global setter dressed up as a hook. `ConnectionStatusNotice` reads it with `useSyncExternalStore`, which is exactly what that hook is for.

**What counts as unreachable** is the load-bearing decision:

- `fetch` itself rejecting (a `TypeError` — connection refused, DNS failure, browser offline) → **unreachable**. This is the "backend is down" case.
- A `5xx` response → **unreachable**. The server is answering but is broken or its upstream is; either way the user's data isn't coming.
- Any `4xx` → **reachable**. A 404 for an anime we don't have, a 400 for an invalid edit, a 401 from an expired MAL token are all normal application outcomes that the calling page already handles. Treating them as an outage would flash the notice during ordinary use.
- Any `2xx` → **reachable**, and clears an existing notice.

So the rule is: *a response of any kind below 500 proves the backend is reachable.* Both helpers report on every call, success or failure, before the existing throw — the throw and each page's `.catch()` behavior are untouched.

**Clearing** happens two ways. Any subsequent successful request clears it (the user navigates, the page loads, the notice goes). And while unreachable, the notice component polls `GET /api/health` every 5 seconds; a success reports reachable and the notice disappears on its own. The poll runs only while the notice is showing, so a healthy session makes no extra requests at all. It goes through the same `fetchJson` path, so its own success is what reports the recovery — no separate reporting logic.

The notice is a fixed-position bar at the bottom of the viewport reading "Can't reach the server — retrying…", dismissible with an × for the user who wants it out of the way. Dismissal hides it for the current outage only; a later transition from reachable back to unreachable shows it again.

*Alternative considered:* wrap `window.fetch` globally. Rejected — it would also catch non-API requests (images from the MAL CDN, most obviously), and a MAL CDN hiccup is not a backend outage.

*Alternative considered:* a React context provider with the client calling a registered callback. Rejected as the same global mutable state with more indirection; `useSyncExternalStore` exists for precisely this shape.

### 6. `App.tsx`'s pre-shell error screen stays as it is

The full-page "Can't reach the backend" hero handles a failure of the very first `getMalAuthStatus()` call, when there is no shell to hang a notice off. The new notice covers everything after the shell mounts. Both can be reached in one session — first-load failure shows the hero, and a mid-session failure shows the bar — and they never show at the same time.

## Risks / Trade-offs

- **Adding a finished anime now costs an AniList round-trip that it didn't before** → It happens on a background queue that the add response never waits on, and it is paced by the existing `AniListRequestPacer` along with every other AniList call. Adding many shows in quick succession queues them rather than bursting.
- **A user who adds a large batch of finished shows generates a burst of queued AniList work** → Same pacer, and the work is exactly what the one-time backfill would have done anyway; this only moves it earlier. Nothing else is blocked behind that queue.
- **The load-time related-anime backfill puts a slow (up to ~20s) background request behind a first detail-page visit for a large franchise** → It is gated on a missing media type, so it runs once per anime, and it is fire-and-forget: the page renders and is fully interactive throughout. This is a strict improvement over paying the same cost with the overlay open and a spinner showing.
- **Treating every 5xx as "backend down" will show the notice for a single broken endpoint** → Acceptable and arguably correct: from the user's side, a 500 means the thing they asked for didn't happen. The next successful request anywhere in the app clears it, so a one-off 500 produces a brief notice, not a stuck one.
- **The 5-second health poll continues for as long as the app is open and the backend is down** → One request per 5 seconds against an endpoint that does no work, and only while the notice is showing. It stops the moment the backend answers.
- **Hiding "Add to watching" for a Watching entry removes a (currently harmless) no-op button** → That is the intent; the entry editor remains the way to change status to anything else, including back to Watching from On hold.

## Migration Plan

No schema change, no migration, no data backfill. Backend and frontend deploy independently:

- The backend change is one condition removal; deploying it early just means adds start fetching airing data sooner.
- The frontend changes are self-contained and depend on no new backend behavior — `GET /api/health` already exists.

Rollback is a revert of either side; the two do not depend on each other.

## Open Questions

None.
