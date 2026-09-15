Paths without a prefix are under `frontend/src/`. Line numbers are as of `7c6c0e9`. Check each one against the file before editing.

Groups 1–3 are PF2, and groups 4–6 are N2. Committing and archiving are done by hand after group 6, so no task here covers them.

**Build and lint** with nvm's Node 22, since the default `node` is v16. Run from `frontend/`: `PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build`, then the same prefix with `npm run lint`.

**Checks are by hand** (design D9). The frontend has no test runner. To make a request fail, use Chrome DevTools → Network → right-click a request → **Block request URL**, or add a pattern in the Network request blocking panel. A blocked request fails at the network level, so each notice shows **no reason**, and the connection-status bar appears too. Both are expected.

## 1. PF2: My List and Top Anime (design D1–D3)

- [x] 1.1 In `pages/MyListPage.tsx`:
  - add `ApiError` to the `../api/client.ts` import, and import `useActionFailure` from `../context/ActionFailureContext.tsx`
  - in `MyListPage`, call `const reportFailure = useActionFailure()` beside the other context hooks (`useEntryEditor`, `useEpisodeIncrement`)
- [x] 1.2 In `changeScore` (`:375-388`):
  - change the bare `catch {` at `:383` to `catch (err) {`
  - call `reportFailure({ title: \`Couldn't update the score for ${pickDisplayTitle(item.title, item.englishTitle)}\`, reason: err instanceof ApiError ? err.reason : null })`
  - rewrite the comment: the select snaps back to the saved score through its controlled value, and the notice accounts for it (action-failure-notices)
  - add `reportFailure` to the dependency list (`[setItems, reportFailure]`). It's stable, so `changeScore` keeps its identity and `MyListRow`'s memo still holds (design D3).
- [x] 1.3 In `pages/TopAnimePage.tsx`:
  - add `ApiError` to the `../api/client.ts` import, and import `useActionFailure`
  - call `const reportFailure = useActionFailure()` in the page component
  - in `handleAdd` (`:150-161`), change the bare `catch {` to `catch (err) {` and call `reportFailure({ title: \`Couldn't add ${pickDisplayTitle(item.title, item.englishTitle)} to list\`, reason: err instanceof ApiError ? err.reason : null })`
  - rewrite the comment: the button stays "Add", and the notice says why

## 2. PF2: the detail page and the series page (design D1–D3)

- [x] 2.1 In `pages/AnimeDetailPage.tsx` `handleRefresh` (`:196-207`):
  - change the guard to `if (!detail || refreshing) return;`. The button only renders once `detail` exists (`:367`), so this changes nothing.
  - change `catch {` to `catch (err) {` and call `reportFailure({ title: \`Couldn't refresh ${pickDisplayTitle(detail.title, detail.englishTitle)}\`, reason: err instanceof ApiError ? err.reason : null })`
  - rewrite the comment:
    - the page keeps what was already cached, and the notice says the refresh didn't happen
    - `reload()` never rejects, so only a failed `refreshAnime` gets here (design D4)
  - leave `handleRetry` (`:213-221`) untouched
- [x] 2.2 In `handlePickAnimePicture` (`:265-278`) and `handleClearAnimePicture` (`:283-297`):
  - change `.catch(() => {` to `.catch((err) => {`
  - call `reportFailure` with `` `Couldn't save the picture for ${…}` `` and `` `Couldn't reset the picture for ${…}` `` respectively, using `pickDisplayTitle(detail.title, detail.englishTitle)` and the same `reason` expression
  - rewrite both comments: the optimistic change stays on screen until a reload, and the notice is what tells you it wasn't saved (design D2)
- [x] 2.3 In `pages/SeriesPage.tsx`:
  - add `ApiError` to the `../api/client.ts` import, and import `useActionFailure` from `../context/ActionFailureContext.tsx`
  - in `SeriesPage`, call `const reportFailure = useActionFailure()` beside `useEntryEditor`
- [x] 2.4 In `handleReorderFavourite` (`:592-608`):
  - add a fourth parameter, `movedTitle: string`
  - change the `catch {` at `:605` to `catch (err) {`. Keep the `patchSeries` revert first, then call `reportFailure({ title: \`Couldn't move ${movedTitle} in your favourites\`, reason: err instanceof ApiError ? err.reason : null })`.
  - extend the leading comment's "A failed save reverts to the order that was actually stored" with "and says so"
  - at both call sites in the favourites list (`:1186` and `:1196`), pass `pickDisplayTitle(entry.title, entry.englishTitle)` as the new argument
- [x] 2.5 In `handlePickSeriesPicture` (`:613-621`), `handleClearSeriesPicture` (`:626-636`) and `handlePickSeriesTitle` (`:638-646`):
  - after the `data?.found` guard and before any `patchSeries`, read `const seriesTitle = pickDisplayTitle(data.series.title, data.series.englishTitle)`
  - change each `.catch(() => {` to `.catch((err) => {` and call `reportFailure` with the same `reason` expression and these titles:
    - `` `Couldn't save the picture for ${seriesTitle}` ``
    - `` `Couldn't reset the picture for ${seriesTitle}` ``
    - `` `Couldn't rename ${seriesTitle} to ${title}` ``
  - rewrite the comments as in 2.2. On the reset, which isn't optimistic, say the page keeps its current picture and the notice says why.
- [x] 2.6 Grep `pages/MyListPage.tsx`, `pages/TopAnimePage.tsx`, `pages/AnimeDetailPage.tsx` and `pages/SeriesPage.tsx` for `catch {` and `.catch(() =>`. Exactly three hits should be left, all named in design Non-Goals:
  - `AnimeDetailPage.tsx`'s picture backfill (`:190` before this change)
  - `SeriesPage.tsx`'s picture backfill (`:454`)
  - `SeriesPage.tsx` `handleRebuild` (`:576`)

  `MyListPage.tsx` and `TopAnimePage.tsx` should have none.

## 3. PF2: build, check and docs

- [x] 3.1 Build and lint (see the top of this file). Both must pass with no new warnings.
- [x] 3.2 Check each site by hand against the running app. For each one: block the URL, do the action, check the page state and the notice (title as in design D1, no reason line), then unblock.
  - **My List score:** block `*/api/anime/*/entry`. Pick a different score on a row. The select returns to the previous score, and the notice reads "Couldn't update the score for <title>".
  - **Top Anime Add:** block `*/api/anime/*/entry`. Press Add on an anime not in the list. The button still says Add, and the notice reads "Couldn't add <title> to list".
  - **Detail Refresh:** block `*/api/anime/*/refresh`. Press "Refresh data". The page keeps its content, the button returns to "Refresh data", and the notice reads "Couldn't refresh <title>".
  - **Detail picture pick and reset:** on an anime with "Choose picture", block `*/api/anime/*/picture`.
    - Pick a picture: it shows, and the notice reads "Couldn't save the picture for <title>". Unblock and reload: the stored picture is back.
    - Block again and press **Default**: the notice reads "Couldn't reset the picture for <title>".
  - **Series picture pick and reset:** on a series page, block `*/api/series/*/picture`. Pick a picture, then use **Default**. The notices read "Couldn't save the picture for <series>" and "Couldn't reset the picture for <series>".
  - **Series title:** block `*/api/series/*/title`. Choose a different title. The notice reads "Couldn't rename <old title> to <new title>".
  - **Favourite reorder:** on a series with two or more tied favourites, block `*/api/rankings/move-adjacent`. Press ▲ or ▼. The order returns to what it was, and the notice reads "Couldn't move <the anime you pressed> in your favourites".
  - **Success path unchanged:** with nothing blocked, refresh a detail page, then pick a picture and press **Default** to put it back. No notice appears. Don't check the score, Add or reorder success paths this way: their `try` branches are untouched, and a score or an add is pushed to the real MyAnimeList list.

  The server's reason isn't reachable from these controls (design D9). Check it by reading instead: every new call uses `err instanceof ApiError ? err.reason : null`, as at `AnimeDetailPage.tsx:232`.
- [x] 3.3 In `docs/ISSUE_TRIAGE.md` (local, gitignored), following the pattern Phase 5 used:
  - replace the PF2 entry (`:171-186`) with a short blockquote pointing to the resolved file
  - update the Summary table: "Partly fixed" count and the resolved row
  - mark item 11 of Phase 6 done
  - update the appendix row for ISSUES #20 (`:387`)
  - fix the "more silent catches in PF2" note (`:469`)
- [x] 3.4 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## PF2.` section under a code-fixes heading for 2026-09-15 (add the heading if it's missing, in the style of "Resolved 2026-09-14 (code fixes)"). Name this change. Record:
  - the nine sites, with the six controls they cover
  - what the page does on failure is unchanged
  - the remaining gaps: a failed optimistic picture or title pick stays on screen until a reload, and `handleRebuild` is still silent (design Open Questions)

## 4. N2: re-read the connection state on return (design D6–D8)

- [x] 4.1 In `App.tsx`, below the mount effect, add the effect from design D6:
  - `const showingConnectScreen = status?.state === 'NotConnected'`
  - an effect on `[showingConnectScreen]` that returns early unless it's true
  - otherwise it attaches one `recheck` to `window` `focus`, `document` `visibilitychange` and `window` `pageshow`, and removes all three on cleanup
  - `recheck` returns unless `document.visibilityState === 'visible'`, then calls `getMalAuthStatus().then(setStatus).catch(() => { … })`

  Leave the mount effect and the render branches unchanged.
- [x] 4.2 Comment the new effect:
  - authorizing leaves the tab, or happens in another one, and the callback page doesn't redirect back, so the connect screen re-reads when you return (`mal-api-integration`, "The connection state is reported")
  - it's the focus and visibility pattern from `context/AppStatusContext.tsx`, plus `pageshow`, because a Back navigation restored from the back/forward cache doesn't remount and isn't reliably followed by the other two (design D8)
  - it listens only while the connect screen is showing, because a re-read inside the shell could unmount it, and a lost login is reported in the app (design D6)
  - identical GETs already in flight are joined in `api/client.ts`, so `focus` and `visibilitychange` firing together make one request
- [x] 4.3 Comment the re-read's `.catch`: it keeps the connect screen rather than setting `statusError`, because only the first read decides the backend-down screen, and the next return retries (design D7).

## 5. N2: build and check

- [x] 5.1 Build and lint. Both must pass with no new warnings.
- [x] 5.2 **Don't delete the stored MAL login** to get the connect screen. That disconnects the real account. Fake the state instead:
  - in DevTools → Sources → Overrides, enable Local Overrides
  - override the response of `/api/mal-auth/status` with `{"state":"NotConnected","lostAt":null}`
  - reload: the connect screen shows
- [x] 5.3 **Returning while still not connected.** With the override on:
  - switch to another tab and back
  - the Network panel shows a new `/api/mal-auth/status` request, usually just one, since `focus` and `visibilitychange` are joined
  - the connect screen stays
- [x] 5.4 **Returning after "authorizing" by switching tabs.** With the connect screen showing, turn Local Overrides off, then switch to another tab and back. The app opens without a reload.
- [x] 5.5 **A failed read on return.** Turn the override back on and reload to the connect screen.
  - Block `*/api/mal-auth/status`, then switch away and back. The connect screen stays, and "Can't reach the backend" doesn't appear.
  - Unblock, turn the override off, and switch away and back. The app opens.
- [x] 5.6 **Returning by Back.** With the override on and the connect screen showing:
  - navigate the same tab to another address, such as `about:blank`
  - turn the override off, then press Back. The app opens.
  - DevTools → Application → Back/forward cache says whether the page was restored from the cache. The Vite dev server's HMR connection can make the page ineligible. If it wasn't restored, repeat this against the Docker-served frontend (`http://127.0.0.1:${FRONTEND_PORT:-5173}`) so the `pageshow` path is exercised.
- [x] 5.7 **No re-reads once the app is open.** With the app showing, switch away and back several times. No `/api/mal-auth/status` request appears. `/api/app-status` polling carries on as before.
- [x] 5.8 Confirm every Local Override and request block from groups 3 and 5 is removed.

## 6. N2: docs and validate

- [x] 6.1 In `CODE_GUIDE.md` §4 "Entry, routing, contexts", extend the `main.tsx` → `App.tsx` bullet (`:862-863`): while the connect screen is showing, the app re-reads the status on focus, visibility and `pageshow`, and a failed re-read keeps the connect screen. Confirm the `ConnectionStatusNotice` bullet's "covers only the very first `getMalAuthStatus()` call" (`:975-977`) still reads true (design D7).
- [x] 6.2 Run `openspec validate fix-silent-actions-and-mal-status-recheck --strict`, and fix anything it reports.
- [x] 6.3 In `docs/ISSUE_TRIAGE.md`:
  - replace the N2 entry (`:145-150`) with a short blockquote pointing to the resolved file
  - update the Summary table's "Still present" count and the resolved row
  - mark item 13 of Phase 6 done. Phase 6 as a whole stays open for PF5.
  - update the N2 half of the ISSUES #21 row (`:388`)
- [x] 6.4 In `docs/ISSUE_TRIAGE_RESOLVED_2026-09-13.md`, add a `## N2.` section after PF2. Name this change. Record:
  - the re-read happens only while the connect screen is showing
  - why `pageshow` was added
  - a failed re-read keeps the connect screen
  - the OAuth callback still doesn't redirect back
