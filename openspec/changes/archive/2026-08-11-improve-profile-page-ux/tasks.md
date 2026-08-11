## 1. Page-state restoration foundation

- [x] 1.1 Add `frontend/src/state/pageStateStore.ts`: a module-level `Map<string, PageSnapshot>` keyed by history entry, where `PageSnapshot = { data: Map<string, unknown>, view: Map<string, unknown>, scrollY: number }`, with `get`/`ensure`/`putData`/`putView`/`putScroll` accessors and insertion-ordered eviction at 30 entries (D1)
- [x] 1.2 Add `frontend/src/state/PageStateContext.tsx`: a provider that reads `useLocation().key` and `useNavigationType()`, exposes `{ key, isRestore, snapshot }` where `isRestore` is `navigationType === 'POP'` **and** a snapshot exists for that key, and ensures a snapshot for the current key (D2)
- [x] 1.3 Add `frontend/src/hooks/usePageData.ts` — `usePageData<T>(key, load)` returning `{ data, loading, setData, reload }`: seeds from the snapshot and refreshes in the background when restoring, loads with `loading: true` otherwise, and writes through to the snapshot on every state change (D3)
- [x] 1.4 Add `frontend/src/hooks/useRestorableState.ts` — a `useState`-shaped hook that seeds from the snapshot's view map when restoring and writes through on every change (D3)
- [x] 1.5 Add `frontend/src/hooks/useScrollRestoration.ts`: records `window.scrollY` into the current snapshot on scroll (rAF-throttled), scrolls to top on `PUSH`/`REPLACE`, and on a restoring `POP` applies the saved offset in a layout effect with an animation-frame retry that aborts on success, on a user scroll, or after ~500 ms (D4)
- [x] 1.6 Set `history.scrollRestoration = 'manual'` in `frontend/src/main.tsx` and mount `PageStateProvider` + `useScrollRestoration()` in `AppShell.tsx` so both wrap every route (D4)
- [x] 1.7 Verify a background refresh failure leaves restored data on screen (throw from `load` on the second call and confirm the page keeps its contents)

## 2. Adopt restoration across every page

- [x] 2.1 `HomePage`: replace the dashboard `useState`/`useEffect` with `usePageData('dashboard', getDashboard)`, keeping `handleEpisodesWatchedChange` patching through `setData` and `onCompleted` calling `reload`
- [x] 2.2 `MyListPage`: `usePageData('my-list', getMyList)`; move `statusFilter`, `sort`, and `airingStatusFirst` to `useRestorableState`
- [x] 2.3 `TopAnimePage`: `usePageData('top-anime', …)`; move `page` to `useRestorableState`
- [x] 2.4 `SeasonPage` and `AiringPage`: `usePageData` keyed by the season/week the URL carries, so a different season is a different snapshot; leave their `useSearchParams` view state alone (D5)
- [x] 2.5 `SearchPage`: `usePageData` keyed by the query string; move `visibleCount` to `useRestorableState` (D5)
- [x] 2.6 `AnimeDetailPage`: `usePageData(\`anime:${id}\`, …)` so each anime keeps its own snapshot and going back from one detail page to another restores the right anime
- [x] 2.7 `ProfilePage`: `usePageData` for the profile payload and for the top-anime and rewatched sections; move `mediaType` and `rewatchedMediaType` to `useRestorableState`
- [x] 2.8 Leave `SettingsPage`'s polled data as-is — it takes scroll restoration only (D5)
- [x] 2.9 Walk every page pair: navigate away and back from each page and confirm no empty flash, filters intact, scroll position intact; then confirm a navbar-link visit still opens on defaults at the top
- [x] 2.10 `ProfilePage`: keep the last-loaded top-anime/rewatched section on screen (via a ref, not `usePageData`'s own state) while a media-type tab switch is loading, so the strip's height — and anything gated on its presence, like the top-anime box's "Edit order" control — stays stable instead of visibly collapsing and reopening

## 3. Truncated-title tooltip

- [x] 3.1 Add `frontend/src/components/TruncatedTitle.tsx` + `.css`: `lines={1|2}` truncation, overflow detection on pointer-enter (`scrollWidth > clientWidth` / `scrollHeight > clientHeight`), and a `createPortal` tooltip to `document.body` that is `position: fixed`, offset below the pointer, `pointer-events: none`, viewport-clamped, and follows `mousemove` (D8)
- [x] 3.2 Verify a title that fits shows no tooltip, that the tooltip is not clipped by a scrolling container, and that clicking through the tooltip still opens the anime
- [x] 3.3 Floor the tooltip's vertical position at the truncated element's own bottom edge (not just `pointer.y + offset`), so hovering near the top of a tall two-line-clamped title doesn't put the tooltip on top of the row's own second line (D8)

## 4. Profile page layout

- [x] 4.1 `ProfilePage.css`: give `.top-anime-strip__item` the same fixed basis as `.rewatched-strip__item` (`flex: 0 0 calc((100% - 9 * 10px) / 10)`) so tiles stop shrinking past ten entries (D9)
- [x] 4.2 `ProfilePage.css`: add `overflow-y: hidden` to `.top-anime-strip` and `.rewatched-strip`, then confirm a vertical trackpad gesture over each strip moves nothing and a hovered edge tile is still not clipped (D9)
- [x] 4.3 `ProfilePage.css`: narrow the stats box — `grid-template-columns: clamp(200px, 18%, 260px) minmax(0, 1fr) minmax(0, 1.4fr)` on `.profile-page__top-row` plus `max-width: 300px` on `.profile-stats`, verified at both wide and sub-1024 px widths (D12)
- [x] 4.4 `index.css`: add the shared `.scroll-y` utility (`overflow-y: auto`, `scrollbar-gutter: stable`, `scrollbar-width: thin`, `::-webkit-scrollbar { width: 8px }`) (D10)
- [x] 4.5 `ProfilePage.css`: apply `.scroll-y` to `.activity-feed` and replace its fixed `max-height: 320px` with `flex: 1 1 auto; min-height: 0; max-height: 340px` so the list reaches the bottom of its box without over-stretching its shorter siblings (D10, D11)
- [x] 4.6 `ProfilePage.css`: give `.score-distribution__label` a fixed `2ch` width with `white-space: nowrap` — at 16px with no `nowrap` the two-digit "10" row wrapped its "0" onto its own line instead of lining up with the single-digit rows (D7)

## 5. Rating distribution

- [x] 5.1 `ProfilePage.tsx`: compute `maxCount` across buckets and drive bar width from `count / maxCount`, leaving an empty track when `maxCount` is 0 (D7)
- [x] 5.2 `ProfilePage.tsx`: render each row's trailing text as `count (share%)` using `count / totalRated`, rounded to a whole percent, shown as `<1%` for a non-zero count that rounds to zero, and with no share at all when nothing is rated (D7)
- [x] 5.3 `ProfilePage.css`: give `.score-distribution__count` a fixed `9ch` width with `white-space: nowrap` (not content-sized) so it fits `120 (40%)` on one line while keeping `tabular-nums` alignment — a content-sized count column let each row's bar-track resolve to a different width, so the bars weren't actually comparable to each other

## 6. Activity feed content (backend)

- [x] 6.1 `ProfileService.BuildActivityFeed`: accept `Added`, `Completed`, `ScoreChanged`, and genuine `EpisodeIncremented`; classify `EpisodeIncremented` and `Completed` as progress events; skip an increment when the last emitted item is a progress event for the same anime; never skip a completion (D6)
- [x] 6.2 Verify against the scenarios: watching a final episode yields one completion item and no increment item for that run; completing via status change yields a completion; a score change and a cleared score each yield one item; decreases, non-completion status changes, and rewatch-count changes still yield nothing
- [x] 6.3 Check `CHANGE_TYPE_LABELS` in `frontend/src/utils/anime.ts` and the `changeDetail` strings written by `UserAnimeEntryEditService` read correctly for the two newly surfaced types in the feed

## 7. Latest updates and full history rows

- [x] 7.1 `ProfilePage.tsx`: render feed titles through `TruncatedTitle` with `lines={1}` and drop the `title={…}` attribute from those rows (D8)
- [x] 7.2 `EditHistoryOverlay.tsx`: rebuild each row as picture + (title, change detail) + timestamp, matching the feed's row structure
- [x] 7.3 `EditHistoryOverlay.tsx`: show the poster from `item.pictureUrl` with the existing placeholder fallback, and take the title from `pickDisplayTitle(item.animeTitle, item.animeEnglishTitle)` so English titles win
- [x] 7.4 `EditHistoryOverlay.tsx`: render the title through `TruncatedTitle` with `lines={2}`
- [x] 7.5 `EditHistoryOverlay.css`: apply `.scroll-y` to `.edit-history__list` and restyle rows for the new picture column (D10)
- [x] 7.6 `ProfileService.GetActivityHistoryAsync`: collapse a consecutive same-anime run of genuine `EpisodeIncremented` entries into a single row reporting the episode range (e.g. "Episodes 4-8") instead of one row per episode; a run of one keeps its original "Episode N" wording; any other change type, a different anime, or a gap in the log ends the run (D13)

## 8. Verification

- [x] 8.1 Build the frontend with nvm's Node v22 (`nvm use 22 && npm run build`) and run `npm run lint`
- [x] 8.2 Build the backend via the `sdk:10.0` Docker image and run the backend test suite
- [x] 8.3 Run the app and walk the profile page end to end: strips, stats width, distribution bars and percentages, feed content and truncation, full-history overlay, and both scrollbars
- [x] 8.4 Run `openspec validate --changes improve-profile-page-ux`
