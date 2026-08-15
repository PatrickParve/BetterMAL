## 1. Page-scroll recording and restore

- [x] 1.1 In `frontend/src/hooks/useScrollRestoration.ts`, replace the `requestAnimationFrame`-deferred recorder with a synchronous one: keep a `keyRef` assigned during render from `usePageState().key`, and have the `scroll` listener call `pageStateStore.putScroll(keyRef.current, window.scrollY)` directly (design.md decision 1). Import the store rather than closing over `snapshot`, and leave a comment recording why the frame delay and the captured snapshot were the cross-entry-write bug.
- [x] 1.2 In the restore layout effect, drop the `programmatic` flag and the `scroll`-based cancellation; cancel the retry loop on `wheel`, `touchstart`, and `keydown` for the scrolling keys (arrows, page up/down, home/end, space) instead (design.md decision 2). Register them `passive` where applicable and remove them in the cleanup alongside the existing `cancelAnimationFrame`.
- [x] 1.3 Raise `RETRY_BUDGET_MS` to 1500 and update its comment to say what the budget is now for (a section whose data was never seeded), noting the loop still stops on the first frame the target is reachable.
- [x] 1.4 Verify the reported symptom is gone: scroll the profile page down, click a poster tile, then go back — repeatedly, including cases where the click follows a trackpad scroll immediately. The page returns to its previous position every time, with no flash at the top.

## 2. Snapshot store

- [x] 2.1 In `frontend/src/state/pageStateStore.ts`, add `strips: Map<string, number>` to `PageSnapshot` (initialised in `ensure`) and a `putStripScroll(key, stripKey, offset)` accessor beside `putScroll` (design.md decision 3).
- [x] 2.2 Make eviction least-recently-used: have `get` and `ensure` re-insert the snapshot they return (delete-then-set) so `Map` insertion order tracks recency, and raise `MAX_ENTRIES` to 50 (design.md decision 9). Update the comment above the constant to say why LRU rather than oldest-created.

## 3. Strip scroll restoration

- [x] 3.1 In `frontend/src/pages/ProfilePage.tsx`, give `useDragScroll` a required `restoreKey: string` argument and rename it to reflect that it now owns the strip's scrolling as well as its drag behaviour. Keep the existing drag logic and returned `handlers`/`onItemClick` shape unchanged.
- [x] 3.2 Inside that hook, record the element's offset: an element-level `scroll` listener writing `el.scrollLeft` through `pageStateStore.putStripScroll` under the current history-entry key and `restoreKey`, synchronously (design.md decision 3).
- [x] 3.3 Apply the recorded offset on a restore only: a layout effect that reads the snapshot's `strips` entry and assigns `el.scrollLeft`, re-applying while the element's `scrollWidth` is still short of the target and stopping once it is reached or the user scrolls the strip. On a fresh visit it must do nothing.
- [x] 3.4 Pass the section keys at each call site: `top-anime:${mediaType}`, `top-series`, and `rewatched:${rewatchedMediaType}` — matching the `usePageData` keys so a restored media-type tab restores its own offset.
- [x] 3.5 Verify: scroll a strip well past its start, open a tile, go back — the strip is where it was, the page's vertical position is too, and the other strips are unaffected. Switch media-type tabs, scroll, leave and return — the restored tab shows its own offset. Reach the page from the navbar — every strip starts at its first tile.

## 4. Strips that fit do not scroll

- [x] 4.1 In `ProfilePage.tsx`, declare `const STRIP_VISIBLE_TILES = 10` with a comment tying it to the tile `flex` basis in `ProfilePage.css` that it must agree with (design.md decision 4).
- [x] 4.2 Add a `--fits` modifier to each of the three strips' class names when the rendered entry count is `<= STRIP_VISIBLE_TILES` (top anime, top series after both narrowings, most rewatched).
- [x] 4.3 In `ProfilePage.css`, define the modifier on all three strips: `overflow-x: hidden` and `cursor: default` (and no `grabbing` cursor on `:active`), leaving the padding, gap, and tile sizing untouched so nothing shifts when a strip crosses the threshold.
- [x] 4.4 Verify: on a media-type tab with exactly ten entries the strip cannot be dragged or wheeled in either direction and shows no grab cursor; switching to a tab with more than ten restores dragging; the tenth tile is not visibly clipped in either state.

## 5. Hidden scrollbars

- [x] 5.1 In `ProfilePage.css`, replace `scrollbar-width: thin` with `scrollbar-width: none` on `.top-anime-strip`, `.top-series-strip`, and `.rewatched-strip`, and add a `::-webkit-scrollbar { display: none }` rule for each (design.md decision 5). Leave the `.activity-feed`/history-overlay scrollbars alone.
- [x] 5.2 Verify no bar appears under the posters at rest or while scrolling, in Chrome/Chromium and Safari, and that the "Latest updates" feed still shows its scrollbar beside its rows.

## 6. Top series watched-coverage rule

- [x] 6.1 In `backend/AnimeTracker.Api/Services/Series/SeriesRankingIndex.cs`, derive the aired main line once (`mainLine.Where(m => m.AiringStatus != "not_yet_aired")`), reuse it for the existing `mainLineAiredCount`, and skip any series where that list has two or more entries and at most one of them has a non-null `EntryStatus` (design.md decisions 6 and 7). Comment the rule with what it is for — an average over one entry does not describe a franchise — and that any status counts as coverage.
- [x] 6.2 Leave `SeriesRankingResult`, `TopSeriesItemDto`, the endpoint, and the frontend types unchanged; no `MainLineWatchedCount` field is added (design.md decision 6).
- [x] 6.3 Add cases to `backend/AnimeTracker.Api.Tests/Services/Profile/ProfileServiceTopSeriesTests.cs` (extending the `AddSeries` helper with per-member airing status and entry status if it does not already take them): three aired main-line entries with one in my list → excluded; the same series with two in my list → included; one aired main-line entry plus a `not_yet_aired` one, the aired one in my list → included; two aired main-line entries, one completed and one plan-to-watch → included; two aired main-line entries with only an extra in my list → excluded.
- [x] 6.4 Check the existing suite for cases that assume the old eligibility rule (`SeriesWithNoListMemberIsExcluded`, the ordering tests' fixtures) and adjust fixtures — not assertions — where a fixture series would now be filtered out.

## 7. Verification

- [x] 7.1 Frontend build and lint clean (`nvm use 22` first — the default node is v16 and Vite's build needs 22): `npm run build` and `npm run lint` in `frontend/`.
- [x] 7.2 Backend build and full test suite clean (`dotnet build` / `dotnet test` via the SDK 10 Docker image, since the local SDK is 9.0).
- [x] 7.3 Against the real profile: confirm Top series no longer lists franchises with two or more aired main-line seasons and at most one in the list (To Your Eternity and the like), still lists Cyberpunk: Edgerunners and other one-aired-season series, and that the "Multi-entry only" toggle and the ranking basis still work over the reduced set.
- [x] 7.4 Walk the whole page once more end to end: scroll position and all three strip offsets restore on back, none of the strips shows a scrollbar, and a ten-entry strip is inert while an eleven-entry one drags.
