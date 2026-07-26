## Context

The "+" button (`IncrementButton`) is already a single shared component, but each of its three call sites owns its own increment logic:

- `CurrentlyWatchingCarousel.tsx` → `updateEntry(...)` then `onEpisodesWatchedChange(animeId, saved.episodesWatched)`, which `HomePage` uses to patch the episode count in place.
- `MyListPage.tsx` → `updateEntry(...)` then `handleSaved(animeId)(saved)`, patching the row's entry.
- `AnimeDetailPage.tsx` → `updateEntry(...)` then `setDetail(prev => ({...prev, entry: saved}))`.

All three send `{ episodesWatched: current + 1 }` to the same endpoint. The backend (`UserAnimeEntryEditService.ApplyEpisodesWatched`) already flips the entry to `Completed` and stamps `CompletedAt` when the new count equals `AnimeMetadata.TotalEpisodes` and the entry was not already Completed — and the returned `UserAnimeEntryDto` carries the resulting `status`. So the client can detect completion from the response alone; nothing new is needed server-side.

The gap is purely client-side: nothing reacts to that status flip. The patched-in-place state also means the dashboard keeps rendering a show the backend no longer considers "Watching" (`MainDashboardService` filters on `Status == Watching`).

The app already has the pattern to copy: `EntryEditorProvider` mounts one `EntryEditorOverlay` at the app root and hands out an `openEditor(target)` function through context, with the overlay built on the shared `Modal` (Esc + click-outside close).

## Goals / Non-Goals

**Goals:**
- One code path for "+"-driven increments, so the completion prompt is automatic at every current and future "+" site.
- A small overlay: anime picture left, score dropdown right, save + skip.
- The triggering view ends up consistent with the server after the prompt closes, by either path.
- Reuse the existing `Modal`, `updateEntry`, and score-dropdown conventions rather than inventing new ones.

**Non-Goals:**
- No backend, DB, or MAL-API change.
- No prompt when completion happens through the entry editor overlay (that form already shows a score field) or through a MAL-side reconciliation.
- No new "rate this" surface elsewhere (profile, history, notifications).
- No change to the auto-complete rule itself, including its unknown-total-episodes and already-Completed carve-outs.

## Decisions

### Detect completion from the edit response, not from local arithmetic
The client compares the pre-increment `entry.status` with `saved.status` from the `updateEntry` response and prompts only on `!== 'Completed'` → `=== 'Completed'`.

*Why:* the server owns the auto-complete rule (including the unknown-total and already-Completed carve-outs). Re-deriving "watched + 1 === total" on the client would duplicate that rule and drift from it — and would misfire when the cached total episode count is stale. Alternative considered: prompting on the client-side count comparison; rejected for that duplication.

Note that the carousel's `CurrentlyWatchingItemDto` has no `status` field. Its items are by construction all `Watching`, so the pre-increment status there is known to be `Watching`; the increment call passes that as the previous status explicitly rather than the hook guessing.

### A `CompletionPromptProvider` + `useEpisodeIncrement()` hook, mirroring `EntryEditorContext`
New `frontend/src/context/CompletionPromptContext.tsx` mounts a single `CompletionScoreOverlay` at the app root (next to `EntryEditorProvider` in `AppShell.tsx`) and exposes:

```ts
type IncrementTarget = {
  animeId: number
  animeTitle: string
  pictureUrl: string | null
  episodesWatched: number      // pre-increment count
  previousStatus: WatchStatus
  currentScore: number | null
  onSaved: (entry: UserAnimeEntryDto) => void  // local patch, as today
  onCompleted?: () => void                     // re-fetch, called once the prompt closes
}
useEpisodeIncrement(): (target: IncrementTarget) => Promise<void>
```

The hook does the `updateEntry` call, calls `onSaved`, and on a completion transition opens the overlay; `onCompleted` fires when the overlay closes.

*Why a hook that owns the request, rather than a bare `openCompletionPrompt()` the call sites invoke themselves:* the requirement is that the prompt cannot be forgotten at a new "+" site. If the detection lives in the hook that performs the request, adding a "+" somewhere means calling one function and getting the behavior; if it lives in the call site, the fourth "+" will silently skip it. Alternative considered: putting the whole thing inside `IncrementButton`; rejected because the button doesn't know the entry and the carousel uses it outside a `ProgressBar`, so the props would have to be threaded through anyway — and the button should stay a dumb control.

*Why context rather than a plain module-level store:* matches `EntryEditorContext`, keeps a single overlay instance, and avoids a second modal-stacking mechanism.

The per-site "is an increment in flight" state (`pendingIncrementId`, `incrementPending`) stays at the call sites — it drives per-row button disabling, which is a rendering concern the provider shouldn't own.

### The overlay saves the score through `updateEntry(animeId, { myScore })`
*Why:* that path already writes the ActivityLog row and triggers the debounced MAL sync (the "Edits log activity and trigger sync" requirement). A dedicated endpoint would duplicate it. On failure the overlay shows an error and stays open — matching `EntryEditorOverlay`'s behavior — rather than silently dropping the score.

`myScore: 0` is the existing "no score" encoding in both `EntryEditorOverlay` and the my-list row dropdown, so the dropdown reuses it. Picking "No score" when the entry had no score is a no-op save; the overlay skips the request in that case and just closes.

### Refresh = the triggering view re-fetches, not `window.location.reload()`
Each call site passes an `onCompleted` that re-runs its own loader: `HomePage` re-runs `getDashboard()`, `MyListPage` re-runs `getMyList()`, `AnimeDetailPage` re-runs its existing `load()`.

*Why:* a full page reload would throw away scroll position, the my-list sort/grouping selection, and the carousel position, and would flash the whole shell — for a one-row change. Re-fetching the page's own data satisfies the requirement ("the show is now watched so it won't be in home screen") without that cost. `AnimeDetailPage` already has `load()`; `MyListPage`'s fetch moves into a `useCallback` so it can be re-run; `HomePage` gains an `onDashboardChanged` callback and its current `handleEpisodesWatchedChange` patch stays for the non-completing case (so ordinary increments don't refetch the whole dashboard).

### Prompt even when the entry already has a score
The dropdown pre-selects the existing score instead of suppressing the prompt.

*Why:* a score set months ago while watching is exactly what someone may want to revise on finishing, and suppressing the prompt for scored entries makes the behavior feel arbitrary. Dismissing costs one Esc. Alternative considered: only prompting when `myScore` is null; rejected as surprising.

### Layout
`Modal` + a flex row: `<img>` at a fixed width (~96px, poster aspect) on the left, and on the right the title, a `<select>` with `No score` + 1–10, and a `Skip` / `Save` button pair matching `EntryEditorOverlay`'s button row. A missing `pictureUrl` renders the same placeholder block my-list rows use. New `CompletionScoreOverlay.tsx` + `.css` under `frontend/src/components/`, following the existing per-component CSS convention.

## Risks / Trade-offs

- **A prompt appearing on a background increment feels intrusive** → it only fires on an explicit "+" click that finishes a show, which is the moment the user is most likely to want to rate it; Esc/click-outside dismisses with no save.
- **Two round trips on the completing increment (edit, then score)** → acceptable: it is one interaction per finished show, and folding the score into the increment request isn't possible since the score is chosen after.
- **`onCompleted` re-fetch could clobber an in-flight increment on another row of my list** → the refetch replaces the whole list from the server, which is the authority; the per-row pending flag already blocks a second increment on the same row, and a concurrent increment on a different row lands server-side either way, so the refreshed list is correct.
- **The carousel's assumed `Watching` previous status** → true today by the dashboard's own filter (`Status == Watching`); if the dashboard ever includes other statuses, the DTO must carry `status` and the call site pass it. Called out in the tasks so the assumption is written down at the call site.
- **A stale cached total episode count means the backend completes at the wrong episode** → pre-existing behavior, unchanged here; the prompt just becomes visible evidence of it, which is arguably an improvement.

## Open Questions

- Should the prompt also offer a quick "add to favorites"/rewatch shortcut later? Out of scope now; the overlay is small enough to extend.
