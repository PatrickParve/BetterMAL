## 1. Shared building blocks

- [x] 1.1 Add `frontend/src/hooks/useDelayedFlag.ts` (design D1). `useDelayedFlag(active: boolean, delayMs = LOADING_INDICATOR_DELAY_MS)` returns `true` only after `active` has stayed true for `delayMs`, and returns `false` the moment `active` goes false. Export `LOADING_INDICATOR_DELAY_MS = 300` from the same file as the one tunable constant.
- [x] 1.2 Add `frontend/src/components/LoadingNotice.tsx` and `.css`. `LoadingNotice({ active = true, className, children = 'Loading…' })` renders nothing until `useDelayedFlag(active)` is true, then renders `<p className={className}>` wrapped in a `loading-notice` element that fades in (`opacity` 0 → 1 over 200 ms). Under `@media (prefers-reduced-motion: reduce)`, it appears without the transition. The page's own loading class keeps its existing look.
- [x] 1.3 Add `frontend/src/hooks/useReconnectRetry.ts` (design D2, D7). `useReconnectRetry(failed: boolean, retry: () => void)` subscribes to `api/connectionStatus.ts`. On an unreachable → reachable transition (a listener call where `getSnapshot()` is now `true`), it calls `retry` once if `failed` and its armed ref is set, then disarms. It returns `rearm()` for Try again to call. It is armed on mount.
- [x] 1.4 Extend `frontend/src/hooks/usePageData.ts` with `failed` and `retry` (design D2):
  - `failed` is set in the catch of a `showLoading` run, and cleared when a `showLoading` run starts or any run succeeds.
  - A silent `reload()` or restore refresh failing never sets it.
  - `failed` is reset in the render-time key-change block beside `data`/`loading`.
  - `retry()` is `runLoad(true)` plus `rearm()`, and the hook wires `useReconnectRetry(failed, () => runLoad(true))`.
  - Update the header comment to describe the failure contract.
  - Keep the existing restore/skip semantics (`frontend-data-loading` spec) unchanged.
- [x] 1.5 Add `frontend/src/components/LoadFailedNotice.tsx` and `.css` (design D3). `LoadFailedNotice({ what, onRetry, compact? })` renders:
  - "Couldn't load {what}."
  - a second line from `useSyncExternalStore(subscribe, getSnapshot)`: while unreachable, "The server can't be reached — this will load by itself once it's back."; otherwise "Something went wrong on the server."
  - a **Try again** button in the app's shared button style.

  `compact` is for profile sections. It follows `section-colour-language` and the hover and pointer rules in `navigation-and-search`.
- [x] 1.6 Add `frontend/src/hooks/useScrollReveal.ts` (design D5). `useScrollReveal(sentinelRef, total, step, setVisibleCount)` owns the IntersectionObserver. Its callback runs `setVisibleCount(prev => (total === 0 || prev >= total ? prev : prev + step))`, so it never lowers the count and never steps an empty list. It re-observes when `total` or `setVisibleCount` changes.
- [x] 1.7 Add `frontend/src/hooks/useHeldData.ts` (design D6). `useHeldData<T>(data: T | null, failed: boolean)` returns `{ shown: T | null, stale: boolean }`:
  - it holds the last non-null `data` while `data` is null and `failed` is false;
  - `failed` drops the held value;
  - `stale` is true while a held value from before a key change is showing.

  Add a `.held-content--muted` class (`opacity: .5; pointer-events: none`), matching Top anime's muted list, applied when `useDelayedFlag(stale)` is true.

## 2. Season and Year pages

- [x] 2.1 `SeasonPage.tsx`: sequence the visit refresh after the read (design D4):
  - The refresh effect, keyed on `debouncedSeasonKey`, runs only once `seasonData !== null` for that same season, guarded by a `refreshedForKey` ref so it posts once per visit.
  - On `skipped`, it re-reads only when the settled data's `lastFetchedAt === null`.
  - It sets `refreshOutcome` after any re-read lands, not before.
  - A failed read posts no refresh. The retry's success posts it.
- [x] 2.2 `SeasonPage.tsx`: rewrite `terminalState` in the design D4 order:
  1. grid
  2. read `failed`: `LoadFailedNotice` ("this season", `retry`)
  3. read unsettled: `LoadingNotice`
  4. cached: `filtersEmpty` / `notListed`
  5. nothing cached and the refresh in flight or re-reading: `LoadingNotice` with "Fetching this season from MyAnimeList…"
  6. refresh failed: "This season couldn't be fetched from MyAnimeList — it'll be retried next time you open it." (no button)
- [x] 2.3 `SeasonPage.tsx`: show "Updating…" only when `useDelayedFlag(refreshing)` is true.
- [x] 2.4 `SeasonPage.tsx`: replace the inline IntersectionObserver with `useScrollReveal`.
- [x] 2.5 `SeasonPage.tsx`: hold the previous season's listing across a season step with `useHeldData` (design D6). Feed `shown` into `items`, and add the muted class to the grid while `stale` is past the delay.
- [x] 2.6 `YearPage.tsx`: apply 2.1–2.5 identically, with the text "Fetching this year from MyAnimeList…", "This year couldn't be fetched from MyAnimeList — it'll be retried next time you open it." and `LoadFailedNotice` ("this year"). For year step holding, `useHeldData` keys on the year.
- [x] 2.7 Update the `SeasonPageView`/`YearPageView` header comments (the "two separate effects" paragraph) to describe the read-then-refresh order.

## 3. Series page and the other reveal grids

- [x] 3.1 `SeriesBrowserPage.tsx`:
  - drop the `.catch(() => ({ items: [], loadFailed: true }))` and the `loadFailed` field, and use `usePageData`'s `failed`/`retry`;
  - compute `terminalState` from `sortedItems.length` (the whole filtered list), never `visibleItems.length`;
  - render `LoadingNotice` and `LoadFailedNotice` ("the series list");
  - switch to `useScrollReveal`.
- [x] 3.2 `SearchPage.tsx`:
  - switch to `useScrollReveal`;
  - render `LoadingNotice`;
  - when `failed`, render `LoadFailedNotice` ("the search results") in place of "No anime found.";
  - make sure the grid's empty and no-match messages are only reached with a settled read.
- [x] 3.3 `MyListPage.tsx`:
  - switch the observer to `useScrollReveal`, keeping the page's own render-time reveal reset;
  - replace the "Loading…" branch of `renderBody` with `LoadingNotice`;
  - add a `failed` branch before the "Nothing here yet" check, rendering `LoadFailedNotice` ("your list"). The recap-scope read's own failure does the same with "this recap period's list".

## 4. Top anime

- [x] 4.1 `TopAnimePage.tsx`:
  - `loadTopAnimeOnce` deletes its `inFlightLoadByType` entry on rejection too, and rethrows;
  - the page's effect catches and sets `failedType` to the selected type, and a new selection or success clears it;
  - with `failedType === selectedType`, render `LoadFailedNotice` ("this ranking") in place of the content and the muted fallback.
- [x] 4.2 `TopAnimePage.tsx`: wire Try again and `useReconnectRetry(failedType === selectedType, …)` to re-run the load. Replace the first-arrival "Loading…" with `LoadingNotice`.

## 5. Remaining pages, sections and overlays

- [x] 5.1 `HomePage.tsx`: render `LoadingNotice` while `loading`, and `LoadFailedNotice` ("the dashboard") when `failed`, instead of `return null` in both cases.
- [x] 5.2 `AiringPage.tsx`:
  - render `LoadingNotice` while the week is loading;
  - render `LoadFailedNotice` ("this week's schedule") when `failed`;
  - hold the previous week across a week step with `useHeldData`, muted after the delay, and derive the range label and grid from `shown`.
- [x] 5.3 `RecapPage.tsx`: replace its loading line with `LoadingNotice`. Add `LoadFailedNotice` ("this recap") when `failed` and nothing is displayed. Leave its existing hold logic alone.
- [x] 5.4 `ProfilePage.tsx`: replace the whole-page "Loading…" with `LoadingNotice`, and the "Couldn't load profile data." line with `LoadFailedNotice` ("your profile"). For each section (top anime, top series, rewatched, rewatched series, time spent by series), render a compact `LoadFailedNotice` naming the section when its `failed` is true, before its empty-message branch.
- [x] 5.5 `context/AppStatusContext.tsx` and `SettingsPage.tsx` (design D8): the provider exposes `failed` (a read failed while `status === null`) and refreshes immediately on the reconnect transition. Settings renders `LoadFailedNotice` ("settings") when `failed`, and `LoadingNotice` otherwise while it has no status.
- [x] 5.6 `AnimeDetailPage.tsx` and `SeriesPage.tsx`: replace "Loading…" with `LoadingNotice`, and "Couldn't load this anime." / "Couldn't load this series." with `LoadFailedNotice` ("this anime" / "this series"). Keep each page's 404 handling as it is.
- [x] 5.7 `AnimeRankOverlay.tsx`, `EditHistoryOverlay.tsx`, `Updates/UpdatesHistoryOverlay.tsx`: replace their "Loading…" lines with `LoadingNotice`, keeping each overlay's class.
- [x] 5.8 Sweep for any remaining `Loading…`/`Loading&hellip;` text and any `.catch(() => {})` on a page read that leaves a page blank or empty, and bring each onto the shared pieces.

## 6. First-load screen

- [x] 6.1 `App.tsx` (design D8):
  - while `statusError`, re-read `getMalAuthStatus()` every `HEALTH_POLL_INTERVAL_MS` (export it from `ConnectionStatusNotice.tsx` or move it to `api/connectionStatus.ts`), and add a Try again button that re-reads at once;
  - success clears `statusError` and sets `status`;
  - change the copy to say it is retrying rather than asking for a reload;
  - replace the pre-shell "Loading…" with `LoadingNotice`.

## 7. TMDB display widths

- [x] 7.1 Measure each picture surface's maximum rendered width at a 2,400 px and a 1,024 px wide viewport, and at phone width, on a scratch stack, then settle design D9's tier table. Measure:
  - row slots;
  - Season/Year/Search/Series grid cards;
  - dashboard carousel and "Followed shows airing" cards;
  - profile strips;
  - score board;
  - recap podium;
  - Top anime showcase, cards and rows;
  - series page More tiles and timeline cards;
  - airing band;
  - picker options;
  - detail picture;
  - series header.

  Record the numbers in design.md under D9. Done: the measurements moved the table from three tiers to four (`tile`, `w500`, added for the profile strips, picker options and the two tallest row slots), and put the recap podium on `hero`.
- [x] 7.2 Add `displayPictureUrl(url, tier: 'row' | 'tile' | 'card' | 'hero')` to `utils/anime.ts`, next to `isTmdbImageUrl`. Only a `https://image.tmdb.org/t/p/original/` URL is rewritten to `w342`/`w500`/`w780`/`w1280`, and anything else is returned unchanged. Add `hooks/useDisplayPicture.ts`, which returns `{ displaySrc, onError }` and holds the original fallback for every surface below. Comment that the `original` URL stays the identity (tmdb-artwork D6/D9) and that this changes only the download.
- [x] 7.3 `PosterPicture.tsx`: add `tier` (default `'card'`). Compute `displaySrc` once (`useDisplayPicture`) and use it for the art and the fill. The art's `onError` while `displaySrc !== src` switches both to `src`, and a different `src` starts fitted again. Pass `tier="tile"` from the five profile strips and `tier="hero"` from the recap podium, the hosts 7.1 found off the card tier.
- [x] 7.4 `RowPicture.tsx`: the same, with `tier` defaulting to `'row'`; `AiringTodayList` and `CompletionScoreOverlay`, whose wide pictures reach 192 and 242 px, pass `tier="tile"`. `SeriesEntryRow.tsx` and `Updates/UpdateCard.tsx` keep their own `<img>` (one crops every shape to a poster slot, the other caps at 170 px, so neither moves onto `RowPicture`) and use `useDisplayPicture(url, 'row')` with the same fallback.
- [x] 7.5 `PicturePickerOverlay.tsx`: render option images at the `tile` width (they measure 249 px at most) with the original fallback. Leave `onPick(option.url)` and the `selected` comparison on `option.url`. Update the comment that says a group "can still hold a hundred `original` images".
- [x] 7.6 `AnimeDetailPage.tsx` and `SeriesPage.tsx`: the header `<img>` uses the `hero` width with the original fallback, and the landscape check reads the displayed `<img>`. The landscape and whole-picture classification keeps working, since a rendition keeps the original's proportions.

## 8. Picture arrival fade

- [x] 8.1 `hooks/useLandscapePicture.ts`: `useOrientationPicture` also returns `arrival: 'held' | 'pending' | 'loaded'`. It is `'held'` when the node is `complete` with a non-zero `naturalHeight` at ref attach, or when its `load` event comes within `PICTURE_FADE_DELAY_MS` (150 ms, exported from the same file) of attaching; `'loaded'` on a later `load` event; and `'pending'` otherwise. It resets with `src`. Added after trying it on real data: the first version faded every `load`, which also faded quick disk-cached pictures and made pages feel slower. `usePictureShape` passes it through. Existing callers (detail, series header) ignore it.
- [x] 8.2 `PosterPicture.tsx`/`.css`: put `poster-picture--pending|loaded|held` on the wrapper:
  - `pending`: the art `opacity: 0` (the fill is not mounted yet);
  - `loaded` (a slow picture): art and fill fade in from `opacity: 0` over 180 ms, as a keyframe animation, since the fill mounts already loaded and has no earlier value to transition from;
  - `held`: no animation (a cached or quick picture);
  - `prefers-reduced-motion`: no animation.

  Make sure the box's placeholder background shows while pending, on every host listed in `artwork-presentation`'s surface lists.
- [x] 8.3 `RowPicture.tsx`/`.css`: the same three classes, on a new `row-picture-frame` around the `<img>`, because an `<img>`'s opacity takes its own background with it. The frame carries the host's class and the placeholder background, so the slot's background shows while pending.

## 9. Verification

- [x] 9.1 `cd frontend && PATH="$HOME/.nvm/versions/node/v22.23.1/bin:$PATH" npm run build` (tsc and vite) and `npx oxlint` pass with no new warnings.
- [x] 9.2 Bring up a scratch stack per the headless-Chrome recipe (scratch Postgres, the backend in Production on a scratch port, `vite preview`, a fake token row, seeded my-list, season, series and TMDB-choice rows). Never point the tests at the user's Docker stack. Inject a `Page.addScriptToEvaluateOnNewDocument` MutationObserver that logs every text node containing "Loading", "couldn't", "No series match", "No anime match", "Nothing here" or "not listed", with a timestamp.
- [x] 9.3 Fast-path checks, with no API delay: open `/series`, `/season` (a season fetched today), `/year`, `/top`, `/`, `/my-list`, `/airing` and `/profile` fresh. For each, assert the log is empty before content appears, that there is exactly one `GET` of the season or year listing, and that `Updating…` is never drawn on a skipped refresh.
- [x] 9.4 Slow-path checks, holding `/api/*` responses with CDP `Fetch`:
  - delay 1 s: assert the loading notice appears at about 300 ms with its fade, and that content replaces it on arrival;
  - delay 320 ms: assert the notice is replaced before it is visibly drawn (computed opacity < 0.2 at replacement);
  - a never-cached season with the refresh held: assert "Fetching this season from MyAnimeList…" and then the grid.

  Done: the 320 ms figure is not a stable threshold on its own, because the backend's own 10–45 ms is added to a held request's delay. It was verified as a sweep of held delays from 150 to 320 ms on the Series page instead. A read landing before the delay draws no notice. A notice replaced within about 35 ms is still at opacity 0.00. Across the sweep the opacity at replacement tracks lifetime/200 ms (0.13 at 44 ms in one run, 0.25 at 53 ms in another). The never-cached case also covers the other three refresh outcomes (failed, notListed, and a skipped one whose read found nothing, the cross-tab case) and the Year page. A faked `notListed` outcome has to write the fetch-log row the real backend writes, since the page derives "not listed" from the re-read.
- [x] 9.5 Stepping checks: step Season, Year and Airing with fast responses and assert no empty frame. With 1 s responses, assert the previous grid or week is held, muted after about 300 ms, and replaced on arrival, and that the page scrolled to the top.
- [x] 9.6 Failure and offline checks, failing `/api/*` with `Fetch.failRequest` and then with 502 and 500:
  - every page in 5.x shows `LoadFailedNotice`, not an empty message or blank;
  - the connection bar shows;
  - after un-failing, the health poll clears the bar and each failed page reloads by itself once;
  - with a persistent 500 on one read, exactly one automatic retry happens, then no more requests for it until Try again;
  - Top anime recovers without a reload;
  - the first-load screen, with `/api/mal-auth/status` failing, recovers by itself within 5 s and on Try again.
- [x] 9.7 Picture checks, intercepting `image.tmdb.org` and `cdn.myanimelist.net`:
  - a TMDB choice is requested as `w780` on a grid card, `w342` in a My list row and `w1280` on the detail page, and never as `original`;
  - MAL URLs are unchanged;
  - picking a picker option stores the `original` URL (poll the API);
  - failing the `w780` request loads `original`;
  - a delayed image fades in, a cached image on back-navigation has no fade, and reduced-motion emulation has no transition.
- [x] 9.8 Tear down the scratch stack by port, stop the scratch container, and confirm the user's stack (`bettermal-*`) is still up and untouched.

## 10. Docs and wrap-up

- [x] 10.1 Update `CODE_GUIDE.md`'s page table (Season, Year, Search, My list, Series rows):
  - the shared `useScrollReveal` replaces "an IntersectionObserver sentinel";
  - add a short section on `usePageData`'s `failed`/`retry`, `LoadingNotice`, `LoadFailedNotice`, `useHeldData` and `useReconnectRetry`;
  - update the TMDB picture section for `displayPictureUrl`.
- [x] 10.2 Ask the user before rebuilding the frontend of their Docker stack (`docker compose up -d --build frontend`). After the rebuild, have them check Season, Year, Top anime and Series on their real data, and confirm the 300 ms delay and the fades feel right.

  Rebuilt on 2026-09-25 with the user's go-ahead. The served bundle is the one verified in section 9 (`index-CGQ8qqqa.js`, `index-CNXoS0FG.css`). Compose also recreated the backend container as a dependency of `frontend`; its image was unchanged and Postgres was not touched. The user's check of Season, Year, Top anime and Series on their real data is done, confirmed at archive time.
