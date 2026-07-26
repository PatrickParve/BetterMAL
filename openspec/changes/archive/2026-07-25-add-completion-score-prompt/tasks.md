## 1. Overlay component

- [x] 1.1 Add `frontend/src/components/CompletionScoreOverlay.tsx`: built on `Modal`, takes `{ animeId, animeTitle, pictureUrl, currentScore, onClose }`, renders the picture (or the my-list placeholder block when `pictureUrl` is null) on the left and title + score `<select>` ("No score" = 0, then 1–10, pre-selected from `currentScore`) on the right.
- [x] 1.2 Wire the overlay's save: call `updateEntry(animeId, { myScore })` on confirm; skip the request and just close when the pick equals the entry's existing score (including "No score" on an unscored entry); on failure show an error and keep the overlay open, mirroring `EntryEditorOverlay`.
- [x] 1.3 Give Skip / Esc / click-outside the same close path as save-success, so the caller's close handling runs exactly once either way.
- [x] 1.4 Add `frontend/src/components/CompletionScoreOverlay.css` following the existing per-component CSS convention (poster ~96px wide on the left, button row matching `EntryEditorOverlay`).

## 2. Shared increment path

- [x] 2.1 Add `frontend/src/context/CompletionPromptContext.tsx` with a `CompletionPromptProvider` that mounts one `CompletionScoreOverlay` instance, mirroring `EntryEditorContext`.
- [x] 2.2 Export `useEpisodeIncrement()` from it, returning an async `increment(target)` that calls `updateEntry(animeId, { episodesWatched: episodesWatched + 1 })`, invokes `target.onSaved(saved)`, and opens the prompt only when `previousStatus !== 'Completed' && saved.status === 'Completed'`.
- [x] 2.3 Have the hook call `target.onCompleted?.()` once the overlay closes by either path, and not at all when no prompt was shown; keep the existing swallow-and-leave-count-as-is behavior when `updateEntry` throws.
- [x] 2.4 Mount `CompletionPromptProvider` in `frontend/src/AppShell.tsx` alongside `EntryEditorProvider`.

## 3. Call sites

- [x] 3.1 `MyListPage.tsx`: move the list fetch into a re-runnable `useCallback` loader, and replace `incrementEpisodes`'s direct `updateEntry` with `useEpisodeIncrement()`, passing the row's `pictureUrl`, `entry.status`, `entry.myScore`, the existing `handleSaved` as `onSaved`, and the loader as `onCompleted`. Keep `pendingIncrementId` for per-row button disabling.
- [x] 3.2 `AnimeDetailPage.tsx`: route `handleIncrement` through the hook, passing `detail.pictureUrl`, `detail.entry.status`, `detail.entry.myScore`, the existing `setDetail` patch as `onSaved`, and the existing `load` as `onCompleted`. Keep `incrementPending`.
- [x] 3.3 `CurrentlyWatchingCarousel.tsx`: route `increment` through the hook. Pass `previousStatus: 'Watching'` with a comment noting it holds because `MainDashboardService` filters currently-watching on `Status == Watching`; `currentScore` is not on `CurrentlyWatchingItemDto`, so pass `null` (the dashboard carousel has no score to pre-fill). Add an `onCompleted` prop for the page to supply.
- [x] 3.4 `HomePage.tsx`: extract the dashboard fetch into a re-runnable loader and pass it to the carousel as the completion refresh callback, keeping `handleEpisodesWatchedChange` for ordinary non-completing increments.
