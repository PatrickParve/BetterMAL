## Context

This change fixes triage items PF2 and N2. The proposal has the evidence with line references. This section covers only what shapes the design. Paths without a prefix are under `frontend/src/`.

**How a failure notice is raised today.**
- `useActionFailure()` (`context/ActionFailureContext.tsx:51-55`) returns `reportFailure({ title, reason })`.
- The detail page's add buttons are the model (`pages/AnimeDetailPage.tsx:223-255`). They `catch (err)` and call `reportFailure({ title: \`Couldn't add ${pickDisplayTitle(detail.title, detail.englishTitle)} to list\`, reason: err instanceof ApiError ? err.reason : null })`. The inline count uses `Couldn't update ${animeTitle}` (`context/CompletionPromptContext.tsx:47-50`).
- `reportFailure` is stable for the life of the app. It is a `useCallback` over `dismissFailure`, which is a `useCallback([])` (`ActionFailureContext.tsx:30-38`).
- `ApiError.reason` is the `{ error }` body's message or `null` (`api/client.ts:92-117`).
- A network-level failure throws the fetch's own error, not an `ApiError`, and also raises the connection-status bar (`api/client.ts:79-91`).

**The nine sites fall into three shapes.**
- **Awaited, with the page left as it was.**
  - My List's score (`MyListPage.tsx:375-388`): a `useCallback`, and `MyListRow` is memoised on it.
  - Top Anime's add (`TopAnimePage.tsx:150-161`).
  - The detail page's refresh (`AnimeDetailPage.tsx:196-207`).
  - The detail page's refresh has no `detail` guard. Its button only renders after the `if (!detail)` return at `:367`.
- **Awaited, with the page rolled back.** The favourite reorder (`SeriesPage.tsx:592-608`). Its two callers, the ▲ and ▼ buttons in the favourites list at `:1177-1206`, have the moved `entry` in scope and already build its display title for their `aria-label`s.
- **A promise chain, optimistic or not.**
  - The detail page's picture pick and reset (`AnimeDetailPage.tsx:265-297`): both optimistic.
  - The series page's picture pick (`:613-621`) and title pick (`:638-646`): both optimistic.
  - The series page's picture reset (`:626-636`): waits for the response.
  - None of them roll back.

**How the app decides between the connect screen and the shell** (`App.tsx`):
- One mount-only effect calls `getMalAuthStatus()`. On failure it sets `statusError`.
- It renders the backend-down screen, "Loading…", the connect screen (`NotConnected`), or `<AppShell />` (`Connected` or `Lost`).
- `App` is outside every provider, `ActionFailureProvider` included.
- `getMalAuthStatus` is a GET through `fetchRaw`, which joins identical GETs already in flight (`api/client.ts:56-72`).

**Constraints.**
- The frontend has no test runner. Settled with the user on 2026-09-15: this change is checked by hand in the browser, plus `npm run build` (`tsc -b && vite build`) and `npm run lint` (oxlint), as earlier frontend changes were.
- The default `node` is v16. Vite 8 needs nvm's v22.
- Two commits: PF2, then N2.

## Goals / Non-Goals

**Goals:**
- Each of the nine catch sites raises one failure notice. The notice names the anime or series and the action, and carries the server's reason when there is one.
- What each control does to the page on failure stays exactly as it is.
- `MyListRow`'s memo still holds: `changeScore` keeps a stable identity.
- While the connect screen is shown, coming back to the tab re-reads the connection state, and a connected state opens the app with no reload. That includes a Back navigation restored from the back/forward cache.
- A failed re-read never replaces the connect screen with the backend-down screen.

**Non-Goals:**
- Rolling back an optimistic picture or title pick when its save fails (Open Questions).
- `AnimeDetailPage.tsx` `handleRetry` (`:213-221`). A failed retry already leaves the page in its error-and-retry state.
- Silent catches the triage doc doesn't name.
  - `SeriesPage.tsx` `handleRebuild` (`:576`) is the closest sibling.
  - The rest are background loads (`usePageData`, the picture backfill), overlays with their own in-place error, or Settings controls that aren't part of PF2.
- Making `usePageData`'s `reload()` reject (D4).
- Re-reading the connection once the app shell is showing, or from the backend-down screen.
- Redirecting the OAuth callback back to the frontend.
- A frontend test runner.

## Decisions

### D1. Notice titles

| Site | Title | Name taken from |
|---|---|---|
| My List score | `` `Couldn't update the score for ${title}` `` | `pickDisplayTitle(item.title, item.englishTitle)`, as `MyListRow`'s `aria-label` does |
| Top Anime add | `` `Couldn't add ${title} to list` `` | `pickDisplayTitle(item.title, item.englishTitle)` |
| Detail refresh | `` `Couldn't refresh ${title}` `` | `pickDisplayTitle(detail.title, detail.englishTitle)` |
| Detail picture pick | `` `Couldn't save the picture for ${title}` `` | same |
| Detail picture reset | `` `Couldn't reset the picture for ${title}` `` | same |
| Series picture pick | `` `Couldn't save the picture for ${title}` `` | `pickDisplayTitle(data.series.title, data.series.englishTitle)`, read before `patchSeries` |
| Series picture reset | `` `Couldn't reset the picture for ${title}` `` | same |
| Series title pick | `` `Couldn't rename ${previousTitle} to ${title}` `` | `previousTitle` as above, read before the optimistic patch; `title` is the picked one |
| Favourite reorder | `` `Couldn't move ${movedTitle} in your favourites` `` | a new `movedTitle: string` parameter (D3) |

- Every title follows the existing "Couldn't <verb> <anime>" form.
- "to list" matches the detail page's own add. `updateEntry(id, {})` adds with the backend's default status, so naming a status would be a guess.
- "reset" names the action the picker's **Default** button takes. It matches the handler and client names (`resetAnimePicture`, `resetSeriesPicture`).
- The title pick names both titles. The page is already showing the new one (D2), so a notice naming only the old title would read as if it referred to something else.

*Alternatives considered:*
- **One generic "Couldn't save your change to X" everywhere.** Rejected. The spec asks the notice to say *what* didn't happen, and on the series page three different actions share one series name.

### D2. Only the notice is added: no site changes what it does to the page

Each catch gains `reportFailure(...)` and nothing else:
- The score select still snaps back through its controlled value.
- "Add" stays "Add".
- The favourite order still reverts to `currentOrder`.
- An optimistic picture or title pick stays on screen until the next reload.

The brief fixes this. The spec delta doesn't state what the page shows after a failed optimistic pick. It only requires the notice, so a later rollback change needs no further spec change.

**The error-capture shape.**
- A `try`/`catch` becomes `catch (err)`.
- A `.catch(() => { … })` becomes `.catch((err) => { … })`.
- Both use `reason: err instanceof ApiError ? err.reason : null`, exactly as at `AnimeDetailPage.tsx:232`.
- The promise chains stay promise chains, as the surrounding code writes them.
- Each existing comment ("a later refresh/reload re-syncs", "the user can retry") is rewritten to say the notice accounts for the failure. On the optimistic picks it also says the choice stays on screen until a reload.

*Alternatives considered:*
- **Roll back optimistic picks in the same change.** Rejected here, and left as an Open Question.
  - The brief rules it out.
  - It needs decisions of its own: what to restore the series picture to when the pre-pick value was the root member's default, and whether a pick made in the meantime should be restored over.

### D3. Wiring per page

- **`MyListPage.tsx`**
  - Import `ApiError` from `../api/client.ts` and `useActionFailure` from `../context/ActionFailureContext.tsx`.
  - Call `const reportFailure = useActionFailure()` beside the other context hooks.
  - Add `reportFailure` to `changeScore`'s dependency list. It's stable (Context), so `changeScore`'s identity doesn't change and `MyListRow`'s memo still bites.
- **`TopAnimePage.tsx`**: the same two imports and the hook call. `handleAdd` is a plain function.
- **`AnimeDetailPage.tsx`**
  - Already wired.
  - `handleRefresh`'s guard becomes `if (!detail || refreshing) return;`, the same guard its sibling handlers use, so the title can read `detail`. No behaviour changes, because the button never renders without `detail`.
- **`SeriesPage.tsx`**
  - The same two imports, and the hook call in `SeriesPage`.
  - `handleReorderFavourite` gains a fourth parameter, `movedTitle: string`. Both call sites pass `pickDisplayTitle(entry.title, entry.englishTitle)`, the value their `aria-label`s already compute.
  - The three series-picker handlers read the series display title before patching.

*Alternatives considered:*
- **Look up the moved entry inside `handleReorderFavourite`** with `findEntry(data.series, currentOrder[index])`. Rejected. The handler would need a `data?.found` narrowing it does without today, and an `undefined` fallback for a title the caller already holds.

### D4. A reload that fails after a successful refresh stays silent

`handleRefresh` awaits `refreshAnime`, then `reload()`. `reload()` never rejects, because `usePageData`'s `runLoad` catches and keeps the data already on screen. So only a failed `refreshAnime` reaches the new notice.

If the refresh succeeds and the re-read fails, the page keeps its older copy with no notice. That's accepted:
- the action itself did take effect on the server
- the next visit reads the refreshed data
- a failed read with data already on screen is `usePageData`'s shared, deliberate behaviour on every page

*Alternatives considered:*
- **Make `reload()` reject.** Rejected. It would change the contract for every page that calls it, for a case that isn't a failed action.

### D5. The spec lists the newly covered controls and names "anime or series"

The `action-failure-notices` delta rewrites "A failed action is never silent":
- It adds the six controls to the "at minimum" list.
- It changes "name the anime" to "name the anime or series".
- It adds one sentence: a save that fails after the page already showed the choice still raises a notice.
- It adds one scenario per control group.

Why a delta and not only code:
- The "at minimum" list reads as the inventory of what is covered. Leaving the six out would make the spec look as if they aren't.
- `/opsx:verify` checks the implementation against scenarios.
- "Name the anime" doesn't literally fit the three series-level actions.

`series-page`'s "A failed save does not stick" is left alone. The notice rule lives in one capability, and the new favourite-reorder scenario covers the notice from there.

*Alternatives considered:*
- **No spec change**, relying on "at minimum" being a floor. Rejected, for the "anime or series" wording and for verifiability.
- **Adding the notice to `series-page` and `anime-detail` as well.** Rejected. It would put the same rule in three places.

### D6. Re-read only while the connect screen is shown

In `App.tsx`, a second effect is keyed on whether the state is `NotConnected`:

```tsx
const showingConnectScreen = status?.state === 'NotConnected'

useEffect(() => {
  if (!showingConnectScreen) return
  function recheck() {
    if (document.visibilityState !== 'visible') return
    getMalAuthStatus()
      .then(setStatus)
      .catch(() => {
        // D7: keep the connect screen; the next return retries.
      })
  }
  window.addEventListener('focus', recheck)
  document.addEventListener('visibilitychange', recheck)
  window.addEventListener('pageshow', recheck)
  return () => {
    window.removeEventListener('focus', recheck)
    document.removeEventListener('visibilitychange', recheck)
    window.removeEventListener('pageshow', recheck)
  }
}, [showingConnectScreen])
```

- When a read returns `Connected` or `Lost`, the effect's cleanup removes the listeners and `<AppShell />` renders.
- The mount effect is unchanged.

**Why not re-read once the shell is showing.**
- A read answering `NotConnected` would unmount `AppShell`, and every provider's state with it.
- A lost login is already reported inside the app: `AppStatusProvider` polls `malConnection`, and Settings explains it (`mal-api-integration`, "A lost connection SHALL leave me in the app").

**Why not from the backend-down screen.** It tells you to reload, and N2 doesn't cover it.

**Duplicate reads.** Returning to a tab often fires `focus` and `visibilitychange` together. Both reads are the same GET, so `fetchRaw` joins the second onto the first and one request goes out. No in-flight guard is needed. The endpoint never contacts MAL either (`mal-api-integration`, "Reading the state SHALL NOT contact MyAnimeList").

*Alternatives considered:*
- **Always listen, as `AppStatusContext` does.** Rejected, for the unmount risk above.
- **Poll on a timer while the connect screen is shown.** Rejected. It adds requests while the tab sits idle, and returning to the tab is the moment that matters.
- **Redirect the OAuth callback to the frontend** (the triage doc's other direction). Rejected for this change. It's a backend change, and it still leaves the "another app tab is already open" case needing a re-read.

### D7. A failed re-read keeps the connect screen

The re-read's `.catch` doesn't set `statusError`. Only the first read decides between the backend-down screen and the rest.
- The backend can be briefly unreachable while you're away on MAL's site, for example restarting.
- Swapping the connect screen for "Can't reach the backend … reload this page" would be wrong as soon as it came back.
- The next return to the tab tries again.

This catch is silent on purpose. It isn't an action taken on your behalf, so `action-failure-notices` doesn't apply, and `App` sits outside `ActionFailureProvider` anyway.

`CODE_GUIDE.md` §4 says the backend-down screen "covers only the very first `getMalAuthStatus()` call". That stays true.

### D8. `pageshow` alongside `focus` and `visibilitychange`

The N2 problem names going Back as one of the two ways back to the app tab.
- If the browser restores the app from the back/forward cache, `useEffect` doesn't run again, since nothing remounts.
- Browsers don't agree on whether that restore fires `visibilitychange` or `focus`. `pageshow` is the event that reliably fires on a restore.
- If the page isn't cached, Back reloads the app, and the mount read covers it.
- The `visibilityState` guard is harmless for `pageshow`, since the page is visible when it fires.

*Alternatives considered:*
- **Only `focus` and `visibilitychange`, exactly as `AppStatusContext`.** Rejected. Whether Back works would then depend on the browser.

### D9. Checked by hand

Each Tests bullet in the brief becomes a hand-check in tasks.md.

**PF2.**
- Block the request's URL in Chrome DevTools (Network → Block request URL). The fetch then rejects at the network level.
- The UI state is unchanged, and a notice appears with the D1 title and no reason. The connection-status bar also appears. That's expected for a network-level failure and was already checked not to overlap the notice.
- The reason half is checked by reading. Every site uses the same expression as `AnimeDetailPage.tsx:232`, which the change that added the mechanism already exercised.
- A server-stated rejection isn't reachable from these controls. The series title picker checks the trim rule before it sends (`components/SeriesTitlePickerOverlay.tsx:13-21`).

**N2.**
- Use a DevTools Local Override of `/api/mal-auth/status` to answer `{"state":"NotConnected","lostAt":null}`, then remove it and return to the tab.
- Don't delete the stored MAL login to get this state. That disconnects the real account.

## Risks / Trade-offs

- **[A failed optimistic pick stays on screen]**
  - After a failed picture or title save, the page shows a choice the server doesn't have until you reload. The notice is the only sign.
  - → The notice names the action and the anime or series, and it lasts long enough to read. Rolling back is an Open Question.
- **[A network failure shows two notices]**
  - A blocked or unreachable request raises the connection-status bar and a failure notice together.
  - → Existing behaviour, shared with "+". The two sit apart, as checked when the notice was added.
- **[Several notices at once]**
  - Clicking several My List scores while the backend is down raises one notice each.
  - → `ActionFailureProvider` caps the stack at three (`MAX_NOTICES`). Each notice dismisses itself after 8 s, and a page change clears them.
- **[A stale answer to a joined read]**
  - If a re-read is already in flight when authorization completes, a return that joins it gets the pre-authorization answer.
  - → The next focus, visibility change or `pageshow` reads again. Getting there needs a return to the tab while the MAL flow was still going.
- **[`pageshow` fires on the first load too]**
  - `pageshow` fires on a normal load as well, after `load`. The listener is only attached once the first read has resolved to `NotConnected`. If that read wins the race against `load`, the first `pageshow` triggers one extra read.
  - → At most one extra read of a cheap endpoint that never contacts MAL, and it answers the same thing.
- **[Hand checks can regress unnoticed]**
  - Nothing automated guards these catch blocks.
  - → Accepted with the user. The sites are one-line calls in a shape the repo already uses.

## Migration Plan

Frontend only. No data, API or configuration change. Rolling back means reverting the two commits.

## Open Questions

- **Should a failed optimistic picture or title pick roll back?** It's left as it is by the brief. If wanted, it's its own small change:
  - the detail page can restore the pre-pick `pictureUrl` and `selectedPictureUrl`
  - the series picture pick can restore the same two fields
  - a later successful pick must not be overwritten by an earlier pick's rollback
- **Should `SeriesPage.tsx` `handleRebuild` (`:576`) report its failures too?** It fails silently on the same terms as the Refresh button this change fixes, but PF2 doesn't list it. If wanted, it's a one-call follow-up.
