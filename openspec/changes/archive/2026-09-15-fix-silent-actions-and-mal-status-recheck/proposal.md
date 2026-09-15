## Why

This change fixes two items from `docs/ISSUE_TRIAGE.md`, Phase 6 ("Frontend polish") of its fix order. They are small and independent of each other.

- **PF2.** Six controls still fail without a word. When the request behind them fails, a picked score snaps back, "Add" stays "Add", and a favourite reorder rolls back, all with no message. A chosen picture or series title looks saved, then reverts on reload. `action-failure-notices` already requires every such failure to be reported, and the "+" button, the inline episode count and the detail page's add buttons already do this.
- **N2.** The "Connect to MAL" screen reads the connection state once, on mount. After you authorize, going Back to the app tab, or switching to one already open, still shows the connect screen until you reload.

PF5 (the stale Home carousel after a rewatch) is in the same phase but is a separate, larger change that redesigns the completion-score prompt. It is not part of this change.

**Verified against the code** (2026-09-15, at `7c6c0e9`). Paths without a prefix are under `frontend/src/`.

- **PF2: nine silent catch sites in four files.**
  - `pages/MyListPage.tsx:375-388` `changeScore`: bare `catch {}` at `:383-385`. The select is controlled by `item.entry.myScore` (`components/MyListRow.tsx:80`), so it snaps back with no notice.
  - `pages/TopAnimePage.tsx:150-161` `handleAdd`: bare `catch {}` at `:156-158`.
  - `pages/AnimeDetailPage.tsx:196-207` `handleRefresh`: bare `catch {}` at `:202-204`. `reload()` never rejects, because `usePageData`'s `runLoad` swallows its own failure (`hooks/usePageData.ts:95-98`). So this catch only ever sees a `refreshAnime` failure.
  - `pages/AnimeDetailPage.tsx:265-278` `handlePickAnimePicture` (`.catch(() => {})` at `:274-277`) and `:283-297` `handleClearAnimePicture` (`:294-296`). Both apply the change optimistically and don't roll it back.
  - `pages/SeriesPage.tsx:613-621` `handlePickSeriesPicture` (`:617-620`), `:626-636` `handleClearSeriesPicture` (`:633-635`) and `:638-646` `handlePickSeriesTitle` (`:642-645`). The picture and title picks are optimistic and don't roll back. The clear waits for the response.
  - `pages/SeriesPage.tsx:592-608` `handleReorderFavourite`: the `catch {}` at `:605-607` restores `currentOrder` with no notice.
  - `AnimeDetailPage.tsx` already imports `ApiError` and `useActionFailure` (`:4,27`) and calls `reportFailure` from `:169`. `MyListPage.tsx`, `TopAnimePage.tsx` and `SeriesPage.tsx` import neither.
  - All four pages render inside `ActionFailureProvider` (`AppShell.tsx:42-71`).
  - Spec: `openspec/specs/action-failure-notices/spec.md:9` covers "every action", its `:11` list starts "at minimum", and `:15` says the app "SHALL NOT silently roll an action back and say nothing". `:13` requires the notice to name "the anime", but three of these actions act on a series.
- **N2: the connection state is read once.**
  - `App.tsx:11-15` calls `getMalAuthStatus()` in a mount-only `useEffect`.
  - The connect link (`:47`) navigates the same tab to `/api/mal-auth/start`. The callback page says "You can close this tab and return to the app." (`backend/AnimeTracker.Api/Controllers/MalAuthController.cs:58`) and doesn't redirect.
  - `context/AppStatusContext.tsx:66-76` already re-reads on `window` `focus` and `document` `visibilitychange`.
  - Spec: `openspec/specs/mal-api-integration/spec.md:323` says when the connect screen is shown, but not that it notices the state changing.
  - Reading the state never contacts MAL (`:321`), so re-reading it is cheap.

## What Changes

- **PF2: every remaining silent catch reports through `useActionFailure()`.**
  - `MyListPage.tsx`, `TopAnimePage.tsx` and `SeriesPage.tsx` import `ApiError` and `useActionFailure`, and call `const reportFailure = useActionFailure()` once in the page component.
  - Each of the nine catches captures its error and calls `reportFailure({ title, reason: err instanceof ApiError ? err.reason : null })`.
  - Each title names the anime or series and the action that didn't happen, in the style of `AnimeDetailPage.tsx:230-233`. Design D1 lists them.
  - What each control does to the page on failure is unchanged: the score snaps back, the button stays "Add", the reorder rolls back, and an optimistic picture or title pick stays on screen. The only change is the notice.
  - `handleRetry` on the detail page, and `handleRebuild` and the other silent catches outside the triage list, are unchanged (design Non-Goals).
- **N2: the connect screen re-reads the connection state when you come back to it.**
  - While the state is `NotConnected`, `App.tsx` re-reads `/api/mal-auth/status` when the window regains focus, when the document becomes visible, and when the page is shown again from the back/forward cache.
  - When the state has become connected (or lost), the app opens with no reload.
  - A failed re-read leaves the connect screen in place. Only the first read's failure shows the "Can't reach the backend" screen.
  - Nothing re-reads once the app shell is showing. `AppStatusProvider` already polls the connection for a lost login.

## Capabilities

### New Capabilities

None.

### Modified Capabilities

- `action-failure-notices`:
  - **Modified:** "A failed action is never silent". The "at minimum" list adds the six controls: the My List score control, Top Anime's add, the detail page's refresh, choosing or resetting a picture on a detail or series page, choosing a series title, and reordering tied favourites on a series page. A notice names "the anime or series" the action was taken on. Adds scenarios for a score that snaps back, a picture or title choice that doesn't save, and a favourite reorder that rolls back.
- `mal-api-integration`:
  - **Modified:** "The connection state is reported". While the connect screen is shown, the app re-reads the state whenever I return to it, so authorizing opens the app with no reload. A failed re-read leaves the connect screen in place. Adds scenarios for returning after authorizing, and for returning while still not connected.

## Impact

- **Frontend** (under `frontend/src/`):
  - `pages/MyListPage.tsx`, `pages/TopAnimePage.tsx`, `pages/AnimeDetailPage.tsx`, `pages/SeriesPage.tsx`: PF2
  - `App.tsx`: N2
- **Backend:** none. No API, database or migration change.
- **Dependencies:** none. The frontend has no test runner, so the checks are by hand in the browser, plus `npm run build` and `npm run lint`, as with earlier frontend changes (design D8).
- **Docs:**
  - `CODE_GUIDE.md`: the `App.tsx` bullet in §4 "Entry, routing, contexts"
  - `docs/ISSUE_TRIAGE.md` and `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md` (local, gitignored): PF2 and N2 move to resolved
- **Commits:** two.
  1. PF2: the four pages.
  2. N2: `App.tsx`, then archiving this change, which writes both capabilities' spec deltas.
- **Out of scope:**
  - PF5, and the Home-only completion-revert item that goes with it. They are a separate change.
  - PF6 and N1, marked "probably skip" in the triage doc.
  - Rolling back an optimistic picture or title pick when its save fails (design Open Questions).
  - Silent catches the triage doc doesn't name, such as `SeriesPage.tsx` `handleRebuild` (`:576`) (design Non-Goals).
  - Redirecting the OAuth callback back to the frontend.
