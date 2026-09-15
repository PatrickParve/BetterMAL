## Why

This change fixes triage item PF5 from `docs/ISSUE_TRIAGE.md`, Phase 6 ("Frontend polish"). It also adds one related item that is not in the triage doc: on the Home page, you can undo a completion you didn't commit to. The two share one code path, so they land as one commit.

- **PF5.** Finish a rewatch with "+" on Home and the card stays at n/n with "+" disabled until you reload. A rewatch never opens the completion-score prompt, and the page's reload only runs when that prompt closes. The triage doc's fix was to reload whenever the status changes. That was rejected during design. Now a rewatch with no score gets the prompt. A rewatch that already has a score still gets no prompt and no reload. For every completion, the page reloads only when a score is actually saved.
- **Home-only completion revert.** After PF5, a completion can be left on Home without being committed: either a scored rewatch that finished silently, or any completion whose prompt was cancelled. Either could be a misclick. If you then lower that card's count on Home, the entry goes back to how it was before the completion. It does not follow the app's general rule for lowering a Completed entry's count.

The brief for this change supersedes the triage doc's fix direction where they disagree.

**Verified against the code** (2026-09-15, at `d6e6a77`; PF2/N2 have already landed). Paths without a prefix are under `frontend/src/`.

- **Prompt gate.** `context/CompletionPromptContext.tsx:62-66` rules out `previousStatus === 'Rewatching'`. `:74` calls `target.onCompleted` whenever the prompt closes, including on Skip (`components/CompletionScoreOverlay.tsx:109-111`), Escape and click-outside (`:86`).
- **Save can close as a skip.** `CompletionScoreOverlay.tsx:49-52`: if the score wasn't changed, Save calls `onClose(null)`, the same as Skip. If only a real save reloads the page, Save with the pre-selected score left as-is would act like Cancel. That covers "No score" on an unscored entry, and any entry that already had a score when first completed. The brief assumed Save always passes back a saved entry (design D2).
- **The carousel has no score.** `components/CurrentlyWatchingCarousel.tsx:78` hard-codes `currentScore: null`. `CurrentlyWatchingItemDto` has no score field in either `backend/AnimeTracker.Api/Services/Dashboard/MainDashboardDto.cs:23-38` or `api/types.ts:126-141`. `MainDashboardService.cs:62-76` builds it from the tracked entity, which already has `MyScore` and `CompletedAt` loaded (`Models/UserAnimeEntry.cs:37,39`).
- **Home patches only the count.** `pages/HomePage.tsx:17-31` patches `episodesWatched`, never `status`. After a completion is left behind, the card still carries its status from before the completion. The revert depends on that (design D6).
- **Server edit order** (`Services/Entries/UserAnimeEntryEditService.cs`):
  - `ApplyEpisodesWatched` (`:170-259`) runs before `ApplyStatus`, `ApplyDates` and `ApplyRewatchCount` (`:60-64`).
  - Its automatic transitions only run when `request.Status is null || request.Status == originalStatus` (`:213`).
  - Reaching the total from Rewatching sets `CompletedAt ??= today` and `RewatchCount++` (`:221-228`).
  - Dropping a Completed entry below the total sends it to Rewatching when eligible (`:236-240`). This entry is still Completed at that point, so it's always eligible (`RewatchingEligibility.cs:21-22`).
  - An explicit non-Completed, non-Rewatching status only sets the status (`:309-312`).
  - `ApplyDates` applies `completedAt` whenever the key is present (`:340-344`).
  - `ApplyRewatchCount` applies independently (`:372-389`).
- **The finish date before the completion isn't always empty.**
  - A Watching entry can already have a finish date: a rewatch parked as Watching, an entry un-completed by hand (`list-editing` "Un-completing keeps the finish date"), or an import.
  - A Rewatching entry can have none: `HasFinishedOnce` also accepts `Status == Completed` or `RewatchCount > 0`.
  - So the brief's `completedAt: null` (Scenario A) would erase a real date, and "CompletedAt unchanged" (Scenario B) is wrong when the completion filled it in.
  - The revert has to send back the date the card had before (design D5).
  - A null date clears on MyAnimeList too (`Services/Mal/Dto/MalListStatusUpdate.cs:25-29`).
- **The prompt already has specs.** Four `list-editing` requirements cover it: "Score prompt on completion via the increment button" (`:881`), "Completion prompt fires only on entering Completed" (`:920`), "Saving or dismissing the completion score prompt" (`:950`) and "Triggering view refreshes after the completion prompt closes" (`:975`). They're modified in place. No new capability is added (design D9).
- **No frontend test runner.** `frontend/package.json` has only `build` and `lint`. Frontend checks are done by hand, as in `fix-silent-actions-and-mal-status-recheck`. Backend behaviour gets xUnit tests.

## What Changes

- **The completion prompt opens for an unscored rewatch.**
  - `setEpisodesWatched` opens the prompt on any change into Completed.
  - A rewatch that already has a score is the only case that skips it.
  - An already-Completed entry and completions made by the system still never prompt.
- **Only a saved score refreshes the page.**
  - `onCompleted` runs only when the prompt closes with a saved entry.
  - Cancel, Escape and click-outside refresh nothing.
  - This applies to every caller and every completion, first-time ones included.
  - `IncrementTarget.onCompleted` now always receives an entry. `AnimeDetailPage`'s null guard and its comment are removed, with no change in behaviour.
- **My List applies the saved score instead of reloading.** Its `onCompleted` patches the saved entry, as its score control and entry editor already do. The Home dashboard still reloads, since whether a card belongs in Currently watching is decided server-side.
- **Save always saves.** `CompletionScoreOverlay`'s Save no longer closes as a skip when the score is unchanged. It sends the score. If nothing changed, the server treats that as a no-op: no activity row, no sync, no ranking placement. It then closes with the saved entry.
- **"Skip" is renamed "Cancel".** The label changes. The button still calls `onClose(null)`.
- **The dashboard payload carries each card's score and finish date.**
  - `CurrentlyWatchingItemDto` gains `MyScore` and `CompletedAt`, in C# and TypeScript.
  - The carousel passes `currentScore: item.myScore`.
- **A shared, opt-in hook for completions left uncommitted.**
  - `IncrementTarget` gains `onPhantomCompleted?: (info: PhantomCompletion) => void`. It's called when a completion happens but `onCompleted` isn't: a silent scored rewatch, or a cancelled prompt.
  - `IncrementTarget` also gains `extraEdit?`, which is sent with the count in the same edit request and can never override `episodesWatched`.
  - `PhantomCompletion` is `{ kind: 'FirstCompletion' } | { kind: 'RewatchCompletion'; rewatchCountAfter: number }`.
  - My List and the detail page pass neither field.
- **The Home carousel undoes a completion left behind** when that card's count is lowered.
  - The carousel records each phantom completion by anime id, together with the card's loaded `completedAt`.
  - It clears the record in `onSaved`, which only runs on success, and drops records for cards no longer in the row.
  - While a record exists, the card's edit sends one of these:
    - `{ status: 'Watching', completedAt: <loaded> }` after a completion from Watching.
    - `{ rewatchCount: rewatchCountAfter - 1, completedAt: <loaded> }` after a rewatch.
- **Spec text** in `list-editing` and `main-dashboard` is updated to match (see Capabilities).

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `list-editing`:
  - The four completion-prompt requirements: an unscored rewatch prompts; "Skip" becomes "Cancel"; Save always saves; only a saved score updates the triggering view, and my list applies the saved entry instead of re-reading.
  - "Watching a rewatch to the end counts it automatically": the prompt is skipped only for a rewatch that already has a score.
  - Three rules gain a named exception for the Home revert:
    - "Ending a rewatch early asks whether it counts" and "Rewatch count is independently editable" ("the only … changes the user does not type").
    - "An entry is Completed only while its progress covers everything available" (lowering from a completion left behind on Home lands in Watching).
    - "Completed-date lifecycle" (the finish date is put back to what it was before).
- `main-dashboard`:
  - "Currently watching carousel ordering" and "Rewatches appear in the currently-watching carousel": a card leaves the row once a score is saved through the prompt, not whenever the entry becomes Completed.
  - A new requirement covers undoing, from the card, a completion left in Currently watching. It includes the payload's score and finish date fields.

## Impact

- **Frontend:**
  - `context/CompletionPromptContext.tsx`
  - `components/CompletionScoreOverlay.tsx`
  - `components/CurrentlyWatchingCarousel.tsx`
  - `api/types.ts` (`IncrementTarget`, `PhantomCompletion`, `CurrentlyWatchingItemDto`)
  - `pages/HomePage.tsx` (comment only)
  - `pages/AnimeDetailPage.tsx` (redundant null guard and comment)
  - `pages/MyListPage.tsx` (patches the saved entry instead of reloading; `reload` is no longer used)
- **Backend:**
  - `Services/Dashboard/MainDashboardDto.cs` and `Services/Dashboard/MainDashboardService.cs`: two new fields on the record, no new query.
  - The edit endpoint is unchanged.
- **Tests:**
  - New xUnit tests for the two new dashboard fields.
  - New xUnit tests for the two corrective edit requests, which pin the server behaviour the revert relies on.
  - Frontend checks are done by hand.
- **API:**
  - `GET /api/dashboard`'s currently-watching items gain `myScore` and `completedAt`. Nothing is removed.
- **Sync and history:**
  - A revert is an ordinary edit. It writes its own activity rows and queues a MyAnimeList sync as usual.
  - The earlier Completed activity row isn't removed.
