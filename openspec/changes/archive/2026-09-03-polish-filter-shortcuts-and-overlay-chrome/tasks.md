## 1. The shared filter control's state, label and chrome (design D1–D4)

- [x] 1.1 In `frontend/src/components/FilterMultiSelect.tsx`, change `selected` and `onChange`'s argument to `string[] | null`. Document the contract at the top of the props type: `null` is **All** (no restriction), an array is exactly those values, and `[]` is **None** (nothing passes) — and say why the empty array could not keep meaning "unfiltered" (it is the one state the two shortcuts have to be able to differ in, design D1).
- [x] 1.2 Rewrite `toggle(value)` for the three cases: from `null`, emit every option except this one; from an array, add or remove the value. Route every emission through one `emit(next: string[])` helper that returns `null` when `next` covers every offered option, so **All** has a single representation however it was reached (design D2).
- [x] 1.3 Point the **All** shortcut at `onChange(null)` and **None** at `onChange([])`. Comment that **All** deliberately does *not* write out every value: two states that render identically must not be separately expressible in a URL or a snapshot.
- [x] 1.4 Rewrite `summary()` to the four-branch rule — `All`, `None`, the single option's label, `N selected` — and note that the count branch is now unreachable for a full selection because `emit` normalises it away.
- [x] 1.5 Add `summaryCandidates()`: `All`, `None`, every option's label, and `${n} selected` for n from 2 to `options.length - 1`. Render the live summary plus every other candidate inside the button, each candidate `aria-hidden="true"`, so only the state in force is announced (design D3).
- [x] 1.6 In `frontend/src/components/FilterMultiSelect.css`, stack those spans in one grid cell on `.filter-multi-select__button` (`display: grid`, one named area, every child assigned to it, non-current children `visibility: hidden`). Keep the button's existing `--control-h`, padding, border and hover rules untouched — `page-header-design`'s shared-height requirement still owns the height. Comment why a pixel `min-width` was rejected (fluid root font size; labels are data, not fixed strings).
- [x] 1.7 Restyle `.filter-multi-select__shortcut` as a bordered button in the small-control family — 1px `--border`, rounded, `--bg`, hovering to `--accent-bg`/`--accent-border` with `--text-h`, and a `:focus-visible` outline — replacing `border: none; background: none` and the hover underline. Make `.filter-multi-select__shortcuts` a two-column grid of equal widths so the pair is even, and keep the divider beneath it.
- [x] 1.8 Check the panel's `min-width: 180px` still holds the two buttons and the longest option label without the panel resizing between states.

## 2. The three URL-backed call sites (design D1)

- [x] 2.1 In `frontend/src/pages/SearchPage.tsx`, parse the type parameter as `typeParam === null ? null : typeParam.split(',').filter(Boolean)`, and have `setTypeFilter` write no parameter for `null`, an empty parameter for `[]`, and the CSV otherwise. Comment that `URLSearchParams.get` already distinguishes absent from present-and-empty, which is what makes the third state free.
- [x] 2.2 Change `filteredItems` to `typeFilter === null ? items : items.filter(...)`, so `[]` yields nothing rather than everything.
- [x] 2.3 Apply the same two changes in `frontend/src/pages/SeasonPage.tsx` and `frontend/src/pages/YearPage.tsx`, where the same parse and the same `typeFilter.length > 0 && !includes` predicate appear.
- [x] 2.4 Confirm each page's "toggling the filter is a fresh view" behaviour is unchanged — the three pages already push a new entry on a filter change and this touches only the value written.

## 3. My List's two filters (design D1)

- [x] 3.1 In `frontend/src/pages/MyListPage.tsx`, widen `FocusSeed.typeFilter` and both `useRestorableState` declarations to `string[] | null`, seed `typeFilter` and `airingFilter` from `null`, and change the `none` focus seed's `typeFilter: []` to `null`. The movie-scoped seed keeps `['movie']`.
- [x] 3.2 Change both predicates in `derived` to `if (typeFilter !== null && !typeFilter.includes(item.mediaType ?? 'unknown')) return false` and the airing equivalent.
- [x] 3.3 Change the "any filter is narrowing the list" test that drives the count line and the clear-filters empty state from `typeFilter.length > 0 || airingFilter.length > 0` to `typeFilter !== null || airingFilter !== null`, and `airingBadgeActive` likewise — both always meant "is this filter doing anything".
- [x] 3.4 Verify by hand that **None** on either filter reaches the existing "nothing matches the current filters" state with its clear-filters affordance, rather than the "nothing here yet" empty-status message.

## 4. Empty states for the new None case (specs: season-browser, year-browser, navigation-and-search)

- [x] 4.1 Confirm `SeasonPage`'s and `YearPage`'s `terminalState` already resolve **None** to `filtersEmpty` (`displayed.length === 0` with a cached listing present) and therefore show "No anime match the current filters" rather than the not-listed or load-failed message. Fix the ordering if it does not.
- [x] 4.2 In `SearchPage.tsx`, add the missing case: when the query matched anime but the type filter leaves no card, show "No anime match the current filters" rather than falling through to a silent empty grid. Keep it distinct from the existing "No anime found." (nothing matched the query at all) and keep any matched series cards rendered alongside it, since the type filter does not apply to series.

## 5. Type-ahead dismissal (design D5)

- [x] 5.1 In `frontend/src/hooks/useAnimeSearch.ts`, replace the exported `setOpen` with `dismiss()` and `reopen()`. Hold the dismissed query in a ref; `dismiss()` records the current debounced query and closes.
- [x] 5.2 Gate the fetch's `.then` on the resolved query not being the dismissed one, so a response in flight when the dropdown was dismissed cannot reopen it. Comment that this closes the race for all three dismissals (submit, Escape, click-outside) at the one place that opens the dropdown, and that dismissal is scoped to that query so typing reopens normally.
- [x] 5.3 In `frontend/src/components/SearchBar.tsx`, add a ref to the input; in `submitSearch`, call `dismiss()` then `blur()` before navigating. Leave the query text in place — "Search submission keeps the query text" still holds.
- [x] 5.4 Point `SearchBar`'s Escape handler and its `useClickOutside` at `dismiss()`, and its `onFocus` at `reopen()`.
- [x] 5.5 Update `frontend/src/pages/SettingsPage.tsx`'s anime-refresh picker to the same two functions (`useClickOutside` and the post-selection close call `dismiss()`, `onFocus` `reopen()`). Its behaviour is unchanged; it inherits the same race fix.

## 6. One restorable-scroll mechanism (design D6)

- [x] 6.1 In `frontend/src/state/pageStateStore.ts`, rename `PageSnapshot.strips` to `scrollers` and `putStripScroll` to `putScrollerOffset`, and update the comment to say the map holds named in-page scroll containers' offsets on whichever axis each scroller uses — nothing about it was ever horizontal but its name.
- [x] 6.2 Add `frontend/src/hooks/useRestorableScroll.ts` holding the record-and-restore half of `ProfilePage`'s `useStripScroll`, parameterised by `restoreKey` and axis: the ref callback that attaches the `scroll` listener writing through `putScrollerOffset`, and the restore-on-`isRestore` path with its `ResizeObserver` retry and its cancel-on-user-input. Move the existing comments with the code — the two explaining why this is a ref callback rather than an effect, and why the offset is recorded synchronously — since they are the reason the shape is what it is.
- [x] 6.3 Rewrite `useStripScroll` in `frontend/src/pages/ProfilePage.tsx` to keep only its drag handlers and compose the new hook's ref callback with its own. Its `restoreKey` values and its `onItemClick` drag suppression are unchanged.
- [x] 6.4 Check the profile page's strips before going further: scroll "My top anime" and "Most rewatched" to different offsets under different media-type tabs, open a tile, go back, and confirm each strip returns to its own offset for the restored tab.

## 7. The score board is restored (design D6, D7)

- [x] 7.1 In `frontend/src/components/Modal.tsx`, add an optional `contentRef` prop forwarded to the `.modal` box. Note that this is the score board's scroll container, and that making the board its own scroll container instead (as `.modal--rank` does) was rejected because it would move the header out of the scroll flow.
- [x] 7.2 In `frontend/src/pages/RecapPage.tsx`, change `boardOpen` from `useState(false)` to `useRestorableState<boolean>('scoreBoard', false)`. Replace the note that the board is "page-local state … no URL parameter and no history entry" with the current rule: still no parameter and still no history entry, but recorded in the entry's snapshot so a restore rebuilds the page as it was left (design D7).
- [x] 7.3 In `frontend/src/components/ScoreBoardOverlay.tsx`, call `useRestorableScroll` on the vertical axis under a fixed key and pass its ref callback to `Modal`'s `contentRef`.
- [x] 7.4 Verify the restore end to end: open the board, scroll well down its slots, follow a poster, go back — the board is open at that offset, and closing it leaves the recap page at the position it was left at.
- [x] 7.5 Verify the two orderings design D7 relies on: leaving `/recap` does not write `false` over the recorded open state (the route change unmounts `RecapPage` and its `Modal` before the pathname effect could fire), and the page's scroll restore lands before `useScrollLock` takes the body — the layout effect precedes the passive one.
- [x] 7.6 Confirm the board still closes on Escape, on a click outside, and on navigation, and that opening it still does not give the back gesture something to stop at.

## 8. Scrollbars (design D8)

- [x] 8.1 In `frontend/src/index.css`, add `.scroll-hidden` beside `.scroll-y` — `overflow-y: auto`, `scrollbar-width: none`, and a `::-webkit-scrollbar { display: none }` rule. Comment that it is the deliberate opposite of `.scroll-y`, not a modifier of it, and point at `UpdatesMenu.css`'s note on why layering the two is the wrong move.
- [x] 8.2 Swap `.scroll-y` for `.scroll-hidden` on `ProfilePage.tsx`'s `activity-feed` (Latest updates) and `divergence-list` (both opinion lists), and on `EditHistoryOverlay.tsx`'s `edit-history__list`.
- [x] 8.3 Give `RankingOverlay.tsx`'s list the same class and drop the now-duplicated `overflow-y: auto` from `.ranking-overlay__list` in `RankingOverlay.css`, keeping its `max-height` calculation — that is what holds the eight-whole-rows rule, and it is unchanged.
- [x] 8.4 Check each of the five lists in a WebKit browser and a Gecko one: no bar at rest or while scrolling, wheel and drag still reach the last row, and the rows have taken back the gutter `scrollbar-gutter: stable` was reserving.
- [x] 8.5 Confirm the recap page's four "See all" overlays (Season ranking, Year ranking, Seasons by time watched, Years by time watched) are covered by 8.3 — they open the same `RankingOverlay` the profile's favourites rankings do.

## 9. Build and walk the change

- [x] 9.1 Run `npm run build` in `frontend` under Node 22 (the default is v16; see the build memory) and fix anything the `string[] | null` widening or the `strips` rename surfaces.
- [x] 9.2 Run `npm run lint`.
- [x] 9.3 Walk all five filters — Search Type, Season Type, Year Type, My List Type and Airing — through All → None → one → several → All, checking the label at each step and that the trigger and the cluster beside it never move.
- [x] 9.4 Check that a link carrying no `type` parameter still opens on All on all three URL-backed pages, and that a `?type=` link opens on None with the filtered-to-nothing message.
- [x] 9.5 Submit a search from the navbar with suggestions showing and again mid-request: no dropdown over the results either time, and the field unfocused; then type again and confirm suggestions return.
