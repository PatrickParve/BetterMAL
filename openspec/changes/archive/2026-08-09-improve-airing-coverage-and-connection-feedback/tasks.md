## 1. Airing data on add, regardless of airing status

- [x] 1.1 In `backend/AnimeTracker.Api/Services/Entries/UserAnimeEntryEditService.cs`, drop the `anime.AiringStatus is "currently_airing" or "not_yet_aired"` condition from the fire-and-forget enqueue so it reads `if (isNew) airingRefreshTrigger.Enqueue(animeId);`, and update the comment above it — the reason is no longer "an airing/upcoming anime", it is "any anime just added gets its episode history fetched now rather than waiting for the backfill".
- [x] 1.2 Confirm the `isNew` guard stays: an edit to an existing entry must not trigger a fetch.
- [x] 1.3 Verify `EpisodeScheduleRefreshService.RefreshOneCoreAsync` needs no change for a finished anime — it resolves the AniList id (or reuses the cached one), pulls the full `airingSchedule`, and `ReplaceForAnimeAsync` writes the rows wholesale.
- [x] 1.4 Manually add a finished show that has never been fetched and confirm its `EpisodeAirings` rows and `AnimeAiringSync` row appear shortly after the add, with the add response returning immediately.

## 2. Duration in hours and minutes

- [x] 2.1 Rewrite `formatDuration(seconds, totalEpisodes)` in `frontend/src/pages/AnimeDetailPage.tsx`: round to whole minutes, then render `<m> min` below 60, `<h>h <m>min` at 60 and above, and `<h>h` when the remainder is 0. Keep the existing `/ep` suffix rule (append unless `totalEpisodes === 1`) so it composes onto both forms.
- [x] 2.2 Update the comment above the function — it currently only explains the `/ep` rule; add why the hour threshold is on the rounded duration rather than on `mediaType`.
- [x] 2.3 Verify against real pages: a 24-min series (`24 min/ep`), a 115-min movie (`1h 55min`), a 120-min movie (`2h`), a 65-min-per-episode anime (`1h 5min/ep`), and one with no stored duration (`No info`).

## 3. Hide "Add to watching" for an anime already being watched

- [x] 3.1 In `AnimeDetailPage.tsx`, extend `hideAddToWatching` to include `"Watching"` alongside `"Completed"` and `"Dropped"`.
- [x] 3.2 Verify the two-button layout still lays out correctly when only Edit sits in the top row (`flex: 1 1 0` on a single child), and check it on a Watching, a Completed, and a Plan-to-watch anime.

## 4. Related-anime media types fetched on page load

- [x] 4.1 In `AnimeDetailPage.tsx`, reduce `handleOpenMoreOverlay` to `setShowRelatedOverlay(true)` and rename it accordingly (its comment about opening-then-backfilling no longer applies).
- [x] 4.2 Add a `useEffect` keyed on `detail` that calls `refreshRelatedAnimeMediaTypes(animeId)` only when `detail` is non-null and `detail.relatedAnime.some(r => r.mediaType === null)`, guarding writes with the same cancelled-flag pattern the detail-load effect uses so an unmount mid-flight doesn't set state.
- [x] 4.3 Make sure the effect can't loop: merging the refreshed list back into `detail` changes `detail`, so the effect must depend on something that settles — key it on `animeId` plus a ref/flag marking "backfill already attempted for this anime", not on `detail` identity.
- [x] 4.4 Keep `relatedAnimeLoading` driving the overlay's "Loading media types…" note, now set by the load-time effect rather than by the open handler.
- [x] 4.5 Verify: a franchise with uncached relations starts the POST on page load and resolves the types without the overlay being opened; opening More mid-flight shows the loading note and updates in place; a fully-cached anime issues no POST at all (check the network tab); navigating away mid-flight logs no React warning.

## 5. Airing page month/year jump controls

- [x] 5.1 In `frontend/src/pages/AiringPage.tsx`, remove the `<input type="date">` and add a "Jump to" cluster of two `<select>`s — month (twelve locale-labelled months) and year (current year + 1 down to 1960, most recent first).
- [x] 5.2 Derive both selects' values from `referenceDate` by slicing the ISO string (not `new Date(iso)` — the existing `toLocalIso` comment explains why bare-ISO parsing lands on the wrong local day).
- [x] 5.3 Add a helper that builds the target ISO date from a chosen year/month plus the current day-of-month, clamping to the target month's last day (31 Jan → Feb yields the 28th/29th), and route both `onChange` handlers through it into the existing `goToWeek`.
- [x] 5.4 Build the month labels from `toLocaleDateString(undefined, { month: 'long' })` over a fixed reference year so they follow the viewer's locale, matching how the page already formats its week range.
- [x] 5.5 In `frontend/src/pages/AiringPage.css`, replace `.airing-page__jump input` styling with equivalent `select` styling (same border/radius/padding/hover as the nav buttons), and make sure the two selects sit side by side without wrapping the header row at normal widths.
- [x] 5.6 Verify: selecting a year 7 years back lands on the same point in that year; the selects update when the `‹ / current / ›` buttons cross a month or year boundary; a 31st-of-the-month week jumping to February clamps; and the week still survives back-navigation from a detail page.

## 6. Connection-status store

- [x] 6.1 Add `frontend/src/api/connectionStatus.ts`: a module-level boolean plus a listener `Set`, exposing `subscribe(listener)`, `getSnapshot()`, `reportReachable()`, and `reportUnreachable()`. Notify listeners only on an actual transition, so a healthy session's every-request `reportReachable()` costs nothing.
- [x] 6.2 Document in a header comment why this is a plain module and not a React context: `client.ts` is not a React module, and `useSyncExternalStore` is the intended consumer.
- [x] 6.3 In `frontend/src/api/client.ts`, wrap `fetchJson` and `fetchVoid` so every call reports: `catch` around the `fetch` itself → `reportUnreachable()` and rethrow; `res.status >= 500` → `reportUnreachable()`; any other response → `reportReachable()`. Leave the existing throw messages and return values exactly as they are.
- [x] 6.4 Apply the same reporting to the four hand-rolled `fetch` calls in `client.ts` (`getPendingReconciliationDiff`, `acceptReconciliationDiff`, `cancelReconciliationDiff`, and any other direct `fetch`) — their 204/404 paths must report *reachable*, not unreachable.
- [x] 6.5 Add a `getHealth()` client function hitting `GET /api/health` through `fetchJson`, so its own success/failure feeds the store with no separate reporting logic.

## 7. Connection-lost notice

- [x] 7.1 Add `frontend/src/components/ConnectionStatusNotice.tsx` reading the store with `useSyncExternalStore(subscribe, getSnapshot)`, rendering nothing while reachable.
- [x] 7.2 While unreachable, render a fixed-position bar reading "Can't reach the server — retrying…" with an × dismiss button; hold the dismissal in component state and reset it on the unreachable → reachable → unreachable transition so a new outage shows the bar again.
- [x] 7.3 Add an effect that polls `getHealth()` every 5s while unreachable and clears the interval as soon as the state flips back (or the component unmounts), so a healthy session issues no polls.
- [x] 7.4 Add `ConnectionStatusNotice.css` — fixed to the bottom of the viewport, above page content, using the existing `--border` / `--accent-*` / `--text` variables and the border/radius conventions the other components use; make sure it does not sit under the navbar or cover the currently-watching carousel's controls.
- [x] 7.5 Mount the notice in `frontend/src/AppShell.tsx`, inside the providers and outside `<Routes>`, so it survives navigation. Leave `App.tsx`'s pre-shell "Can't reach the backend" hero untouched.
- [x] 7.6 Verify by stopping the backend mid-session: the bar appears on the next request, one bar only across several failing requests, × dismisses it, restarting the backend clears it within ~5s with no interaction, and a 404 (open a nonexistent anime id) never triggers it.

## 8. Verification and docs

- [x] 8.1 Build the backend through the `sdk:10.0` Docker image and the frontend with nvm's node v22; both clean, and `npm run lint` passes.
- [x] 8.2 Re-walk the detail page end to end: duration forms, the button set for each status, and the load-time related-anime backfill.
- [x] 8.3 Re-walk the airing page: month/year jumps, week buttons, clamping, and back-navigation.
- [x] 8.4 Update `CODE_GUIDE.md` for the new `connectionStatus` store and notice component, the widened add-trigger, and the moved related-anime backfill.
