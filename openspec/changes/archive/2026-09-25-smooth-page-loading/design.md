## Context

The user reports that the Season, Year, Top anime and Series pages have begun to flicker as they open, since the TMDB change. They flash "Loading…", sometimes an error, and on Series "No series match the selected filters", before the real page appears. The user asks whether TMDB is to blame. This section records what was measured and read on 2026-09-25, against the running Docker stack and the code at `fabb95d`.

**The backend is not slow.** Warm timings through nginx (`127.0.0.1:5173`): `/api/series/list` 33–45 ms (120 KB), `/api/year/2025` 26–30 ms (374 KB), `/api/top-anime` 11–15 ms (180 KB), `/api/season/2026/summer` 10–12 ms (77 KB). Database commands in the backend log run in 0–2 ms. The TMDB change (`ec2b71b`) touched none of these reads. On the frontend it touched only the picker, the detail and series pages and Settings.

**The flicker comes from how the frontend presents a read in progress:**

1. *Loading text from the first frame.* Every page renders "Loading…" as soon as `usePageData` reports `loading`. A 30–60 ms read therefore paints the text for 2–4 frames, which is visible as a flicker.
2. *The reveal count collapses to 0.* `SeasonPage`, `YearPage`, `SeriesBrowserPage`, `SearchPage` and `MyListPage` each copy one IntersectionObserver. Its callback runs `setVisibleCount(prev => Math.min(prev + N, list.length))`. While the list is loading the grid is empty, so the sentinel under it is on screen, the observer fires with `list.length === 0`, and the count becomes 0. When the data lands, `SeriesBrowserPage` derives its terminal state from `visibleItems.length === 0` with `items.length > 0`, which reads as `filtersEmpty`. It paints "No series match the selected filters" until the re-created observer fires and reveals 24. Season and Year derive their terminal state from the whole list, so they paint an empty grid for that interval instead.
3. *A refresh outcome decides the page before the read has landed.* `SeasonPageView` starts `usePageData`'s `GET /api/season/{y}/{s}` and, in parallel, the undebounced first `POST …/refresh`. A skipped refresh costs one `GetLastFetchedAsync` and usually answers before the listing read. The terminal state then sees `lastFetchedAt === null`, because the read hasn't landed, and `refreshOutcome === 'skipped'`, and yields `loadFailed`: "This season couldn't be loaded — it'll be retried next time you open it." The same callback also sees `lastFetchedAtRef.current === null` and issues a second full `GET` of the listing. `YearPage` has the same structure. The "Updating…" label flashes for the few milliseconds the skipped refresh takes.

Why it seemed new: nothing on these read paths changed, but the outcome of cause 3 depends on timing, as does how long cause 2's frame lasts. Rebuilding the stack for the TMDB change also restarted the backend. A cold backend compiles each EF query on first use, which widens every window above on the first visits after a rebuild.

**What TMDB did add is picture weight.** TMDB choices are stored and displayed as `https://image.tmdb.org/t/p/original/…`, and every surface draws them from that URL: grid cards, rows, strips, picker options and headers. Measured on the user's own picks: originals of 195 KB, 671 KB and 1,068 KB, and a 3840×2160 backdrop of 495 KB. A MAL `large` picture is 33 KB. The same images at `w780` weigh 192 KB, 205 KB, 305 KB and 47 KB. TMDB serves `w342`, `w500`, `w780` and `w1280` for posters and backdrops alike (verified). 25 anime and 23 series have a TMDB choice. Cached TV posters average 1,277 px wide, with a maximum of 2,000.

**Failure and offline states are inconsistent.** `usePageData` swallows errors ("a fresh load with nothing to show simply stays empty"), so a page cannot tell failed from empty:

- Home returns `null`, which leaves the page blank.
- Airing renders only its header.
- My list says "Nothing here yet".
- Search says "No anime found".
- Recap shows only its controls.
- Top anime keeps its rejected promise in `inFlightLoadByType`, so the page shows "Loading…" for the rest of the session and the rejection goes unhandled.
- Settings shows "Loading…" until a later app-status poll succeeds.
- The Profile sections show their empty messages.
- The first-load "Can't reach the backend" screen asks for a manual reload.

`ConnectionStatusNotice` already detects recovery, by polling `/api/health` every 5 s while the backend is unreachable, but nothing re-runs a failed page read when it recovers.

## Goals / Non-Goals

**Goals:**
- A read that completes quickly goes from the page header straight to content. No loading text, empty message, error or empty grid is painted in between.
- A slow read still shows a loading indicator, and it appears without a hard flash.
- A failed read is never presented as an empty result. Every data page has one failure state with Try again, and pages recover by themselves once the server is back.
- Season and Year read their cached listing once per visit and judge the refresh outcome against that read.
- TMDB pictures cost the browser a size fitted to where they are drawn, while their identity stays the `original` URL.
- Pictures arrive smoothly.

**Non-Goals:**
- Backend performance work. The reads are already 10–45 ms.
- Skeleton placeholders. A skeleton has to be delayed as well, or it flashes on a fast load like the text does, and it is a visual redesign nobody asked for. It could follow later on top of the delayed indicator.
- React Router data loaders, Suspense or transitions that keep the old route mounted until the new route's data is ready. That is an architectural change across every page and the page-state-restoration machinery.
- Changing what the connection notice treats as an outage. A 500 still marks the backend unreachable, as specified in `connection-status`.
- `srcset`/`sizes` responsive images (see D9).
- Changing the stored TMDB URL or anything the API sends.

## Decisions

### D1. One delayed, fading loading indicator for the whole app

A hook `useDelayedFlag(active, 300)` returns `true` only once `active` has stayed true for 300 ms. A component `LoadingNotice` renders nothing until then, and then renders the page's existing loading text, with the page's own class, inside a wrapper that fades in over 200 ms. A `prefers-reduced-motion: reduce` media query turns the fade off. Every "Loading…" line in the app moves to it:

- the pages: Season, Year, Series, Top anime, Search, My list, Recap, Profile, Settings, detail, series;
- the three overlays: `AnimeRankOverlay`, `EditHistoryOverlay`, `UpdatesHistoryOverlay`;
- `App.tsx`'s pre-shell line.

The Season and Year "Updating…" labels use `useDelayedFlag` directly.

- *Why 300 ms:* it is well above the measured warm reads, which finish in 10–45 ms plus render time, and still short enough that a slow load feels acknowledged. React's own Suspense throttling uses the same order of magnitude.
- *Why a fade rather than a minimum display time:* a minimum display time would hold content back once it has arrived, which the goal forbids. With the fade, an indicator that is replaced a few milliseconds after it starts is still nearly transparent, so there is no flash.
- *Alternative, a global top-of-page progress bar:* rejected. It is one more moving part in the chrome and would still flash on fast loads unless delayed the same way.

### D2. `usePageData` reports failure, retries on request, and retries once on reconnect

`UsePageDataResult` gains `failed: boolean` and `retry(): void`.

- **`failed`** is set when a load started with `showLoading`, that is a fresh load, rejects. It is cleared when any load for the key starts with `showLoading`, or succeeds. A silent reload failing (a restore's background refresh, or `reload()`) keeps the data and does not set `failed`, which matches today's behaviour. The key-change reset during render also resets `failed`.
- **`retry()`** is `runLoad(true)`. It also re-arms the automatic retry.
- **The automatic retry** comes from a subscription to `api/connectionStatus.ts`. On a listener call where `getSnapshot()` is now `true`, that is an unreachable-to-reachable transition, a hook whose `failed` is true and whose `autoRetryArmed` ref is true clears the ref and calls `runLoad(true)`. The ref is re-armed by `retry()` and by a key change. It is not re-armed by the automatic retry's own failure.

Classifying failures by status was considered, to retry only on 502/503/504 or a network error: nginx answers a stopped backend with 502. It was rejected because Vite's dev proxy answers a stopped backend with 500, the same status as an application error. "At most once until the user acts" breaks the only possible loop, where a persistent 500 marks the backend unreachable, the health poll marks it reachable, and the retry gets the 500 again, without guessing at status semantics.

Retrying with backoff while failed was rejected. The health poll is already the app's single recovery signal, and a per-page timer would hammer a failing endpoint in parallel with it.

`SeriesBrowserPage`'s own `.catch(() => ({ items: [], loadFailed: true }))` goes away. That put a failure into the restore snapshot as if it were data, and `failed` now covers it.

### D3. One failure block

`LoadFailedNotice({ what, onRetry })` renders "Couldn't load {what}." It also renders a second line, from `useSyncExternalStore(connectionStatus)`:

- while unreachable: "The server can't be reached — this will load by itself once it's back."
- otherwise: "Something went wrong on the server."

Below that is a **Try again** button, which uses the app's shared button styling and is centred in the page body.

Each surface passes a noun, for example "your list", "this week's schedule", "the series list", "this season" or "Top series". Profile sections render the same block, compact, inside the section. The existing failure texts of the detail, series and profile pages ("Couldn't load this anime." and so on) move onto it, and gain the retry.

### D4. Terminal states are decided from settled reads; Season and Year sequence their refresh after the read

The general rule, for every page: while `usePageData`'s `loading` is true for the read that decides the page, render `LoadingNotice` and no message. Messages come from the whole filtered list, never from the revealed slice.

For Season, and identically for Year, the order is:

1. The grid, when `displayed.length > 0`.
2. `failed`, which renders `LoadFailedNotice` ("this season").
3. The read not yet settled (`seasonData === null`), which renders `LoadingNotice`.
4. A cached listing exists (`lastFetchedAt !== null`), which renders `filtersEmpty` or `notListed`.
5. Nothing cached and the refresh unsettled or re-reading, which renders `LoadingNotice` with the text "Fetching this season from MyAnimeList…".
6. The refresh failed, which renders the MAL failure message, reworded to "This season couldn't be fetched from MyAnimeList — it'll be retried next time you open it." It has no button, because `season-browser` forbids a refresh control.

**The refresh is requested only once the read has settled successfully.** The refresh effect is keyed on the debounced season, as today, and additionally waits for `seasonData !== null` for that same season. It is guarded by a `refreshedForKey` ref, so it runs once per visit. When it gets a skipped outcome it re-reads only if the settled data has `lastFetchedAt === null`, the cross-tab case. `refreshOutcome` is set after that re-read lands, not before, so the terminal state never passes through `loadFailed` or `notListed` mid-sequence.

On a restore, `seasonData` is seeded, so the refresh is requested at once, as today. If the read fails, no refresh is requested. After a retry or reconnect succeeds, the effect sees data and requests the refresh then.

A refresh request that itself fails to reach the backend (a network error or ≥ 500 on the `POST`) is recorded as `failed`, as today. When nothing is cached, that shows the MAL message while the connection notice shows at the same time. This edge is accepted: it needs the backend to die in the few milliseconds between the read and the refresh.

- *Alternative, keep the two requests in parallel and fix only the terminal state:* rejected. The redundant re-read decision would still race. Sequencing costs one read's latency, 10–40 ms, in front of a MAL fetch that takes seconds, and costs nothing on the common skipped path.

### D5. One scroll-reveal hook

`useScrollReveal(sentinelRef, total, step, setVisibleCount)` owns the IntersectionObserver the five pages copy. It steps with `setVisibleCount(prev => (total === 0 || prev >= total ? prev : prev + step))`. It never lowers the count and never steps an empty list. Slicing handles a count above `total`. The initial and restored counts come from `useRestorableState` exactly as today, and `useCompleteLastRow` is untouched.

My list keeps its explicit reveal reset on control changes (the `bound-my-list-render` D3/D4 render-time identity check). Only the observer moves into the hook.

### D6. Holding content across a period step

A hook `useHeldData(data, key)` returns `{ shown, stale }`. When the key changes and `data` becomes `null`, `shown` stays the last non-null data and `stale` is true until the new data arrives. When a read fails, `shown` becomes `null`, so the failure state shows. `stale` combined with `useDelayedFlag(stale, 300)` drives a `--muted` class: `opacity: .5; pointer-events: none`, with the same look Top anime's muted list already has.

- **Season and Year** use `shown` for their grid and derived controls. The type options may briefly derive from the previous period's listing. That is invisible on a fast step and muted on a slow one.
- **Airing** holds the week.
- **Recap** keeps its own existing hold, which carries recap-specific filter-fallback logic, and Top anime keeps its module cache.

Scroll behaviour is unchanged. A step is a new history entry, `useScrollRestoration` scrolls to the top as it does now, and the new period opens on its first screenful. Holding the old grid for those milliseconds means the page height does not collapse under the scroll.

A back/forward restore is seeded from its snapshot, so there is nothing to hold.

### D7. Top anime

- `loadTopAnimeOnce` deletes its `inFlightLoadByType` entry on rejection as well as on success, and callers catch.
- The page keeps a `failedType` state. When it matches `selectedType`, the page renders `LoadFailedNotice` in place of the content, and the muted fallback gives way to it.
- Try again and a reconnect call the load again. The reconnect uses the same once-per-failure rule, through a small `useReconnectRetry(failed, retry)` hook that `usePageData` also uses internally, so the rule has one implementation.
- The first-arrival "Loading…" becomes `LoadingNotice`.

### D8. Settings and the pre-shell screen

- **Settings:** `AppStatusProvider` gains `failed`, true when a read failed and no status is held, and it refreshes at once on the reconnect transition rather than waiting for its next 10 s tick. The Settings page renders `LoadFailedNotice` ("settings") when `failed`, instead of staying on "Loading…".
- **`App.tsx`:** while `statusError` is true, a 5 s interval, the same `HEALTH_POLL_INTERVAL_MS` value, re-reads `getMalAuthStatus()`, and a Try again button does so at once. Success clears `statusError` and the app continues. The copy changes from "…then reload this page" to "Retrying…". The pre-shell "Loading…" becomes `LoadingNotice`.

### D9. TMDB display widths: a frontend mapping, four tiers, fall back to original

`utils/anime.ts` gains `displayPictureUrl(url, tier)`. For a URL starting with `https://image.tmdb.org/t/p/original/` it swaps `original` for the tier's width. Any other URL, MAL's included, is returned unchanged. The tiers follow the rule "the smallest TMDB width at least twice the widest CSS width the surface draws", which is the `tmdb-artwork` rule over `w342`, `w500`, `w780` and `w1280`. The content column reaches 2,400 px, so a six-column grid card reaches about 375 px.

Settled by measuring (task 7.1), with the tiers below the table:

| Tier | Width | Covers up to | Surfaces |
|---|---|---|---|
| `row` | `w342` | 171 px | `RowPicture` by default (every row slot up to 78 px tall, whose wide cap is 139 px), `SeriesEntryRow`, `Updates/UpdateCard` (capped at 170 px) |
| `tile` | `w500` | 250 px | the four profile strips, picker options, and `RowPicture` in the two tallest slots (`airing-today__thumb`, `completion-score__picture`) |
| `card` | `w780` | 390 px | `PosterPicture` by default: grid cards, dashboard cards, Top anime cards, airing band, score board, More tiles, timeline cards |
| `hero` | `w1280` | 640 px | the detail page picture, the series page header, and the recap podium, passed as `tier="hero"` |

Measured maximum rendered widths, in CSS px, at a 2,400 / 1,024 / 390 px viewport. A wide picture is the widest case for a surface whose box follows its shape.

| Surface | 2,400 | 1,024 | 390 | Tier |
|---|---|---|---|---|
| Row slots, poster (52 x 72) | 52 | 52 | 52 | `row` |
| Row slots, wide picture (72 tall, My list and Top anime) | 128 | 128 | 128 | `row` |
| Row slots, wide picture (78 tall, edit history, unresolved) | 139 | 139 | 139 | `row` |
| Row slot, wide picture (108 tall, airing today) | 192 | 192 | 192 | `tile` |
| Row slot, wide picture (136 tall, completion score) | 242 | 242 | 242 | `tile` |
| Update card picture | 170 | 170 | 170 | `row` |
| Profile strips | 219 | 81 | 18 | `tile` |
| Picker options | 249 | 249 | 249 | `tile` |
| Season, Year, Search, Series grid cards | 375 | 149 | 162 | `card` |
| Dashboard carousel cards | 311 | 160 | 160 | `card` |
| Top anime cards / showcase | 304 / 158 | 107 / 158 | 144 / 140 | `card` |
| Airing band | 323 | 127 | 165 | `card` |
| Score board tiles | 172 | 177 | 292 | `card` |
| Series page timeline cards | 192, wide 384 | 192, wide 384 | 192, wide 384 | `card` |
| Series page More tiles | 177 to 371 | 173 to 364 | 301 | `card` |
| Recap podium | 423 | 192 | 314 | `hero` |
| Detail page picture (wide picture) | 400 | 360 | 332 | `hero` |
| Series header (wide picture) | 420 | 389 | 320 | `hero` |

The two facts that moved the table from the three tiers first drafted: a profile strip and a picker option are wider than a `row` covers but well under a `card`, and the detail page, series header and recap podium each draw wider than a `card` covers. The Top anime showcase and the More tiles, which were candidates for `hero`, stay under 390 px.

`PosterPicture` and `RowPicture` compute `displaySrc` once, through the `useDisplayPicture(src, tier)` hook, and use it for both the art and the fill, so the fill still costs no second request. The hook remembers which `src` has fallen back: an `onError` on the art while `displaySrc !== src` switches to `src`, the original, and a different `src` starts on the fitted rendition again. The detail page, the series header, `SeriesEntryRow`, `UpdateCard` and the picker options, which draw their own `<img>`, use the same hook.

Everything that is identity keeps the `original` URL: the picker's `onPick(option.url)` and its `selected` comparison, `SelectedPictureUrl`, DTOs, transfer and backup. Only `<img src>` changes.

Alternatives considered:

- *The backend sends sized URLs.* Rejected. The URL is the choice's identity for validation, the picker's current-picture mark and device transfer. Changing it means the D9 rewrite of stored choices from `add-tmdb-image-source`, and display size is a presentation concern.
- *`srcset` with `sizes`.* Deferred. It would serve `w500` to a typical 1,440 px laptop, but it needs an accurate `sizes` expression per surface, and some surfaces change width once the picture's shape is known, which would re-select a candidate after load. The fixed tiers are predictable and already give a 3–10× reduction.
- *`sizes="auto"`.* Rejected. Browser support is incomplete and it only works with `loading="lazy"`.

### D10. Picture arrival fade

`useOrientationPicture` already distinguishes an image that is `complete` when its ref attaches from one that finishes loading later. It gains a second return value, `arrival: 'held' | 'pending' | 'loaded'`. `held` means drawn at once: the image was `complete` when the ref attached, or its `load` event came within `PICTURE_FADE_DELAY_MS` (150 ms) of attaching. `loaded` is a later arrival, which fades. The fade is for a picture that was visibly slow. On a quick one, a disk-cached picture on a fresh page load most of all, it only adds its own 180 ms to the wait. The browser reports a disk-cached image asynchronously, so `complete` alone would have faded every such picture.

`PosterPicture` puts a class on the wrapper it already renders. `RowPicture` renders a bare `<img>` that is its own slot, and an `<img>`'s opacity takes its own background with it, so the placeholder could not show through. `RowPicture` therefore wraps the `<img>` in a `row-picture-frame` that carries the host's class (which only ever sets `--row-picture-*` and a radius) and the placeholder background, and the classes go on that frame:

- `pending`: the art has `opacity: 0`, and the box's placeholder background shows.
- `loaded`: the art and the fill fade from `opacity: 0` over 180 ms, when the picture took longer than the delay. This is a keyframe animation rather than a transition, because the fill is mounted only once the art has loaded, so it has no earlier value to transition from and one rule fades both.
- `held`: `opacity: 1` at once, with no animation. The art is still invisible until it has loaded, so a picture is never drawn part-way; the delay only decides whether the reveal is animated.

A `prefers-reduced-motion` rule drops the animation. An `error` event leaves the picture `pending`, so the placeholder shows. The fallback in D9 retries first.

Nothing about the box geometry changes, so `artwork-presentation`'s "a grid of posters does not move" still holds. The detail and series headers are not covered by this requirement and are left as they are.

## Risks / Trade-offs

- [A 300 ms blank body on a slow first load reads as "nothing happening"] → the header and controls are already there, and the indicator follows at 300 ms. Tune the constant in one place if it feels late.
- [Holding the previous period briefly shows its content under the new period's label] → it is invisible on a fast step, and muted and not interactive after 300 ms on a slow one. Restores are unaffected.
- [One automatic retry can miss a flapping recovery: the server comes back, the retry fails, and the server comes back again] → the failure state keeps Try again. This was accepted over any retry loop.
- [TMDB could stop serving a width] → the `onError` fallback loads `original`. Every width used was verified for posters and backdrops on 2026-09-25.
- [`w780` is about 6× MAL's 33 KB for a TMDB poster] → it is the price of the sharper pictures the TMDB change was made for. It is still about 3× lighter than today, and 10× lighter for backdrops. `srcset` stays open as a later refinement.
- [The refresh now waits for the read] → it adds 10–40 ms to a MAL fetch that takes seconds. The skipped path, which is most visits, is unaffected.
- [An unreachable-to-reachable transition fires a burst of retries across mounted sections, such as Profile's six reads] → they are the same reads a fresh visit makes, and `fetchRaw` de-duplicates identical in-flight GETs.

## Migration Plan

Frontend only: rebuild the frontend image (`docker compose up -d --build frontend`) or run `npm run build`. There is no data migration, no API change and no backend restart. Rollback is redeploying the previous frontend bundle.

## Open Questions

None blocking. The 300 ms delay and the 180/200 ms fades are starting values, to be confirmed with the user once they can see them.
