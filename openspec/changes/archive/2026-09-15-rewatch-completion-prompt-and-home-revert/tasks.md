Paths without a prefix are under `frontend/src/`. Backend paths start with `backend/`. Line numbers are as of `d6e6a77`. Check each one against the file before editing.

Groups 1–2 are the backend, groups 3–5 the frontend. Group 6 covers checks and docs. Both items land as **one commit**, made by hand after group 6 along with archiving the change, so no task here covers that.

**Backend build and test.** The local SDK is 9.0, so copy `backend/` to `/private/tmp/bm-build/` (excluding `bin/` and `obj/`). Then run `dotnet build` and `dotnet test` in `mcr.microsoft.com/dotnet/sdk:10.0`, mounting from there. Docker can't bind-mount `~/Documents`.

**Frontend build and lint** with nvm's Node 22, since the default `node` is v16. Run from `frontend/`: `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`, then the same prefix with `npm run lint`.

**Frontend checks are by hand** (design D10). The frontend has no test runner. Watch each `PATCH /api/anime/{id}/entry` body in Chrome DevTools → Network. To make a request fail, right-click it → **Block request URL**.

## 1. Backend: score and finish date on the dashboard payload (design D4)

- [x] 1.1 In `backend/AnimeTracker.Api/Services/Dashboard/MainDashboardDto.cs`, add `int? MyScore` and `DateOnly? CompletedAt` to `CurrentlyWatchingItemDto` (`:23-38`), after `AiringStatus`:
  - give each a short comment in the record's existing style
  - the score decides whether a finished rewatch opens the completion prompt
  - the finish date is what the Home undo restores (`main-dashboard` "A completion left in Currently watching can be undone from its card")
- [x] 1.2 In `backend/AnimeTracker.Api/Services/Dashboard/MainDashboardService.cs` (`:64-75`), pass `e.MyScore` and `e.CompletedAt` in the new slots. Both are already loaded on the entity, so add no query or `Include`.
- [x] 1.3 In `backend/AnimeTracker.Api.Tests/Services/Dashboard/MainDashboardServiceRewatchingTests.cs`, add a `[Fact]` with two currently-watching entries:
  - one with `MyScore = 7` and a `CompletedAt`
  - one with `MyScore = null` and `CompletedAt = null`
  - assert each item's `MyScore` and `CompletedAt` match its entry

## 2. Backend: pin the server behaviour the undo depends on (design D5, D10)

All in `backend/AnimeTracker.Api.Tests/Services/Entries/UserAnimeEntryEditServiceRewatchingTests.cs`, reusing `SeedAsync` and `CreateService`. Add a short section comment naming the `main-dashboard` requirement and design D5. No production code changes in this group.

- [x] 2.1 **Undo a completion from Watching, no finish date before.** Seed a Completed entry, as the completion edit leaves it:
  - 12 of 12 episodes, `finished_airing`, `CompletedAt = today`, `RewatchCount = 0`, `MyScore = 6`
  - Send `{ EpisodesWatched = 11, Status = Watching, CompletedAt = null }`.
  - Assert: Watching, 11 episodes, `CompletedAt` null, `RewatchCount` 0, `MyScore` 6.
- [x] 2.2 **Undo a completion from Watching, finish date already there.** Seed as 2.1 but with `CompletedAt = 2024-01-01`.
  - Send `{ EpisodesWatched = 11, Status = Watching, CompletedAt = 2024-01-01 }`.
  - Assert: Watching, 11 episodes, `CompletedAt` still 2024-01-01.
- [x] 2.3 **Undo a finished rewatch.** Seed a Completed entry:
  - 24 of 24 episodes, `finished_airing`, `CompletedAt = 2024-01-01`, `RewatchCount = 2`, `MyScore = 8`
  - Send `{ EpisodesWatched = 20, RewatchCount = 1, CompletedAt = 2024-01-01 }`, with no status.
  - Assert: Rewatching, 20 episodes, `RewatchCount` 1, `CompletedAt` 2024-01-01, `MyScore` 8.
- [x] 2.4 **Undo a finished rewatch whose finish date the completion filled in.** Seed as 2.3 but with `CompletedAt = today` and `RewatchCount = 1`.
  - Send `{ EpisodesWatched = 20, RewatchCount = 0, CompletedAt = null }`.
  - Assert: Rewatching, `RewatchCount` 0, `CompletedAt` null.
  - This also proves the drop to Rewatching doesn't depend on the finish date or rewatch count sent in the same request.
- [x] 2.5 **An unchanged score save changes nothing.** Seed a Completed entry with `MyScore = 7`, then send `{ MyScore = 7 }`.
  - Assert the returned entry is unchanged.
  - Assert no `ActivityLog` row was added.
  - Assert `PendingSync` is still false.
- [x] 2.6 Run the backend build and tests (see the top of this file). Everything must pass.

## 3. Frontend: types, the completion prompt, and My List (design D1–D3, D7, D11)

- [x] 3.1 In `api/types.ts`:
  - add `myScore: number | null` and `completedAt: string | null` to `CurrentlyWatchingItemDto` (`:126-141`), with comments matching 1.1
  - add and export `PhantomCompletion`: `{ kind: 'FirstCompletion' } | { kind: 'RewatchCompletion'; rewatchCountAfter: number }`. Comment: a completion that happened without a score being saved (a scored rewatch finishing with no prompt, or a prompt closed without saving). `FirstCompletion` means any completion not reached from Rewatching (design D5).
  - in `IncrementTarget` (`:102-119`), narrow `onCompleted` to `(entry: UserAnimeEntryDto) => void`, and rewrite its comment: called only when the completion prompt closes with a saved score
  - add `onPhantomCompleted?: (completion: PhantomCompletion) => void`
  - add `extraEdit?: Omit<UserAnimeEntryEditRequest, 'episodesWatched'>`, with a comment: extra fields sent in the same edit, which can never override the count
- [x] 3.2 In `context/CompletionPromptContext.tsx` `setEpisodesWatched` (`:39-76`):
  - change the request to `updateEntry(target.animeId, { ...target.extraEdit, episodesWatched: value })`
  - replace the `justCompleted` block (`:55-75`) with design D1's shape:
    - `becameCompleted`
    - the scored-rewatch early return, calling `onPhantomCompleted` with `RewatchCompletion` and `saved.rewatchCount`
    - `setPrompt`, whose `onClosed` calls `onCompleted` for a saved entry and otherwise `onPhantomCompleted`
  - rewrite the comment above it: cite `list-editing` "Completion prompt fires only on entering Completed" (a scored rewatch is the only rewatch left out) and "Triggering view refreshes after the completion prompt closes" (only a saved score refreshes). Keep the existing note that every completion here is on an anime that has aired in full.
- [x] 3.3 In `components/CompletionScoreOverlay.tsx`:
  - in `handleSave` (`:48-62`), remove the `score === initialScore` short-circuit so Save always sends `updateEntry(animeId, { myScore: score })` (design D2). Add a comment saying why: Save must close as a save, and an unchanged score changes nothing on the server.
  - if `initialScore` is then unused apart from `useState`, inline it
  - rename the button text `Skip` to `Cancel` (`:110`)
  - update the `onClose` prop comment (`:20-23`): `null` means Cancel, Escape or click-outside, and the caller must not treat it as a commit
- [x] 3.4 In `pages/AnimeDetailPage.tsx` `buildIncrementTarget` (`:315-336`), simplify `onCompleted` to `(saved) => setDetail((prev) => (prev ? { ...prev, entry: saved } : prev))`. Drop the "null means the user skipped" sentence from its comment. No change in behaviour.
- [x] 3.5 In `pages/MyListPage.tsx`, stop reloading after a saved score (design D11):
  - in `buildIncrementTarget` (`:95-113`), replace `onCompleted: reload` with `onCompleted: (saved) => patchItem(setItems, item.animeId, saved)`, and remove the `reload` parameter
  - rewrite its comment: grouping and sorting are client-side from the patched entry. `myRank` stays as loaded until the next read, the same as the row's score control and the entry editor.
  - drop the `reload` argument at both call sites (`:353`, `:368`) and from both dependency lists (`:359`, `:374`)
  - drop `reload` from the `usePageData` destructure (`:139`), after grepping the file to confirm nothing else uses it

## 4. Frontend: the Home carousel's undo (design D4–D6, D8)

All in `components/CurrentlyWatchingCarousel.tsx` unless noted.

- [x] 4.1 Import `PhantomCompletion` and `UserAnimeEntryEditRequest` from `../api/types.ts`. Add state:
  - `const [phantoms, setPhantoms] = useState<ReadonlyMap<number, { completion: PhantomCompletion; completedAtBefore: string | null }>>(() => new Map())`
  - comment: completions left in the row without a saved score, keyed by anime id, each with the card's finish date as loaded
- [x] 4.2 Add a prune effect on `[items]`:
  - drop records whose anime id isn't in `items`, returning `prev` unchanged when nothing is dropped
  - put it beside the existing overflow effect, before the `items.length === 0` early return, so hook order stays fixed
  - comment: count patches keep an id in `items`, so only a server read prunes
- [x] 4.3 Add a small local helper that turns a record into the corrective `Omit<UserAnimeEntryEditRequest, 'episodesWatched'>` from design D5's table:
  - `FirstCompletion` → `{ status: 'Watching', completedAt: completedAtBefore }`
  - `RewatchCompletion` → `{ rewatchCount: completion.rewatchCountAfter - 1, completedAt: completedAtBefore }`
  - include a one-line note on why each shape reaches the right server path
- [x] 4.4 In `buildTarget` (`:65-87`):
  - `currentScore: item.myScore`, replacing the hard-coded `null` and its stale comment
  - rewrite the `previousStatus` comment to say that Home never patches status, and the undo relies on that (design D6)
  - `onSaved`: patch the count as today, then remove `item.animeId`'s record if there is one, returning `prev` when there isn't. Comment: `onSaved` only runs after a successful save, so a failed undo keeps its record for a retry.
  - `onPhantomCompleted: (completion) => setPhantoms((prev) => new Map(prev).set(item.animeId, { completion, completedAtBefore: item.completedAt }))`
  - `extraEdit`: the helper from 4.3 applied to `phantoms.get(item.animeId)`, or `undefined`
  - `onCompleted`: unchanged
- [x] 4.5 In `pages/HomePage.tsx`, rewrite the comment on `onCompleted={reload}` (`:40-42`): the reload runs only once a score is saved through the completion prompt, which is what moves the anime out of "Currently watching". A completion left uncommitted stays in place and can be undone from the card. No code change.
- [x] 4.6 Grep `frontend/src` for `onCompleted`, `Skip` and `currentScore: null` to confirm nothing stale is left:
  - `onCompleted`: only the three callers and the context should remain
  - `Skip`: none should remain in `CompletionScoreOverlay.tsx`
  - `currentScore: null`: none should remain

## 5. Frontend: build and lint

- [x] 5.1 Run the frontend build (see the top of this file). `tsc -b` must pass, including the narrowed `onCompleted` at all three callers.
- [x] 5.2 Run the frontend lint (see the top of this file). No new warnings in the touched files.

## 6. Checks by hand, and docs

Start the stack, then set up test entries by hand, through the entry editor or the database. You need:
- a Watching entry one episode short of a finished anime's total, with no finish date;
- a similar Watching entry that already has a finish date;
- a Rewatching entry one short of the total, with a score;
- a Rewatching entry one short of the total, with no score.

After each step, reload the page when you need to see the stored state, or read the PATCH response.

- [x] 6.1 **First completion, Save** (Home):
  - Press "+" on the first Watching card. The prompt opens with "Cancel", "Save" and, once a score is picked, "Save and rank".
  - Leave "No score" and press Save.
  - Expect a `{ myScore: 0 }` PATCH and a dashboard reload with the card gone.
- [x] 6.2 **First completion, Cancel / Escape / click-outside** (Home). Repeat on a fresh Watching card three times, closing each way. Each time:
  - no dashboard re-read in Network
  - the card stays where it sits at n/n, with "+" disabled
- [x] 6.3 **Undo of 6.2**: set that card's count to total − 1.
  - The PATCH body is `{ status: "Watching", completedAt: null, episodesWatched: … }`.
  - After a reload the entry is Watching with that count, no finish date, and the rewatch count unchanged.
  - Repeat with the Watching entry that already had a finish date. The body carries that date, and it's still there afterwards.
- [x] 6.4 **Scored rewatch** (Home):
  - Press "+" on the scored Rewatching card. No prompt, no re-read, the card stays at n/n.
  - The response shows Completed and `rewatchCount` + 1.
  - Set the count to total − 1. The body is `{ rewatchCount: <after − 1>, completedAt: <loaded>, episodesWatched: … }` with no `status`.
  - After a reload the entry is Rewatching with the original rewatch count and finish date.
- [x] 6.5 **Unscored rewatch** (Home):
  - Press "+" on the unscored Rewatching card. The prompt opens.
  - Cancel it, then lower the count. You get the same result as 6.4.
  - Set it up again, finish it, and save a score of 7. The dashboard reloads without the card, and the entry is Completed with score 7 and the rewatch count + 1.
- [x] 6.6 **Undo fails, then retried**:
  - Leave a card completed but uncommitted, as in 6.2 or 6.4.
  - Block the entry PATCH URL and lower the count. A failure notice appears and the card keeps its full count.
  - Unblock and lower it again. The PATCH body still carries the corrective fields, and the result matches 6.3 or 6.4.
- [x] 6.7 **After the undo the card is ordinary**: raise an undone card back to its total. It completes again with the prompt or the silent path, exactly as before.
- [x] 6.8 **The next read ends it**: leave a card completed but uncommitted, then reload the browser. The card is gone.
- [x] 6.9 **My List and the detail page**:
  - Complete an entry from My List and Cancel. There's no `GET /api/my-list`, and the row reads Completed in the Completed group.
  - Complete another and Save with a score. Again no `GET /api/my-list`; the row reads Completed with that score. Repeat with Save and rank: the ranking editor opens over a row that already shows the score.
  - Lower its count there. The PATCH body is only `{ episodesWatched }` and the entry becomes Rewatching (the general rule).
  - On a detail page: complete, Cancel, and the page shows Completed with no re-read. Complete and Save, and the page shows the saved score with no re-read.
- [x] 6.10 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern PF2 and N2 used:
  - replace the PF5 entry under "Partly fixed" (`:182-188`) with a short blockquote pointing to the resolved file
  - update the Summary table: the "Partly fixed" count, and add PF5 to the "Resolved 2026-09-15 (code fixes)" row
  - mark item 12 of Phase 6 done, and mark Phase 6's heading "— done", as earlier phases were
  - update the ISSUES #21 appendix row (`:375`) so its PF5 half reads as resolved
- [x] 6.11 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## PF5.` section after N2, under "Resolved 2026-09-15 (code fixes)". Name this change. Record:
  - the fix went beyond the triage doc's fix direction: an unscored rewatch now prompts, a scored one doesn't, and nothing reloads without a saved score (a plain reload was rejected)
  - Save now always saves, since an unchanged score used to close as a skip
  - the Home-only undo was added alongside, and it restores the finish date the card had before, not always null
  - two settled decisions: "Cancel" stays with no tooltip, and My List now patches the saved entry instead of reloading (its `myRank` tiebreak updates on the next read, like its other score saves)
- [x] 6.12 Run `openspec validate rewatch-completion-prompt-and-home-revert --strict` and fix anything it reports.
