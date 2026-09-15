## Context

The proposal has the evidence and line references. This section covers only what shapes the design. Paths without a prefix are under `frontend/src/`. Backend paths start with `backend/AnimeTracker.Api/`.

**How a completion from a progress row works today.**
- Every "+" press and inline count edit goes through `setEpisodesWatched` in `context/CompletionPromptContext.tsx:39-76`. It sends `updateEntry(animeId, { episodesWatched })`.
- On failure it calls `reportFailure` and returns. The promise resolves either way (`:41-52`).
- On success it calls `target.onSaved(saved)` (`:53`). Then it may open the prompt, which calls `target.onCompleted(saved | null)` when it closes, however it closes (`:74`, `:83-87`).
- The three callers:
  - `CurrentlyWatchingCarousel` (Home): `onSaved` patches only the card's count through `HomePage.handleEpisodesWatchedChange` (`pages/HomePage.tsx:17-31`). `onCompleted` is `reload`.
  - `MyListPage`: `onSaved` patches the whole entry (`pages/MyListPage.tsx:85-93`). My List groups rows client-side by `item.entry.status` (`:475-478`), so a completed row moves to Completed from the patch alone. `onCompleted` is `reload`.
  - `AnimeDetailPage`: `onSaved` patches the entry. `onCompleted` patches the saved entry and ignores `null` (`pages/AnimeDetailPage.tsx:315-336`).

**What the server does with the edits the revert sends** (`Services/Entries/UserAnimeEntryEditService.cs`). The steps run in a fixed order: `ApplyEpisodesWatched`, `ApplyStatus`, `ApplyDates`, `ApplyScore`, `ApplyRewatchCount` (`:60-64`).
- `ApplyEpisodesWatched`'s automatic transitions run only when the request has no status, or the same status as before (`:213`):
  - Reaching the total from a status other than Completed completes the entry. It fills `CompletedAt` if empty, and raises `RewatchCount` by one if the entry was Rewatching (`:221-228`).
  - Dropping a Completed entry below the total sends it to Rewatching if eligible, otherwise Watching (`:236-240`). The entry is still `Completed` when that check runs, so `HasFinishedOnce` is always true (`RewatchingEligibility.cs:21-22`). On an anime that has aired in full, the result is always Rewatching.
- If the request's status differs from the stored one, `ApplyStatus`'s last branch sets it and leaves the count alone (`:309-314`). Only the Rewatching branch resets the count (`:301-308`).
- `ApplyDates` applies `completedAt` whenever the key is present. Sending the stored value changes nothing (`:340-344`).
- `ApplyRewatchCount` applies on its own, whatever the status did (`:372-389`).
- `ApplyScore` does nothing when the score is unchanged (`:357-358`). With no changes, the edit writes no activity, doesn't queue a sync and doesn't place a ranking (`:89-111`). It still returns the entry.

**Constraints.**
- The frontend has no test runner; `package.json` has only `build` and `lint`. Frontend checks are done by hand, as in `fix-silent-actions-and-mal-status-recheck`. Backend behaviour gets xUnit tests.
- The default `node` is v16. Vite needs nvm's v22.
- The local .NET SDK is 9.0. The backend is built and tested in `mcr.microsoft.com/dotnet/sdk:10.0` from a copy under `/private/tmp`.
- The brief asks for one commit covering both items.

## Goals / Non-Goals

**Goals:**
- A rewatch with no score opens the completion prompt. A rewatch with a score doesn't.
- For every caller, `onCompleted` runs only when the prompt closes with a saved score. Save counts as a saved score even when the score wasn't changed.
- "Skip" is renamed "Cancel".
- The Home carousel knows each card's score and its finish date before completion.
- On Home, lowering a card's count after a completion that was left uncommitted puts the entry back as it was before: status, rewatch count and finish date, in one edit.
- The detail page keeps its current behaviour. My List applies the saved entry from the prompt instead of reloading (D11). Neither reloads after Cancel.

**Non-Goals:**
- **The undo on My List or the detail page.** It is Home-only by explicit direction.
- **Keeping the undo across navigation.** It lasts while the carousel is mounted (D8).
- **Rewriting activity history.** The undo writes its own rows. The Completed row stays.
- **Undoing the start date.** A "+" from 0 to a 1-episode total also fills `StartedAt`, and the undo leaves it, the same as lowering any count to 0 elsewhere.
- **`RewatchingEligibility`, `AiredEpisodeGate`, the general drop-below-total rule, and save-and-rank** are unchanged.
- **The edit endpoint** is unchanged. The only backend change is two new fields on the dashboard payload.

## Decisions

### D1. The prompt gate: any change into Completed, except a rewatch that already has a score

In `setEpisodesWatched`, after `onSaved`:

```ts
const becameCompleted = target.previousStatus !== 'Completed' && saved.status === 'Completed'
if (!becameCompleted) return
const isRewatch = target.previousStatus === 'Rewatching'
if (isRewatch && target.currentScore != null) {
  target.onPhantomCompleted?.({ kind: 'RewatchCompletion', rewatchCountAfter: saved.rewatchCount })
  return
}
setPrompt({ ...,
  onClosed: (savedResult) => {
    if (savedResult !== null) target.onCompleted?.(savedResult)
    else target.onPhantomCompleted?.(isRewatch
      ? { kind: 'RewatchCompletion', rewatchCountAfter: saved.rewatchCount }
      : { kind: 'FirstCompletion' })
  },
})
```

`previousStatus !== 'Rewatching'` is dropped, so a rewatch goes through the same gate as any other completion.

`currentScore` is `null` exactly when the entry has no score. The server stores "No score" as `null` (`:368`), and every caller passes `entry.myScore`, the carousel included after D4.

*Alternatives:*
- **Reload whenever the status changes** (the triage doc's fix). Rejected in design discussion. A misclicked "+" would reload the card out of the row before it could be corrected.
- **Prompt for every rewatch.** Rejected. It asks again for a score the entry already has, every time.

### D2. Only a saved score triggers `onCompleted`, and Save always saves

Today `CompletionScoreOverlay.handleSave` closes with `onClose(null)` when the score wasn't changed (`components/CompletionScoreOverlay.tsx:49-52`). Under D1, that makes Save act like Cancel whenever the pre-selected score is kept. That covers "No score" on an unscored entry, and every first completion of an entry that already has a score. The card would stay at n/n after the user pressed Save. The brief treats Save as a commit. So the short-circuit is removed: Save always sends `updateEntry(animeId, { myScore: score })` and closes with the returned entry.

When the score is unchanged, the server does nothing and returns the entry. No activity, no sync, no ranking placement. Save can now fail in that case too, and it then shows the prompt's existing error, like any failed save.

Because `onCompleted` is now only called with an entry, its type narrows to `(entry: UserAnimeEntryDto) => void`. `AnimeDetailPage`'s `if (saved)` guard and its "null means the user skipped" comment go, with no change in behaviour. The carousel's `reload`-shaped callback still type-checks unchanged. My List's callback changes in D11.

*Alternatives:*
- **Keep the short-circuit and accept that Save sometimes acts like Cancel.** Rejected. Pressing Save and seeing nothing commit is confusing.
- **Widen `onClose` to a three-way result (saved / kept / cancelled).** Rejected. It adds contract surface to the overlay and all three callers just to save one no-op request.

### D3. "Skip" becomes "Cancel", label only

Only the button text changes. The overlay's `onClose` doc comment changes to say "Cancel".

Settled: "Cancel" stays, with no tooltip. "Not now" and "Skip scoring" were considered, since the entry is already Completed when the prompt opens and "Cancel" could read as undoing that. Afterwards, My List and the detail page show the entry as Completed, which makes clear what Cancel did. On Home, Cancel followed by lowering the count really does take the completion back (D5).

### D4. The dashboard payload carries `MyScore` and `CompletedAt`

- `CurrentlyWatchingItemDto` gains `int? MyScore` and `DateOnly? CompletedAt`, appended after `AiringStatus`.
- `MainDashboardService` passes `e.MyScore` and `e.CompletedAt` from the entity it already loaded (`:62-76`). No query changes.
- `api/types.ts` mirrors them as `myScore: number | null` and `completedAt: string | null`, the same shapes `UserAnimeEntryDto` uses.
- The carousel passes `currentScore: item.myScore`, and its stale "no score field" comment goes.

Both fields are needed. Without the score, every rewatch completed from Home would look unscored and always prompt, the opposite of D1. Without the finish date, the undo can't put it back (D5).

*Alternative:* have the undo always send `completedAt: null`, as the brief suggested, with no new field. Rejected in D5.

### D5. What the corrective edit sends

The carousel keeps, per card, the `PhantomCompletion` plus the card's `completedAt` as loaded (`completedAtBefore`). While a record exists, `buildTarget` sets `extraEdit` to one of these:

| Record | `extraEdit` | Server path | Result |
|---|---|---|---|
| `FirstCompletion` | `{ status: 'Watching', completedAt: completedAtBefore }` | Explicit status differs from Completed, so the automatic transitions are skipped (`:213`). `ApplyStatus` last branch (`:309-314`). `ApplyDates`. | Watching, typed count, finish date as before, rewatch count untouched |
| `RewatchCompletion` | `{ rewatchCount: rewatchCountAfter - 1, completedAt: completedAtBefore }` | No status, so the automatic drop gives Rewatching (`:236-238`). `ApplyDates`. `ApplyRewatchCount`. | Rewatching, typed count, finish date as before, rewatch count as before |

**Why the finish date isn't just `null`.** Any completion, first or rewatch, fills the finish date only if it's empty (`:224`). So what the undo needs to know is whether the date was empty before, and neither the kind of completion nor the rewatch count says that:
- **A Watching entry can already have a finish date, with a rewatch count of 0.** It isn't reached through airing: a show can't complete until everything has aired, and a caught-up show just leaves the dashboard as Watching with no finish date. It comes from a finished show being rewatched as Watching instead of Rewatching. Two ways that happens:
  - Choosing Watching for a Completed entry in the editor, which keeps the finish date (`list-editing` "Un-completing keeps the finish date").
  - Importing from MyAnimeList an entry rewatched there as "watching" (`Services/Mal/MalMappingExtensions.cs:20`). MyAnimeList has no rewatching status, and a first import has no local Rewatching to keep (`MalStatusResolution.cs:12-13`).

  Finishing such an entry from Home keeps that date, and `completedAt: null` would erase it.
- **A Rewatching entry can have no finish date, and its rewatch count becomes 1 or more on finishing.** For example, an entry imported from MyAnimeList as Completed with no finish date, then set to Rewatching (`HasFinishedOnce` accepts `Status == Completed`). Finishing that rewatch fills in today, which the undo has to clear.
- Sending the loaded value covers both. When it equals what's stored, `ApplyDates` does nothing, and a null value also clears the date on MyAnimeList (`Services/Mal/Dto/MalListStatusUpdate.cs:25-29`).

*Alternatives:*
- **Decide from the rewatch count** (keep the date when the count is 1 or more). Rejected; the two cases above get it wrong in both directions.
- **Compare `saved.completedAt` with today.** Rejected. The server's "today" is the UTC date, which differs from the browser's local date near midnight, and a date already set to today can't be told apart.

**Why `rewatchCountAfter - 1`.** The completing edit raises the rewatch count by exactly one (`:225-226`), and `saved.rewatchCount` is the server's own figure right after that. The card doesn't carry a rewatch count, and doesn't need one.

**What `FirstCompletion` covers.** It is any completion not reached from Rewatching, including a parked rewatch resumed as Watching. The undo is still right for that case: back to Watching with its own finish date, and its rewatch count was never raised. The name follows the brief.

### D6. Home keeps patching only the count

The undo depends on the Home card keeping its status from before the completion.
- `buildTarget` passes `item.status` as `previousStatus`. After a phantom completion that is still Watching or Rewatching. After the undo, the server is back to that same status, so the card is accurate again with no status patch.
- If Home started patching `status` from `saved`, a later full-count edit on that card would look like it started from Completed, and the gate in D1 would never fire again.

This reliance is written into the carousel's comment so a later change doesn't patch status there.

### D7. The shared contract: `onPhantomCompleted` and `extraEdit`

These are added to `IncrementTarget` in `api/types.ts`:

```ts
export type PhantomCompletion =
  | { kind: 'FirstCompletion' }
  | { kind: 'RewatchCompletion'; rewatchCountAfter: number }

// on IncrementTarget:
onCompleted?: (entry: UserAnimeEntryDto) => void          // narrowed (D2)
onPhantomCompleted?: (completion: PhantomCompletion) => void
extraEdit?: Omit<UserAnimeEntryEditRequest, 'episodesWatched'>
```

`setEpisodesWatched` sends `updateEntry(target.animeId, { ...target.extraEdit, episodesWatched: value })`. The `Omit` and the spread order together mean `extraEdit` can never override the count.

`onPhantomCompleted` is called at exactly the two places a completion happens without `onCompleted` (D1). The context doesn't know the undo exists. It only reports a completion that wasn't committed, and forwards whatever extra fields the caller asks for. My List and the detail page pass neither field.

*Alternative:* put the whole undo inside the context, keyed by anime id. Rejected. The context is shared by three pages and the undo is Home-only by direction. Keeping state in the context would need a per-caller opt-in anyway, and would outlive the page that owns it.

### D8. The carousel's bookkeeping, and when a record ends

- **State.** `useState<ReadonlyMap<number, { completion: PhantomCompletion; completedAtBefore: string | null }>>`. It is always replaced with a new `Map`, never changed in place.
- **Record.** `buildTarget(item)`'s `onPhantomCompleted` stores `{ completion, completedAtBefore: item.completedAt }` under `item.animeId`.
- **Apply.** `buildTarget` reads the current map when the click happens and derives `extraEdit` (D5), or `undefined` if there's no record.
- **Clear.** `onSaved` patches the count as today, and removes the card's record if there is one.
  - `onSaved` runs only after a successful `updateEntry`, so a failed undo keeps its record and a retry sends the same corrective edit.
  - Clearing after the awaited `increment`/`setWatched` returns would be wrong, because that promise also resolves on failure.
  - `onSaved` for the completing edit itself runs before the record is created, so it never clears the record being made.
- **Prune.** An effect on `items` drops records whose anime id is no longer in `items`. It returns the previous map when nothing was dropped, so it doesn't cause an extra render. Count patches keep the id in `items`, so only a server read can prune.
- **Lifetime.** Records live as long as the carousel is mounted. Leaving Home unmounts it. On return, a restore first paints the saved snapshot, with the n/n card, and the background refresh then removes the completed card. Keeping records across that would mean storing them in the page snapshot, for a gap of one request (see Risks).

A card with a record can't be completed again without the undo first: "+" is disabled at the ceiling, and confirming the same count sends nothing (`components/ProgressBar.tsx:100`). The only edit it can receive is the lowering one.

### D9. Where the spec text lives

- **No new capability.** The completion prompt is already covered by four `list-editing` requirements (`:881`, `:920`, `:950`, `:975`), so they are modified in place. A separate capability would move four requirements and their history for no change in behaviour.
- **The undo** is a new requirement in `main-dashboard`. It isn't placed under "Rewatches appear in the currently-watching carousel", because undoing a first completion has nothing to do with rewatches. The same requirement also covers the two new payload fields.
- **Named exceptions.** Three `list-editing` rules would otherwise read as contradicted, so each names the `main-dashboard` requirement as its one exception:
  - "An entry is Completed only while its progress covers everything available" (the undo lands in Watching, not Rewatching).
  - "Completed-date lifecycle" (the finish date is restored automatically).
  - "Ending a rewatch early asks whether it counts" together with "Rewatch count is independently editable" (the rewatch count changes without being typed).

### D10. How it's tested

- **Backend (xUnit).**
  - `MainDashboardServiceRewatchingTests`: currently-watching items carry `MyScore` and `CompletedAt`, including a null score and a null finish date.
  - `UserAnimeEntryEditServiceRewatchingTests`, reusing its seeding and fakes: the two corrective edit requests from D5 give exactly the table's results, including a finish date that was already there being kept and one filled in by the completion being cleared.
  - These tests pin the server's step order that the undo relies on. Reordering `Apply*` would fail them, rather than quietly breaking the undo.
- **Frontend (by hand).** Build and lint. Then the brief's scenarios in the browser, checking each PATCH body in DevTools → Network, and forcing failures with request blocking.

### D11. My List applies the saved entry instead of reloading

`MyListPage`'s `onCompleted` becomes `(saved) => patchItem(setItems, item.animeId, saved)`, like its `onSaved`. The reload is dropped: it added nothing the patch doesn't.
- My List groups and sorts rows client-side from `item.entry` (`pages/MyListPage.tsx:475-478`), so the patched entry already moves the row to Completed with its score. The existing comment calling this a "server-computed regrouping" is out of date.
- The only server-computed field the reload refreshed is `myRank`, the my-score sort's tiebreak (`api/types.ts:317-320`, `utils/anime.ts:431-434`). My List's inline score control (`:377-395`) and the entry editor (`:340`) already save scores by patching and leave `myRank` as loaded. The prompt now does the same.
- On save and rank, the reload ran before any reordering in the ranking editor anyway, so it never showed the final ranks.

`buildIncrementTarget` loses its `reload` parameter. `reload` is then unused in `MyListPage`, so it comes out of the `usePageData` destructure (`:139`) and both callbacks' dependency lists (`:359`, `:374`).

The Home dashboard still reloads after a saved score: whether a card belongs in Currently watching is decided server-side, and the edit response doesn't describe it.

*Alternative:* keep the reload for `myRank`. Rejected; it's inconsistent with every other score save on the same page.

## Risks / Trade-offs

- **[Stale card data]** `completedAtBefore` and `myScore` come from the last dashboard read. If they changed in another tab, the undo restores the older finish date, or the gate uses the older score. → Home itself can't change either value, so this needs a concurrent edit elsewhere between loading Home and the undo. Accepted.
- **[Gap after a restore]** Back-navigating to Home paints the snapshot, including an uncommitted n/n card, before the refresh removes it. A count edit in that gap has no record and follows the general rule. → The gap is one request long and the card is about to disappear. Accepted; the spec says the undo lasts "while that page stays open".
- **[A record outlives a server-side change to its entry]** Suppose the dashboard is re-read while a card has a record, and a new episode has re-opened that entry so it's still in the row. The old record would then apply to the next lowering edit. → This needs a completion, then another card's committed completion (the only reload on Home), and a re-open between the two. Even then the edit still takes back that same completion. Accepted.
- **[Save always sends a request]** Save with an unchanged score now costs one round trip and shows "Saving…". → The request changes nothing on the server. A failure shows the prompt's existing error and the user can press Cancel.
- **[My List's rank tiebreak lags]** After a score is saved through the prompt on My List, the row's `myRank` stays as loaded until the next read. In a my-score sort, a newly scored entry can sit in the wrong place among equal scores until then. → This is the same as My List's inline score control and entry editor today (D11). Accepted.
- **[History keeps the undone completion]** Activity, and anything that counts Completed rows, still sees the completion that was undone. → This is the same as un-completing by hand today. Rewriting history is out of scope.

## Migration Plan

- There is no migration. The payload only gains fields, and the frontend and backend deploy together from one compose stack.
- Rollback is reverting the commit. The new fields are ignored by an older frontend.

## Open Questions

None. Both questions raised during design are settled: "Cancel" stays with no tooltip (D3), and My List stops reloading after a saved score (D11).
